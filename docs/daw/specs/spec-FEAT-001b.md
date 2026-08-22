# Spec FEAT-001b: Crear mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| PRD | docs/daw/prd/prd-FEAT-001b.md |
| Tier | FEATURE |
| Date | 2026-08-16 |
| Spec loops | 0 |

## Summary

Un usuario autenticado sube la fotografía de un mural (JPEG/PNG/WebP, ≤10MB, validada también por
firma de bytes) junto con su ubicación (GPS o manual). El backend orquesta: subida a un contenedor
privado de Azure Storage, validación automática NSFW (NsfwSpy), y persistencia con estado inicial
`Pending` o `Rejected` según el resultado del scan. La foto solo se sirve mediante una URL SAS de
solo lectura y corta duración (5 min), generada al momento de la respuesta, y el acceso a un mural
`Pending`/`Rejected` queda restringido a su dueño o a un Administrador (mismo mensaje genérico de
"no encontrado" en ambos casos de denegación, para no confirmar por enumeración qué Ids existen).

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 4, Block 7 |
| FR-02 | Block 4, Block 7 |
| FR-03 | Block 4, Block 7 |
| FR-04 | Block 4, Block 7 |
| FR-05 | Block 4, Block 7 |
| FR-06 | Block 7 |
| FR-07 | Block 4, Block 8 |
| FR-08 | Block 3, Block 4 |
| FR-09 | Block 3, Block 4 |
| FR-10 | Block 3 |
| FR-11 | Block 1, Block 4 |
| FR-12 | Block 4 |
| FR-13 | Block 7 |
| FR-14 | Block 7 |
| FR-15 | Block 2, Block 5 |
| FR-16 | Block 5 |
| NFR-01 | Strategy: Block 4 valida `IFormFile.Length` ≤ 10MB (FluentValidation) antes de subir |
| NFR-02 | Strategy: Block 7 usa `navigator.geolocation.getCurrentPosition({ enableHighAccuracy: true })` |
| NFR-03 | Strategy: Block 2 crea el contenedor explícitamente con `PublicAccessType.None` |

## Dependencies between blocks

```
Block 1 (Domain: Mural) ──┐
Block 2 (Storage)        ─┼──▶ Block 4 (Crear mural) ──▶ Block 6 (NSwag + mural.service.ts)
Block 3 (NSFW)           ─┘         │                              │
                                     ▼                              ▼
                          Block 5 (Consultar mural) ────────▶ Block 7 (Formulario) ──▶ Block 8 (Routing)
```

- Block 2 y Block 3 son independientes entre sí y de Block 1 (servicios técnicos de Infrastructure,
  sin dependencia del modelo de dominio).
- Block 4 depende de Block 1, Block 2 y Block 3 (los orquesta).
- Block 5 depende de Block 1 y Block 2 (no de Block 3 — consultar no vuelve a escanear).
- Block 6 depende de Block 4 y Block 5 (necesita el OpenAPI expuesto por ambos endpoints para
  regenerar `MuralsClient`).
- Block 7 depende de Block 6. Block 8 depende de Block 7.
- Orden de ejecución sugerido: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8.

## Block 1 — Domain: entidad Mural

**Files**
- `backend/src/Paretto.Domain/Entities/Mural.cs` (new)
- `backend/src/Paretto.Domain/Enums/MuralStatus.cs` (new)
- `backend/src/Paretto.Infrastructure/Data/AppDbContext.cs` (modified — `DbSet<Mural>` + mapping en `OnModelCreating`)
- `backend/src/Paretto.Infrastructure/Data/Migrations/{timestamp}_AddMurals.cs` + `.Designer.cs` (new)
- `backend/src/Paretto.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` (modified — actualizado por la migración)

**Logic**
- `Mural`: `Id` (Guid, PK, `Guid.NewGuid()` por defecto — mismo patrón que `User`/`Session`),
  `UserId` (Guid, FK a `User`), `PhotoBlobName` (string — nombre del blob, NUNCA una URL ni el
  nombre de archivo original del cliente), `Latitude`/`Longitude` (double), `Status` (`MuralStatus`,
  default `Pending`), `CreatedAt` (DateTime, default `DateTime.UtcNow`).
- `MuralStatus`: `Pending = 0`, `Rejected = 1`. **Deliberadamente sin `Published`** — este
  sub-ticket nunca mueve un mural a publicado (ver "Out of Scope" del PRD); `Published` lo agrega
  FEAT-001c cuando implemente la aprobación. No es un enum incompleto, es el alcance de este ticket.
- Mapping: `PhotoBlobName` `IsRequired().HasMaxLength(300)` (blobs se nombran `{Guid}{extensión}`,
  muy por debajo del límite); `Latitude`/`Longitude` `IsRequired()`; `Status` `IsRequired()`
  `.HasDefaultValue(MuralStatus.Pending)`; `CreatedAt` `IsRequired()`. FK `UserId → User.Id` con
  `OnDelete(DeleteBehavior.Restrict)` (a diferencia de `Session`, que sí cascadea: un mural es
  contenido generado por el usuario, no debe desaparecer silenciosamente si en el futuro se
  implementara borrado de cuentas — decisión conservadora, no hay requisito de borrado de cuenta en
  este PRD ni en el de FEAT-001a).

**Data model**
- Entidad `Mural`: `Id: Guid (PK)`, `UserId: Guid (FK→User, not null, Restrict)`,
  `PhotoBlobName: string (not null, max 300)`, `Latitude: double (not null)`,
  `Longitude: double (not null)`, `Status: MuralStatus (not null, default Pending)`,
  `CreatedAt: DateTime (not null, default UtcNow)`. Índice implícito de EF Core sobre `UserId` (FK).

**Error handling**
- N/A (bloque de solo modelo/migración, sin lógica de ejecución propia).

**Required tests**
- [ ] `MuralPersistenceTests`: crear un `Mural` con todos los campos y recuperarlo — valores
      persistidos correctamente, `Status` default `Pending` si no se especifica, `CreatedAt` se
      completa automáticamente (mismo estilo que `AuthPersistenceTests.cs`).
- [ ] La migración aplica limpiamente sobre una base vacía (verificado al correr la suite, que
      recrea el esquema — mismo mecanismo ya usado por los tests de Auth).

**Completion criterion**
`dotnet ef migrations list` muestra `AddMurals` aplicada sin errores; `MuralPersistenceTests` pasa.

**Rollback considerations**
Revertir con `dotnet ef database update <migración previa>` + `dotnet ef migrations remove`; no hay
datos preexistentes que migrar (tabla nueva), por lo que el rollback es no destructivo.

---

## Block 2 — Infrastructure: almacenamiento de fotos (Azure Storage)

**Files**
- `backend/src/Paretto.Infrastructure/Storage/IBlobStorageService.cs` (new)
- `backend/src/Paretto.Infrastructure/Storage/AzureBlobStorageService.cs` (new)
- `backend/src/Paretto.Infrastructure/Paretto.Infrastructure.csproj` (modified — `Azure.Storage.Blobs`, declarado en AGENTS.md "Stack" → Infrastructure)
- `backend/src/Paretto.Api/Program.cs` (modified — registrar `IBlobStorageService`, mismo patrón que `IPasswordHasher`)
- `backend/src/Paretto.Api/appsettings.json` (modified — sección `AzureStorage` con `ConnectionString` VACÍO)
- `backend/src/Paretto.Api/appsettings.Development.json` (modified — `AzureStorage:ConnectionString = "UseDevelopmentStorage=true"` (Azurite), `ContainerName = "mural-photos"`)

**Logic**
- `IBlobStorageService`: `Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct)` (devuelve el `blobName` recibido, por simetría con el resto de la interfaz) y `string GenerateReadSasUrl(string blobName, TimeSpan validity)`.
- `AzureBlobStorageService`: usa `BlobServiceClient` construido desde `AzureStorage:ConnectionString`
  (`IConfiguration`). Antes de la primera subida, asegura el contenedor con
  `CreateIfNotExistsAsync(PublicAccessType.None)` — **mitigación NFR-03 / threat model R (contenedor
  privado, sin acceso público anónimo)**, explícito y no implícito en "subir la imagen".
  `GenerateReadSasUrl` emite un SAS de blob (no de cuenta ni de contenedor) con permiso `Read`
  únicamente y `ExpiresOn = DateTimeOffset.UtcNow.Add(validity)`.
- **El nombre del blob SIEMPRE lo genera quien llama a `UploadAsync` (Block 4), nunca este
  servicio a partir del nombre de archivo original del cliente** — mitigación de path
  traversal/overwrite (threat model R4).
- Credenciales: `appsettings.json` (no-Development) queda con `AzureStorage:ConnectionString` VACÍO
  a propósito — en un despliegue real se provee por variable de entorno/User Secrets/Key Vault,
  nunca committeado (threat model R5). El valor de Azurite en `appsettings.Development.json` es un
  valor público bien conocido, no un secreto.

**API contract**
N/A (servicio interno, sin endpoint propio).

**Data model**
N/A.

**Input validation**
N/A (no recibe input de usuario final directamente; el `blobName` que recibe ya fue generado y
validado por el llamador).

**Error handling**
- Si Azurite/Azure Storage no está disponible, `UploadAsync` propaga la excepción del SDK — la
  captura y traduce el Handler de Block 4 (`MuralPersistenceException`, ver ese bloque).

**Required tests**
- [ ] `AzureBlobStorageServiceTests`: `CreateIfNotExistsAsync` se invoca con `PublicAccessType.None`
      (verificable contra Azurite en la suite de integración, o mediante un wrapper testeable del
      `BlobContainerClient` si Azurite no está disponible en CI).
- [ ] `GenerateReadSasUrl` produce una URL cuyo SAS tiene permiso de solo lectura y un `se=`
      (expiry) de aproximadamente `now + 5 min` — testeable sin red real, la generación de SAS con
      autenticación por account key es determinística dado un connection string válido.
- [ ] `UploadAsync` contra un connection string inválido/Azurite no disponible propaga la excepción
      del SDK (no la traga) — valida el comportamiento documentado arriba en "Error handling", del
      que depende Block 4 para traducirlo a `MuralPersistenceException`.

**Completion criterion**
Los tres tests anteriores pasan; subir un archivo de prueba contra Azurite (entorno local) y
recuperarlo vía la URL SAS generada funciona manualmente.

---

## Block 3 — Infrastructure: validación NSFW (NsfwSpy)

**Files**
- `backend/src/Paretto.Infrastructure/Moderation/INsfwContentScanner.cs` (new)
- `backend/src/Paretto.Infrastructure/Moderation/NsfwScanResult.cs` (new — enum `Clean`, `Nsfw`, `Inconclusive`)
- `backend/src/Paretto.Infrastructure/Moderation/NsfwSpyContentScanner.cs` (new)
- `backend/src/Paretto.Infrastructure/Paretto.Infrastructure.csproj` (modified — paquete `NsfwSpy`, declarado en AGENTS.md "Stack" → Infrastructure; verificar antes de agregarlo que no tenga CVEs Critical/High conocidos)
- `backend/src/Paretto.Api/Program.cs` (modified — registrar `INsfwContentScanner`)

**Logic**
- `INsfwContentScanner.ScanAsync(Stream imageContent, CancellationToken ct) : Task<NsfwScanResult>`.
- `NsfwSpyContentScanner` envuelve el clasificador subyacente (inyectado como una dependencia propia
  para que sea reemplazable en tests). Corre la inferencia con un timeout explícito (p. ej. 5s, vía
  `CancellationTokenSource` enlazado) — **mitigación threat model R6** (un archivo malformado no
  puede colgar el request indefinidamente).
- **Cualquier excepción o timeout se captura, se loguea con `ILogger<NsfwSpyContentScanner>` en
  nivel `Warning` (mensaje + excepción completa) y ENTONCES se devuelve `Inconclusive`** — nunca se
  propaga y nunca se descarta en silencio (corrige el FAIL de `daw-arch-auditor`: AGENTS.md prohíbe
  un catch que solo loguea y sigue sin dejar rastro; acá si deja rastro vía `ILogger`, cumple con
  "nunca un catch vacío" porque no está vacío, y con la prohibición de "solo logueé y seguí" en el
  sentido de que el resultado sigue siendo observable — `Inconclusive` — no un éxito disfrazado).
- Mapeo a estado del mural (decisión que toma Block 4): `Clean` → el mural queda `Pending`
  (FR-08/FR-09: nada bloquea su publicación futura, pero tampoco hay auto-aprobación, la modera
  FEAT-001c); `Nsfw` → `Rejected` (FR-09); `Inconclusive` → `Pending`, igual que `Clean` (FR-10:
  "falla o no responde" nunca bloquea el flujo ni descarta el mural).

**Error handling**
- Ver arriba: toda excepción/timeout del modelo → logueada + `Inconclusive`, jamás una excepción sin
  loguear ni una que se propague al Handler.

**Required tests**
- [ ] Clasificador subyacente devuelve "no NSFW" → `ScanAsync` devuelve `Clean`.
- [ ] Clasificador subyacente devuelve "NSFW" → `ScanAsync` devuelve `Nsfw`.
- [ ] Clasificador subyacente lanza una excepción → `ScanAsync` devuelve `Inconclusive` Y se verifica
      que `ILogger` recibió una entrada `Warning` con la excepción (no un catch silencioso).
- [ ] Clasificador subyacente no completa dentro del timeout → `ScanAsync` devuelve `Inconclusive`
      sin colgar el test (verificable con un clasificador fake que espera indefinidamente).

**Completion criterion**
Los 4 tests anteriores pasan; `ScanAsync` nunca propaga una excepción a su llamador bajo ningún
escenario ejercitado.

**Nota de trazabilidad — dependencia adicional (WARN de `daw-arch-auditor`)**
Se agregó `PackageReference` directo a `Magick.NET-Q16-AnyCPU` 14.16.0 en
`Paretto.Infrastructure.csproj` para pinnear una dependencia transitiva de `NsfwSpy` 3.5.0
(`Magick.NET-Q16-AnyCPU` 11.1.2) afectada por NU1903 (CVEs High conocidos). Reverificado con
`dotnet list package --vulnerable --include-transitive` sin hallazgos tras el pin.

---

## Block 4 — API: crear mural

**Files**
- `backend/src/Paretto.Api/Features/Murals/Commands/CreateMuralCommand.cs` (new — incluye `CreateMuralCommandValidator`, `CreateMuralResponse`, `MuralPersistenceException`, `CreateMuralCommandHandler`)
- `backend/src/Paretto.Api/Features/Murals/Mappings/MuralMappingConfig.cs` (new — Mapster, mismo patrón que `AuthMappingConfig.cs`)
- `backend/src/Paretto.Api/Api/Controllers/MuralsController.cs` (new — acción `Create`; `GetById` la agrega Block 5)
- `backend/src/Paretto.Api/Program.cs` (modified — sin cambios de DI nuevos, ya cubiertos por Block 2/3; solo si hace falta configurar `RequestFormLimits` globalmente, si no se hace por atributo)

**Logic**
1. `CreateMuralCommand : IRequest<CreateMuralResponse>` con `IFormFile Photo`, `double Latitude`,
   `double Longitude`. Sin campo `UserId` — igual que `RegisterUserCommand` nunca acepta `Role` del
   cliente (mitigación de tampering, threat model).
2. `CreateMuralCommandValidator : AbstractValidator<CreateMuralCommand>`:
   - `Photo`: `NotNull()`; `Must(f => f.Length <= 10 * 1024 * 1024)` (NFR-01/FR-02); `MustAsync`
     validando la firma de bytes (magic numbers) contra JPEG (`FF D8 FF`), PNG
     (`89 50 4E 47 0D 0A 1A 0A`) y WebP (`RIFF....WEBP`) — **no alcanza con `ContentType`/extensión,
     ambos falsificables por el cliente** (threat model R3). La regla lee los primeros bytes vía
     `file.OpenReadStream()` y explícitamente hace `stream.Position = 0` antes de retornar (corrige
     el WARN de `daw-arch-auditor`: el Handler necesita un stream fresco después).
   - `Latitude`: `InclusiveBetween(-90, 90)`.
   - `Longitude`: `InclusiveBetween(-180, 180)`.
3. `MuralPersistenceException : AppException` — mensaje genérico "No se pudo guardar el mural.
   Intentá nuevamente.", `StatusCodes.Status500InternalServerError`. Se usa tanto si falla la subida
   a Storage como si falla el guardado en la base (FR-12 no exige distinguir el origen del fallo al
   usuario, solo detectarlo y no marcar el mural como guardado).
4. `CreateMuralCommandHandler`:
   - Lee `UserId` desde `IHttpContextAccessor.HttpContext.User` (`ClaimTypes.NameIdentifier`,
     `Guid.Parse`) — mismo patrón que `LogoutCommandHandler`, único precedente en el repo.
   - Lee el archivo UNA sola vez a un `byte[]` (`await request.Photo.OpenReadStream()` →
     `MemoryStream`), y a partir de ahí crea streams independientes para la subida y para el scan
     NSFW — evita cualquier ambigüedad de reposicionamiento entre ambos consumidores.
   - Genera `blobName = $"{Guid.NewGuid()}{ExtensionFor(request.Photo.ContentType)}"` — **siempre
     server-side, nunca a partir del nombre de archivo del cliente** (threat model R4).
   - `await _blobStorageService.UploadAsync(...)`. Si lanza → `MuralPersistenceException`.
   - `var scanResult = await _nsfwContentScanner.ScanAsync(...)` (nunca lanza, ver Block 3).
   - `Status = scanResult == NsfwScanResult.Nsfw ? MuralStatus.Rejected : MuralStatus.Pending`
     (FR-08/FR-09/FR-10).
   - Persiste el `Mural`; `catch (DbUpdateException)` → `MuralPersistenceException` (mismo patrón de
     `RegisterUserCommandHandler`, sin registrar el mural como guardado — FR-12/AC-10).
   - **Nota de diseño aceptada**: si la subida a Storage tiene éxito pero el guardado en base
     falla después, el blob queda huérfano (sin ninguna fila `Mural` que lo referencie). No se
     implementa una compensación/rollback del blob en este ticket — el contenedor es privado y el
     blob nunca queda expuesto ni enlazado desde ningún lado, así que el único costo es
     almacenamiento no utilizado. Aceptado explícitamente, no requiere aprobación de riesgo de
     seguridad (no es un riesgo de seguridad, es un desperdicio de storage).
   - Retorna `CreateMuralResponse { Id, Status }` vía `IMapper`.

**API contract**
- Method + path: `POST /api/murals`
- Request: `multipart/form-data` — `Photo` (file, requerido, ≤10MB, JPEG/PNG/WebP con firma de
  bytes válida), `Latitude` (double, requerido, -90..90), `Longitude` (double, requerido, -180..180)
- Response: `201 Created` — `{ id: guid, status: string }`
- Error codes: `401` (sin sesión — lo resuelve `SessionAuthenticationHandler` antes de llegar a la
  acción), `422` (validación FluentValidation: archivo inválido/oversized/coordenadas fuera de
  rango), `500` (`MuralPersistenceException`)
- Auth: `[Authorize]` (FR-07)

**Data model**
Ver Block 1 (`Mural`). Este bloque no modifica el modelo, lo consume.

**Input validation**
Ver `CreateMuralCommandValidator` arriba — tipo (magic bytes), tamaño (≤10MB), rango de coordenadas.

**Error handling**
| Error | Código | Manejo |
|---|---|---|
| Sin sesión | 401 | `SessionAuthenticationHandler` (ya existente, sin cambios) |
| Archivo ausente/inválido/oversized, coordenadas fuera de rango | 422 | `ValidationBehavior` (pipeline existente) traduce `FluentValidation.ValidationException` |
| Falla de subida a Storage o de guardado en DB | 500 | `MuralPersistenceException` vía `ExceptionHandlingMiddleware` (mismo mecanismo que `DuplicateAccountException`) |
| Scan NSFW falla/no responde | — (no es un error HTTP) | Absorbido en Block 3, el mural se crea igual como `Pending` (FR-10) |

**Required tests**
- [ ] Foto+coordenadas válidas, scan `Clean` → `201`, mural persistido con `Status = Pending` — AC-01, AC-03, AC-05, AC-09
- [ ] Archivo > 10MB → `422` con motivo — AC-02
- [ ] Archivo no-imagen renombrado `.jpg` (firma de bytes inválida) → `422` con motivo — AC-02, valida threat model R3
- [ ] Sin sesión → `401` — AC-06 (mitad backend)
- [ ] Scan NSFW devuelve `Nsfw` → mural persistido con `Status = Rejected`, `201` — AC-07
- [ ] Scan NSFW lanza/no responde (mock de `INsfwContentScanner` que devuelve `Inconclusive`) → mural persistido con `Status = Pending`, `201` — AC-08
- [ ] Falla simulada de `DbUpdateException` al guardar → `500`, ningún `Mural` persistido — AC-10
- [ ] Falla simulada de `IBlobStorageService.UploadAsync` (lanza) → `500`, ningún `Mural`
      persistido — AC-10, distingue el origen de la falla (Storage) del anterior (DB), ambos deben
      resultar en el mismo `MuralPersistenceException`/500
- [ ] Latitud/longitud fuera de rango → `422` — sad path FR-04/FR-05

**Completion criterion**
Los 9 tests anteriores pasan; `POST /api/murals` devuelve `201` con un `Mural` persistido cuyo
`Status` refleja el resultado del scan, y `422`/`401`/`500` exactamente en los sad paths listados.

---

## Block 5 — API: consultar un mural (sirve la foto vía URL firmada)

**Files**
- `backend/src/Paretto.Api/Features/Murals/Queries/GetMuralByIdQuery.cs` (new — incluye `MuralResponse`, `MuralAccessDeniedException`, `GetMuralByIdQueryHandler`)
- `backend/src/Paretto.Api/Api/Controllers/MuralsController.cs` (modified — agrega la acción `GetById`)
- `backend/src/Paretto.Api/Features/Murals/Mappings/MuralMappingConfig.cs` (modified — mapping `Mural → MuralResponse`, ignorando `PhotoUrl` del auto-map porque se calcula aparte)

**Logic**
- `GetMuralByIdQuery : IRequest<MuralResponse> { Guid Id }`.
- `MuralAccessDeniedException : AppException` — mensaje genérico **"Mural not found."**,
  `StatusCodes.Status404NotFound`. **La MISMA excepción y el MISMO mensaje se usan tanto si el
  mural no existe como si existe pero el solicitante no tiene acceso** — mitigación de enumeración
  (threat model R1), calcando el precedente ya establecido en este mismo repo con
  `DuplicateAccountException` (mensaje genérico para no revelar por cuál campo colisionó).
- `GetMuralByIdQueryHandler`:
  1. Busca el `Mural` por `Id`. Si no existe → `MuralAccessDeniedException`.
  2. Si `Status` es `Pending` o `Rejected`: lee `UserId` y `Role` del `ClaimsPrincipal` actual
     (`IHttpContextAccessor`, mismo patrón que Block 4/`LogoutCommandHandler`). Si
     `UserId != Mural.UserId` **Y** `Role != UserRole.Administrator` → `MuralAccessDeniedException`.
     **Esta comprobación aplica a la respuesta COMPLETA, no solo a `PhotoUrl`** — quien no tiene
     acceso no se entera ni de las coordenadas ni del estado del mural (FR-16 + espíritu de RF-013).
  3. Genera la URL SAS: `_blobStorageService.GenerateReadSasUrl(mural.PhotoBlobName, TimeSpan.FromMinutes(5))` (FR-15/AC-13, reutiliza Block 2).
  4. Mapea a `MuralResponse { Id, Status, PhotoUrl, Latitude, Longitude, CreatedAt }` vía `IMapper`,
     con `PhotoUrl` seteado manualmente después del map (no es una propiedad de `Mural`).

**API contract**
- Method + path: `GET /api/murals/{id}`
- Request: parámetro de ruta `id` (guid)
- Response: `200 OK` — `{ id, status, photoUrl, latitude, longitude, createdAt }`
- Error codes: `401` (sin sesión), `404` (no existe O no autorizado — mismo mensaje genérico)
- Auth: `[Authorize]` (cualquier usuario autenticado puede intentar; la autorización fina la aplica el Handler)

**Data model**
Ver Block 1. Sin cambios de esquema.

**Input validation**
`id` debe ser un GUID válido (lo garantiza el binding de ASP.NET Core sobre la ruta tipada `Guid`;
un valor no parseable como GUID nunca llega al Handler, ASP.NET Core responde `400` automáticamente
antes del pipeline de MediatR).

**Error handling**
| Error | Código | Manejo |
|---|---|---|
| Sin sesión | 401 | `SessionAuthenticationHandler` (existente) |
| Mural inexistente | 404 | `MuralAccessDeniedException`, mensaje genérico |
| Mural existe pero el solicitante no es dueño ni Admin (estado Pending/Rejected) | 404 | `MuralAccessDeniedException`, MISMO mensaje genérico que el caso anterior |

**Required tests**
- [ ] Dueño consulta su propio mural pendiente → `200` con `photoUrl` — AC-13, AC-14 (camino permitido)
- [ ] Administrador consulta el mural pendiente de otro usuario → `200` — AC-14 (camino permitido)
- [ ] Un tercer usuario autenticado (no dueño, no Admin) consulta un mural pendiente → `404` — AC-14 (camino denegado), valida threat model R1
- [ ] Mismo caso con un mural `Rejected` → `404` — AC-14
- [ ] Id inexistente → `404` con el MISMO mensaje genérico que el caso denegado (verifica anti-enumeración)
- [ ] Sin sesión → `401`

**Completion criterion**
Los 6 tests anteriores pasan; `GET /api/murals/{id}` nunca distingue, en su respuesta ni en su
mensaje de error, entre "no existe" y "existe pero no tenés acceso".

---

## Block 6 — Frontend: regenerar cliente API + servicio de murales

**Files**
- `frontend/src/app/core/api-client/api-client.generated.ts` (regenerated vía NSwag — **nunca editado a mano**)
- `frontend/src/app/features/murals/data/mural.service.ts` (new)
- `frontend/src/app/features/murals/data/mural.service.spec.ts` (new)

**Logic**
- Con el backend de Block 4/5 corriendo, correr la regeneración de NSwag (`nswag.json` ya
  configurado con `MultipleClientsFromPathSegments`, así que `MuralsController` produce
  `MuralsClient` automáticamente).
- `MuralService` (`providedIn: 'root'`) envuelve `MuralsClient.create()`/`.getById()`, mismo patrón
  que `AuthService`: nunca expone `MuralsClient`/`ApiException` a los componentes, traduce todo a
  `ApiError { status, message }` reutilizando la misma función `toApiError` (o una equivalente
  compartida, a decidir en CODE si conviene moverla a `core/`).

**API contract**
N/A (consume los contratos ya definidos en Block 4/5).

**Data model**
N/A.

**Input validation**
N/A (delegada al backend; este servicio no valida, solo transporta).

**Error handling**
Igual que `auth.service.ts`: todo error del cliente generado (`ApiException` o de red) se traduce a
`ApiError`, nunca se traga silenciosamente.

**Required tests**
- [ ] `create()` exitoso → devuelve la respuesta mapeada
- [ ] `create()` con error 422/500 → devuelve un `ApiError` tipado con el mensaje del backend
- [ ] `getById()` exitoso → devuelve la respuesta mapeada
- [ ] `getById()` con error 404 → devuelve un `ApiError` tipado

**Completion criterion**
Los 4 tests anteriores pasan; `mural.service.ts` compila contra el cliente regenerado sin `any`.

---

## Block 7 — Frontend: formulario de creación de mural

**Files**
- `frontend/src/app/features/murals/ui/create-mural-form.component.ts` (new)
- `frontend/src/app/features/murals/ui/create-mural-form.component.html` (new)
- `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts` (new)

**Logic**
- Standalone component `CreateMuralFormComponent`, selector `app-create-mural-form` (prefijo `app`
  de `angular.json`).
- Selector de foto (`<input type="file" accept="image/jpeg,image/png,image/webp">`): al elegir un
  archivo, valida `file.type` contra el allowlist y `file.size <= 10MB`; si no pasa, muestra un
  error inline y deshabilita "Guardar" (FR-01/FR-02/FR-03/AC-01/AC-02). **Nota explícita**: esto es
  solo feedback rápido de UX — `file.type`/`.size` los controla el cliente y son trivialmente
  falsificables; la autoridad real es la validación de firma de bytes del backend (Block 4).
- Geolocalización: al montar, `navigator.geolocation.getCurrentPosition({ enableHighAccuracy: true })`
  (NFR-02). Éxito → completa lat/lng automáticamente (AC-03). Denegación/no soportado → revela
  inputs numéricos de latitud/longitud manuales sin interrumpir el resto del formulario
  (FR-06/AC-04); ingreso manual válido habilita "Guardar" (FR-05/AC-05).
- Envío: llama a `muralService.create(...)`. Éxito → muestra el mensaje de confirmación ("tu mural
  quedó pendiente de revisión", FR-14/AC-12). Error → **NO resetea el formulario**: conserva el
  `File` seleccionado y las coordenadas (capturadas o ingresadas) en signals del componente, muestra
  el mensaje de error con una acción "Reintentar" que vuelve a llamar `create()` con los mismos
  datos, sin pedirlos de nuevo (FR-13/AC-11).
- No implementa su propio chequeo de sesión — la ruta protegida (Block 8) ya lo resuelve
  estructuralmente (FR-07).

**API contract**
N/A (consume `mural.service.ts`).

**Data model**
N/A.

**Input validation**
Ver "Logic" arriba — validación cliente de tipo/tamaño de archivo y de rango de coordenadas
(reutilizando los mismos límites que el backend, sin ser la fuente de verdad).

**Error handling**
| Error | Manejo |
|---|---|
| Archivo inválido (tipo/tamaño) client-side | Inline, "Guardar" deshabilitado, sin llamar al backend |
| `create()` devuelve `ApiError` (422/500) | Mensaje de error visible, formulario preserva foto+ubicación, botón "Reintentar" |
| Geolocalización denegada/no soportada | Fallback a inputs manuales, sin bloquear el resto del flujo |

**Required tests**
- [ ] Archivo oversized → error inline, "Guardar" deshabilitado — AC-02
- [ ] Archivo no-imagen → error inline, "Guardar" deshabilitado — AC-01 (camino inverso)
- [ ] Geolocalización exitosa → lat/lng se completan solos — AC-03
- [ ] Geolocalización denegada → aparecen inputs manuales, formulario sigue usable — AC-04
- [ ] Ingreso manual válido → "Guardar" habilitado — AC-05
- [ ] Envío exitoso → mensaje de confirmación visible — AC-12
- [ ] Envío fallido → foto y ubicación se conservan, "Reintentar" vuelve a enviar sin pedir datos de nuevo — AC-11

**Completion criterion**
Los 7 tests anteriores pasan; el componente renderiza standalone sin dependencias de un módulo padre.

---

## Block 8 — Frontend: routing protegido

**Files**
- `frontend/src/app/app.routes.ts` (modified — nueva entrada de ruta)
- `frontend/src/app/app.routes.spec.ts` (modified — test de la nueva ruta)

**Logic**
- Agrega `{ path: 'murals/new', canActivate: [authGuard], loadComponent: () => import('./features/murals/ui/create-mural-form.component').then(m => m.CreateMuralFormComponent) }`
  al array `routes`, calcando el lazy-loading ya usado para `login`/`register` y reutilizando el
  `authGuard` existente (su comentario ya anticipaba esta reutilización).

**API contract**
N/A.

**Data model**
N/A.

**Input validation**
N/A.

**Error handling**
Usuario sin sesión que navega a `/murals/new` → `authGuard` redirige a `/login` (comportamiento ya
existente y testeado para las otras rutas protegidas).

**Required tests**
- [ ] Navegar a `/murals/new` sin sesión redirige a `/login` — AC-06 (mitad frontend), extiende
      `app.routes.spec.ts` con el mismo patrón usado para las rutas actuales.

**Completion criterion**
El test anterior pasa junto con los tests de ruta ya existentes (sin romperlos).

---

## Final verification

- Los 16 FR y las 14 AC del PRD están cubiertos por al menos un bloque (ver tabla de Coverage).
- Las 3 NFR tienen una estrategia documentada (ver tabla de Coverage).
- Ningún mural en estado `Pending`/`Rejected` es accesible por nadie que no sea su dueño o un
  Administrador (Block 5, validado por threat model R1).
- Ningún archivo subido se acepta solo por extensión/`Content-Type` — la firma de bytes es
  obligatoria (Block 4, threat model R3).
- El contenedor de Azure Storage nunca tiene acceso público anónimo (Block 2, NFR-03).
- Ninguna credencial de Storage queda hardcodeada en `appsettings.json` de producción (Block 2,
  threat model R5).
- El scan NSFW nunca bloquea ni cuelga el flujo de creación, y nunca falla en silencio sin loguear
  (Block 3, corrige el FAIL de `daw-arch-auditor`).
- `docs/daw/security/threat-FEAT-001b.md` queda como evidencia de las 8 mitigaciones aplicadas.
