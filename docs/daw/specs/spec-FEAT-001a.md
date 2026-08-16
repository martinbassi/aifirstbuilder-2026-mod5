# Spec FEAT-001a: Autenticación básica

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| PRD | docs/daw/prd/prd-FEAT-001a.md |
| Tier | FEATURE |
| Date | 2026-08-15 |
| Spec loops | 0 |

## Summary

Primer ticket de código del repo: bootstrapea el workspace de backend (.NET 10, 3 proyectos:
`Paretto.Domain`/`Paretto.Infrastructure`/`Paretto.Api`, ver `docs/adr/adr-001-...md`) y de frontend
(Angular 21), y sobre esa base construye registro, login y logout con sesión **server-side** (tabla
`Sessions`, token opaco hasheado — no JWT, decisión de PLAN por preocupación de escalabilidad vs.
revocación inmediata). Incorpora las 6 mitigaciones del threat model
(`docs/daw/security/threat-FEAT-001a.md`): sin campo `role` aceptado del cliente en el registro,
tokens de sesión persistidos como hash, rate limiting básico en `/login` y `/register`, logging
explícito de eventos de auth, validación de expiración en cada request, y CSP headers.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 5 |
| FR-02 | Block 5 |
| FR-03 | Block 5 |
| FR-04 | Block 6 |
| FR-05 | Block 6 |
| FR-06 | Block 7 |
| FR-07 | Block 3 |
| NFR-01 | Strategy: `IPasswordHasher` (Block 4) — hashing vía `PasswordHasher<User>`, nunca texto plano |
| NFR-02 | Strategy: `app.UseHttpsRedirection()` + `RequireHttps` en `Program.cs` (Block 1); infraestructura de despliegue fuera del alcance de código |
| NFR-03 | Strategy: `Session.ExpiresAt = now + 7 días` al crear la sesión (Block 6), validado en cada request por `SessionAuthenticationHandler` (Block 6) |

## Dependencies between blocks

```
Block 1 (bootstrap backend) ─┬─→ Block 3 (dominio/persistencia)
                              ├─→ Block 4 (seguridad)
                              ├─→ Block 5 (registro)
                              ├─→ Block 6 (login + auth scheme)
                              └─→ Block 7 (logout)
Block 2 (bootstrap frontend) ─────────────────────────────→ Block 8 (NSwag + UI)
Block 3 ──────────────────────→ Block 5, Block 6
Block 4 ──────────────────────→ Block 5, Block 6, Block 7
Block 5 ──────────────────────→ Block 6 (mismo patrón de mensaje genérico)
Block 6 ──────────────────────→ Block 7, Block 8
Block 7 ──────────────────────→ Block 8
```

Orden de ejecución: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 (2 puede correr en paralelo con 1/3/4 si hace
falta, pero no tiene sentido implementarlo antes de tener algo que consumir).

## Block 1 — Bootstrap del backend (.NET 10, 3 proyectos)

**Files**
- `backend/Paretto.sln` (new)
- `backend/src/Paretto.Domain/Paretto.Domain.csproj` (new) — class library, `net10.0`, sin
  referencias a paquetes de infraestructura.
- `backend/src/Paretto.Infrastructure/Paretto.Infrastructure.csproj` (new) — class library,
  referencia `Paretto.Domain`.
- `backend/src/Paretto.Api/Paretto.Api.csproj` (new) — ASP.NET Core Web API, referencia
  `Paretto.Domain` y `Paretto.Infrastructure`.
- `backend/src/Paretto.Api/Program.cs` (new) — wiring: `AddControllers`, `AddMediatR`
  (`Paretto.Api` assembly), `AddValidatorsFromAssembly` (FluentValidation), `AddMapster`,
  `AddDbContext<AppDbContext>` (SQL Server, connection string de `appsettings`),
  `AddAuthentication`/`AddAuthorization` (esquema completo en Block 6), `AddRateLimiter`
  (mitigación R3), `UseHttpsRedirection`, `UseExceptionHandler` (middleware de Block 1),
  `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI`.
- `backend/src/Paretto.Api/appsettings.json` (new) — `ConnectionStrings:DefaultConnection` (SQL
  Server localhost), `Session:ExpiryDays: 7`.
- `backend/src/Paretto.Api/appsettings.Development.json` (new) — overrides de desarrollo local.
- `backend/src/Paretto.Api/Common/Behaviors/LoggingBehavior.cs` (new) — `IPipelineBehavior<TRequest,
  TResponse>`: loguea request/response de cada Command/Query (nombre, duración, resultado), nunca el
  contenido de campos sensibles (contraseñas, tokens).
- `backend/src/Paretto.Api/Common/Middleware/ExceptionHandlingMiddleware.cs` (new) — traduce
  excepciones no capturadas y fallos de `Result<T>` a `ProblemDetails`. **Decisión documentada:**
  `AGENTS.md` describe un único `IPipelineBehavior` centralizando logging y traducción a
  `ProblemDetails`; acá se separan en dos piezas porque un `IPipelineBehavior` de MediatR no tiene
  acceso a `HttpContext`/`HttpResponse` — `LoggingBehavior` cubre el logging (su responsabilidad
  natural), y este middleware ASP.NET Core cubre la traducción HTTP. Es la interpretación técnica
  correcta de la misma regla, no un incumplimiento.
- `backend/tests/Paretto.Api.Tests/Paretto.Api.Tests.csproj` (new) — proyecto xUnit, referencia
  `Paretto.Api` + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`).
- `backend/src/Paretto.Api/nswag.json` (new — reubicado desde `frontend/nswag.json` durante la
  revisión de Block 2, ver nota ahí) — configuración de NSwag apuntando a
  `https://localhost:7126/swagger/v1/swagger.json`, output relativo
  `../../../frontend/src/app/core/api-client/api-client.generated.ts`, cliente Angular/TypeScript.
  Vive junto al proyecto que expone el OpenAPI (el insumo), no junto al que solo consume el
  resultado generado.

**Logic**

Estructura de 3 proyectos con dirección de dependencias `Api → Infrastructure → Domain` impuesta por
referencias de proyecto (ver `docs/adr/adr-001-estructura-multiproyecto-dotnet.md`). `Program.cs`
registra todo el pipeline pero sin lógica de negocio propia todavía — es el punto de composición.

**Paquetes NuGet nuevos — justificación explícita (regla de `AGENTS.md`: "no nuevos paquetes sin
justificación")**

| Paquete | Justificación |
|---|---|
| `MediatR` 12.5.0 | Ya declarado en el stack base de `AGENTS.md` — no requiere justificación. |
| `FluentValidation` (+ `FluentValidation.DependencyInjectionExtensions`) | Stack base. |
| `Mapster` (+ `Mapster.DependencyInjection`) | Stack base. |
| `Microsoft.EntityFrameworkCore.SqlServer` + `.Design` | Stack base (EF Core). |
| `Microsoft.AspNetCore.Identity` | **Nuevo.** Se usa exclusivamente `PasswordHasher<User>` — biblioteca oficial de Microsoft para hashing de contraseñas (cumple NFR-01), sin traer un sistema de Identity completo ni una dependencia de terceros. |
| `Microsoft.AspNetCore.Authentication.Abstractions` | **Nuevo.** Necesario para implementar `AuthenticationHandler<TOptions>` custom (decisión de PLAN: sesión server-side, no JWT). Es la abstracción base de autenticación de ASP.NET Core, no una librería de terceros. |
| `Swashbuckle.AspNetCore` | **Nuevo.** Genera el documento OpenAPI (`swagger.json`) que el frontend consume vía NSwag (ya parte del stack base) para generar su cliente HTTP. Es un prerequisito técnico de NSwag, no una elección de librería nueva. |
| `Microsoft.AspNetCore.RateLimiting` | **Nuevo, pero nativo de .NET 10** (namespace `Microsoft.AspNetCore.RateLimiting`, no requiere paquete NuGet externo desde .NET 7). Mitigación R3 del threat model (`docs/daw/security/threat-FEAT-001a.md`): sin esto, `/login` y `/register` quedan sin límite de intentos mientras RF-052 no exista como ticket propio. |

**Error handling**
- Cualquier excepción no capturada por un handler → `ExceptionHandlingMiddleware` la traduce a
  `ProblemDetails` (500, sin exponer detalles internos en producción; `Development` sí incluye
  detalle para debugging).

> La resiliencia de conexión a SQL Server al arrancar (fail-fast vs. reintentos) es una decisión de
> infraestructura/despliegue, no de código de este bloque — no se documenta acá como error manejado
> porque este spec no la implementa ni la testea; queda para cuando exista un ticket de
> infraestructura/despliegue.

**Required tests**
- [ ] `HealthCheckTests`: `GET /swagger/v1/swagger.json` responde 200 — valida AC-01 a AC-06 de
  forma indirecta (el host arranca con toda la configuración cargada, prerequisito de todos los
  demás tests).
- [ ] `ExceptionHandlingMiddlewareTests`: un endpoint de prueba que lanza una excepción no
  capturada responde `500` con un body `ProblemDetails` — valida F-SPEC-16 sobre el error
  documentado arriba.

**Completion criterion**
`dotnet build backend/Paretto.sln` compila sin errores ni warnings de referencia circular; los 2
tests pasan.

---

## Block 2 — Bootstrap del frontend (Angular 21)

**Files**
- `frontend/angular.json`, `frontend/package.json`, `frontend/tsconfig.json`,
  `frontend/tsconfig.app.json` (new) — generados vía `ng new` con el preset del proyecto (standalone
  components, sin módulos NgModule).
- `frontend/src/main.ts` (new) — `bootstrapApplication(AppComponent, appConfig)`.
- `frontend/src/app/app.config.ts` (new) — `provideHttpClient(withInterceptors([authInterceptor]))`
  (el interceptor se implementa recién en Block 8; acá se deja el arreglo vacío o el import
  preparado), `provideRouter(routes)`, providers de ng-zorro (`provideNzI18n`, íconos).
- `frontend/src/app/app.routes.ts` (new) — array de rutas vacío por ahora (Block 8 agrega
  `/login`, `/register`).
- `frontend/src/app/core/api-client/.gitkeep` (new) — reserva la carpeta donde NSwag va a generar
  el cliente en Block 8.

> **Corrección aplicada durante CODE (no en el archivo original de este bloque):** la configuración
> de NSwag (`nswag.json`) se movió a `backend/src/Paretto.Api/nswag.json` — ver Block 1. Se
> detectó durante la revisión del Bloque 2 que un archivo de configuración de build cuyo insumo es
> el propio backend (genera el cliente a partir de su OpenAPI) pertenece conceptualmente a ese
> proyecto, no al frontend que solo consume el resultado. No hay borde `CODE→PLAN` en el grafo de
> transiciones para un re-paso formal por PLAN; se trató como corrección de implementación
> (ubicación de un archivo de configuración, sin cambio de comportamiento) y se documentó acá en
> vez de silenciarla.

**Logic**
Workspace mínimo que compila y sirve. Sin rutas de negocio todavía — eso llega en Block 8, una vez
que el backend expone los endpoints de auth que NSwag necesita para generar el cliente.

**Error handling**
N/A — no hay lógica de negocio en este bloque.

**Required tests**
- [ ] Test de arranque del `AppComponent` raíz (`TestBed.createComponent` o equivalente del preset
  de `ng new`) — smoke test de que el workspace compila y renderiza.

**Completion criterion**
`ng build` compila sin errores; `ng serve` levanta la app (pantalla base, sin funcionalidad de auth
todavía).

---

## Block 3 — Dominio y persistencia de Auth

**Files**
- `backend/src/Paretto.Domain/Entities/User.cs` (new)
- `backend/src/Paretto.Domain/Entities/Session.cs` (new)
- `backend/src/Paretto.Domain/Enums/UserRole.cs` (new)
- `backend/src/Paretto.Infrastructure/Data/AppDbContext.cs` (new)
- `backend/src/Paretto.Infrastructure/Data/AppDbContextFactory.cs` (new) — `IDesignTimeDbContextFactory<AppDbContext>`
  usado por la CLI de `dotnet ef` (`migrations add`/`database update`) para construir `AppDbContext`
  sin pasar por el pipeline de DI de `Paretto.Api`; lee la connection string real de la variable de
  entorno `ConnectionStrings__DefaultConnection` cuando está presente, con un placeholder sin
  credenciales como fallback (suficiente para `migrations add`, insuficiente para `database update`
  real).
- `backend/src/Paretto.Infrastructure/Data/Migrations/{timestamp}_InitialCreate.cs` (new, generado
  por `dotnet ef migrations add`)

**Data model**

`User`:
| Field | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `Username` | `string` | NOT NULL, UNIQUE, max 50 |
| `Email` | `string` | NOT NULL, UNIQUE, max 256 |
| `PasswordHash` | `string` | NOT NULL |
| `Role` | `UserRole` (enum: `Standard`=0, `Administrator`=1) | NOT NULL, default `Standard` |
| `CreatedAt` | `DateTime` | NOT NULL, default `UtcNow` |

> Nota de terminología: el PRD (`docs/daw/prd/prd-FEAT-001a.md`, FR-07) nombra el rol por defecto
> "Colaborador/Explorador" en la jerga de producto. `Standard` es la traducción de código acordada
> en PLAN — el código va en inglés por convención de `AGENTS.md`, el glosario de producto sigue
> siendo español.

`Session`:
| Field | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PK, default `Guid.NewGuid()` |
| `TokenHash` | `string` | NOT NULL, UNIQUE (SHA-256 en hex del token opaco — el token en claro NUNCA se persiste, mitigación R2 del threat model) |
| `UserId` | `Guid` | NOT NULL, FK → `User.Id`, `ON DELETE CASCADE` |
| `ExpiresAt` | `DateTime` | NOT NULL |
| `CreatedAt` | `DateTime` | NOT NULL, default `UtcNow` |

Índices: único en `User.Username`, único en `User.Email`, único en `Session.TokenHash` (el índice
es también lo que hace barato el lookup de Block 6 — comparación por igualdad sobre una columna
indexada).

**Error handling**
- Violación de unicidad de `Username`/`Email` a nivel de DB → capturada por `AppDbContext.SaveChanges`
  como `DbUpdateException`, traducida por el Handler de Block 5 al mismo mensaje genérico de FR-02
  (defensa en profundidad: la validación de FluentValidation ya debería atajarlo antes, pero una
  carrera entre dos requests concurrentes solo la ataja la constraint de DB).

**Required tests**
- [ ] La migración inicial aplica sin error contra una instancia de SQL Server 2025 de test.
- [ ] Insertar dos `User` con el mismo `Username` (o el mismo `Email`) falla por la constraint única
  de DB — prueba que la unicidad no depende solo de la validación de aplicación.

**Completion criterion**
`dotnet ef database update` aplica la migración sin error; los dos tests de constraint pasan.

---

## Block 4 — Servicios de seguridad

**Files**
- `backend/src/Paretto.Infrastructure/Security/IPasswordHasher.cs` (new)
- `backend/src/Paretto.Infrastructure/Security/PasswordHasher.cs` (new)
- `backend/src/Paretto.Infrastructure/Security/ISessionTokenGenerator.cs` (new)
- `backend/src/Paretto.Infrastructure/Security/SessionTokenGenerator.cs` (new)

**Logic**
- `IPasswordHasher`: `string Hash(string password)`, `bool Verify(string password, string hash)` —
  wrapea `Microsoft.AspNetCore.Identity.PasswordHasher<User>` (PBKDF2 con salt aleatorio por
  contraseña, comportamiento por defecto de la librería).
- `ISessionTokenGenerator`: `(string RawToken, string TokenHash) Generate()` — `RawToken` es 256
  bits de `RandomNumberGenerator.GetBytes` codificados en Base64Url (lo que se devuelve al cliente en
  Block 6); `TokenHash` es el SHA-256 hex del `RawToken` (lo único que se persiste en `Sessions`,
  mitigación R2).

**Input validation**
N/A — servicios internos, no reciben input de usuario directamente.

**Error handling**
N/A — operaciones criptográficas determinísticas sin condición de fallo esperable en uso normal.

**Required tests**
- [ ] `Hash` de la misma contraseña dos veces produce hashes distintos (salt aleatorio).
- [ ] `Verify` acepta el hash correcto y rechaza uno incorrecto.
- [ ] `Generate` produce 1000 tokens sin colisiones.
- [ ] `TokenHash` es determinístico a partir del mismo `RawToken`, pero no hay forma de recuperar el
  `RawToken` a partir del `TokenHash` (prueba conceptual: `TokenHash` no aparece en ningún log ni
  respuesta junto al `RawToken`).

**Completion criterion**
Los 4 tests pasan.

---

## Block 5 — Registro

**Files**
- `backend/src/Paretto.Api/Features/Auth/Commands/RegisterUserCommand.cs` (new) — Command,
  Validator, Handler, Response, en el mismo archivo o carpeta según convención del proyecto.
- `backend/src/Paretto.Api/Features/Auth/Mappings/AuthMappingConfig.cs` (new) — Mapster:
  `User` → `RegisterUserResponse`.
- `backend/src/Paretto.Api/Api/Controllers/AuthController.cs` (new) — acción `Register`.

**API contract**
- Method + path: `POST /api/auth/register`
- Request: `{ username: string, password: string, email: string }`
- Response 201: `{ id: guid, username: string }`
- Error codes:
  - `400` `ProblemDetails` — email o username ya en uso (mensaje genérico único, FR-02/AC-02:
    exactamente el mismo texto sin importar cuál campo está duplicado).
  - `422` `ProblemDetails` — falla de validación FluentValidation (contraseña inválida, campos
    faltantes).
- Auth: anónimo (no requiere sesión — es el endpoint que crea la cuenta).

**Input validation**
- `Username`: requerido, max 50 caracteres.
- `Email`: requerido, formato de email válido, max 256 caracteres.
- `Password`: requerido, 8 a 128 caracteres, debe incluir al menos una letra y un número (FR-03).

**Logic**
`RegisterUserCommandHandler`: verifica unicidad de `Username`/`Email` contra `AppDbContext`; si
cualquiera de los dos ya existe, devuelve el error genérico de FR-02 (texto idéntico en ambos casos,
sin distinguir cuál campo falló). Si es válido: hashea la contraseña con `IPasswordHasher`, asigna
`Role = UserRole.Standard` **hardcodeado en el servidor** (el `Command` no tiene un campo `Role` —
mitigación R1 del threat model: ningún valor que llegue en el body del request puede influir en el
rol asignado), persiste el `User`, devuelve `RegisterUserResponse` (`Id`, `Username` — nunca
`PasswordHash`).

**Error handling**
- Email/username duplicado → `400` con el mensaje genérico (ver API contract).
- Contraseña inválida → `422` con el detalle de qué regla de FluentValidation falló (acá sí se
  puede ser específico: no es un vector de enumeración de cuentas, es feedback sobre la contraseña
  que el propio usuario está eligiendo).
- Fallo de guardado en DB (constraint de unicidad, carrera entre requests concurrentes) →
  `DbUpdateException` capturada y traducida al mismo `400` genérico de FR-02.

**Required tests**
- [ ] Registro exitoso crea la cuenta con `Role.Standard` — valida AC-01.
- [ ] Email duplicado y username duplicado devuelven el **mismo texto de mensaje**, verificado
  literalmente (no solo el mismo código HTTP) — valida AC-02 y el hallazgo del threat model.
- [ ] Contraseña con menos de 8 caracteres es rechazada — valida AC-03 (sad path).
- [ ] Contraseña con más de 128 caracteres es rechazada — valida AC-03 (sad path).
- [ ] Contraseña sin números o sin letras es rechazada — valida AC-03 (sad path).
- [ ] Un `Command` construido con un JSON de request que incluye `"role": "Administrator"` no
  produce una cuenta con ese rol — test específico de seguridad para el riesgo R1 del threat model.
- [ ] Con el repositorio simulando una `DbUpdateException` al guardar (carrera entre dos requests
  concurrentes con el mismo email/username), el Handler devuelve el mismo `400` genérico de FR-02 —
  valida el tercer error documentado arriba (F-SPEC-16).

**Completion criterion**
Los 6 tests pasan; `POST /api/auth/register` con datos válidos devuelve 201.

---

## Block 6 — Login + esquema de autenticación por sesión

**Files**
- `backend/src/Paretto.Api/Features/Auth/Commands/LoginCommand.cs` (new)
- `backend/src/Paretto.Infrastructure/Auth/SessionAuthenticationSchemeOptions.cs` (new)
- `backend/src/Paretto.Infrastructure/Auth/SessionAuthenticationHandler.cs` (new)
- `backend/src/Paretto.Api/Program.cs` (modified) — `AddAuthentication(SessionAuthScheme)
  .AddScheme<SessionAuthenticationSchemeOptions, SessionAuthenticationHandler>(...)`,
  `AddAuthorization()`.
- `backend/src/Paretto.Api/Api/Controllers/AuthController.cs` (modified) — acción `Login`.

**API contract**
- Method + path: `POST /api/auth/login`
- Request: `{ username: string, password: string }`
- Response 200: `{ token: string, expiresAt: datetime }`
- Error codes: `401` `ProblemDetails` — credenciales inválidas (mensaje genérico único, FR-05/AC-05:
  mismo texto sin importar si falló el usuario o la contraseña).
- Auth: anónimo.

**Input validation**
- `Username`, `Password`: ambos requeridos (no vacíos).

**Logic**
`LoginCommandHandler`: busca `User` por `Username`; si no existe, o si `IPasswordHasher.Verify`
falla, devuelve el mismo error genérico de credenciales inválidas (sin distinguir cuál de los dos
campos causó el fallo). Si es válido: `ISessionTokenGenerator.Generate()`, persiste una fila en
`Sessions` con `TokenHash`, `UserId`, `ExpiresAt = UtcNow.AddDays(7)` (NFR-03), devuelve `{ token:
RawToken, expiresAt }` — el `RawToken` es la única vez que existe en texto plano fuera de la memoria
del request.

`SessionAuthenticationHandler : AuthenticationHandler<SessionAuthenticationSchemeOptions>`: lee el
header `Authorization: Bearer {token}`; calcula el SHA-256 del token recibido; busca `Session` por
`TokenHash`. **Si no existe la fila, o si `Session.ExpiresAt < UtcNow` → `AuthenticateResult.Fail`**
(la validación de expiración es explícita y ocurre en cada request, no solo la existencia de la
fila). Si es válida: arma el `ClaimsPrincipal` con `ClaimTypes.NameIdentifier = Session.UserId` y
`ClaimTypes.Role = Session.User.Role`, leídos siempre de la fila de la base — nunca de un claim que
llegue en el propio request (mitigación de elevación de privilegios del threat model).

**Error handling**
- Usuario inexistente o contraseña incorrecta → `401` con el mensaje genérico.
- Token ausente, o inexistente en `Sessions` (incluye cualquier token malformado — un token que no
  matchea ningún `TokenHash` se trata igual que uno inexistente, no hay una rama de código
  distinta), o con `ExpiresAt` vencido → el esquema de auth falla la autenticación; el endpoint
  protegido devuelve `401` (comportamiento estándar de ASP.NET Core cuando `AuthenticateResult.Fail`).

**Required tests**
- [ ] Login exitoso devuelve un token y `expiresAt` ≈ `UtcNow + 7 días` — valida AC-04.
- [ ] Usuario inexistente devuelve el mismo mensaje/código que contraseña incorrecta — valida AC-05
  (verificado literalmente, mismo texto).
- [ ] Un endpoint `[Authorize]` de prueba acepta un token válido y no expirado.
- [ ] Un endpoint `[Authorize]` de prueba rechaza un token que no existe en `Sessions`.
- [ ] Un endpoint `[Authorize]` de prueba rechaza un token cuya `Session.ExpiresAt` ya pasó — test
  específico de la validación de expiración (no alcanza con "el token no existe").
- [ ] Un endpoint `[Authorize]` de prueba rechaza una request sin header `Authorization` — valida el
  caso "token ausente" documentado arriba (F-SPEC-16).

**Completion criterion**
Los 5 tests pasan; un token emitido por `/login` autentica correctamente sobre un endpoint protegido
de prueba.

---

## Block 7 — Logout

**Files**
- `backend/src/Paretto.Api/Features/Auth/Commands/LogoutCommand.cs` (new)
- `backend/src/Paretto.Api/Api/Controllers/AuthController.cs` (modified) — acción `Logout`.

**API contract**
- Method + path: `POST /api/auth/logout`
- Request: sin body (usa el token del header `Authorization` de la request actual)
- Response: `204 No Content`
- Error codes: `401` si no hay una sesión válida (mismo comportamiento que cualquier endpoint
  `[Authorize]`).
- Auth: requiere sesión válida (`[Authorize]`).

**Logic**
`LogoutCommandHandler`: obtiene el token crudo del header `Authorization` de la request actual
(disponible vía `IHttpContextAccessor` o equivalente), calcula su `TokenHash`, borra la fila de
`Sessions` con ese hash. Invalidación real e inmediata — no depende de que el token expire solo.

**Error handling**
- Sin sesión válida → `401` (el pipeline de autenticación ya lo rechaza antes de llegar al Handler).

**Required tests**
- [ ] Logout borra la fila correspondiente de `Sessions`.
- [ ] Una request posterior a un endpoint `[Authorize]` de prueba, usando el mismo token, es
  rechazada con `401` — prueba la invalidación real (AC-06), no solo que el endpoint de logout
  devolvió `204`.
- [ ] `POST /api/auth/logout` sin un token válido devuelve `401` — valida el error documentado
  arriba (F-SPEC-16).

**Completion criterion**
Los 3 tests pasan.

---

## Block 8 — Cliente NSwag + feature `auth` en Angular

**Files**
- `frontend/src/app/core/api-client/api-client.generated.ts` (generated — NSwag CLI usando
  `backend/src/Paretto.Api/nswag.json` (ver Block 1) contra el `swagger.json` de `Paretto.Api`,
  comando documentado en `backend/src/Paretto.Api/README` o script equivalente. **Nunca editado a
  mano.**)
- `frontend/src/app/features/auth/data/auth.service.ts` (new)
- `frontend/src/app/features/auth/state/session.store.ts` (new)
- `frontend/src/app/core/interceptors/auth.interceptor.ts` (new)
- `frontend/src/app/features/auth/ui/register-form.component.ts` (new)
- `frontend/src/app/features/auth/ui/login-form.component.ts` (new)
- `frontend/src/app/app.routes.ts` (modified) — agrega `/login`, `/register`, y `authGuard`
  (`CanActivateFn`) reutilizable por sub-tickets futuros (FEAT-001b/c).
- `frontend/src/index.html` (modified) — header `Content-Security-Policy` (mitigación R5 del threat
  model, riesgo aceptado con mitigación en profundidad).

**Logic**
- `auth.service.ts`: envuelve `api-client.generated.ts` (`register()`, `login()`, `logout()`);
  traduce la respuesta a los signals de `session.store.ts`; expone errores tipados (`ApiError`) sin
  swallowearlos.
- `session.store.ts`: signals `token` (persistido en `sessionStorage` — sobrevive un refresh de
  página, se pierde al cerrar la pestaña; decisión documentada como parte del riesgo aceptado R5),
  `user`, y `isAuthenticated` (`computed`).
- `auth.interceptor.ts`: si `session.store` tiene `token`, adjunta `Authorization: Bearer {token}`
  a cada request saliente hacia la API propia; en una respuesta `401`, limpia la sesión y redirige a
  `/login`.
- `register-form.component.ts` / `login-form.component.ts`: standalone, ng-zorro; llaman
  exclusivamente a `auth.service.ts` (nunca `HttpClient` ni el cliente generado directo); muestran el
  mensaje de error genérico del backend **tal cual** (sin agregar distinción propia entre "email" y
  "usuario" del lado del cliente — repetiría la fuga de FR-02 en el frontend).
- `authGuard`: `CanActivateFn` que redirige a `/login` si `session.store().isAuthenticated()` es
  `false`.

**Input validation**
- Los mismos límites del backend (Username max 50, Email formato válido, Password 8-128 con letra y
  número) se validan también client-side en los formularios, como primera capa de feedback — la
  validación server-side sigue siendo la autoridad final.

**Error handling**
- Error de red / API caída → `auth.service.ts` propaga un `ApiError` genérico que los componentes
  muestran sin detalle técnico.
- `400`/`401`/`422` del backend → se muestran literalmente los mensajes ya genéricos que devuelve la
  API.

**Required tests**
- [ ] `auth.service.ts`: `register()`/`login()` exitosos actualizan `session.store` correctamente.
- [ ] `auth.service.ts`: un error del cliente generado se propaga como `ApiError`, no se swallowea.
- [ ] `register-form`/`login-form`: el mensaje de error mostrado es exactamente el que devuelve el
  backend, sin texto adicional que distinga campos.
- [ ] `auth.interceptor.ts`: adjunta el header `Authorization` cuando hay token, no lo adjunta
  cuando no hay sesión.
- [ ] `authGuard`: redirige a `/login` sin sesión, permite el acceso con sesión.

**Completion criterion**
Los 5 tests pasan; un flujo manual de registro → login → acceso a una ruta protegida de prueba →
logout → la misma ruta protegida redirige a `/login`, funciona de punta a punta contra el backend
local.

---

## Final verification

- Los 6 requisitos funcionales del PRD (FR-01 a FR-07, con FR-02/FR-03 fusionados) tienen al menos
  un bloque que los cubre (ver tabla de Coverage) y al menos un test que los valida.
- Las 6 mitigaciones del threat model (`docs/daw/security/threat-FEAT-001a.md`) están incorporadas:
  R1 (Block 5), R2 (Block 3/4/6), R3 (Block 1), R5 (Block 8, riesgo aceptado + CSP), R6 (Block 1),
  validación de expiración (Block 6).
- `dotnet build` + `dotnet test` sobre `backend/Paretto.sln` pasan en verde.
- `ng build` + `npm test` sobre `frontend/` pasan en verde.
- Un flujo manual de extremo a extremo (registro → login → request autenticada → logout → request
  rechazada) funciona contra ambos workspaces corriendo localmente.
