# Threat Model — FEAT-001b: Crear mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| Date | 2026-08-16 |
| PRD | `docs/daw/prd/prd-FEAT-001b.md` |
| Design reviewed | Plan de 8 bloques (backend: Domain/Mural, Storage, Moderation/NsfwSpy, `POST /api/murals`, `GET /api/murals/{id}`; frontend: mural.service.ts, create-mural-form, routing) |

## Componentes nuevos analizados

1. `POST /api/murals` (`CreateMuralCommand` + `CreateMuralCommandHandler` + `MuralsController.Create`)
2. `GET /api/murals/{id}` (`GetMuralByIdQuery` + `GetMuralByIdQueryHandler` + `MuralsController.GetById`)
3. `IBlobStorageService`/`AzureBlobStorageService` (Azure Storage — subida + SAS)
4. `INsfwContentScanner` + implementación con NsfwSpy (modelo ML in-process)
5. Validación de archivo subido (transversal a 1 y 3)

## Trust boundaries declaradas (F-TM-02)

| Boundary | Entre | Notas |
|---|---|---|
| B1 | Browser (no confiable) → API | Ya cubierta por el esquema de sesión de FEAT-001a (token opaco, `SessionAuthenticationHandler`); este ticket reutiliza el mecanismo, no lo modifica. |
| B2 | API → Azure Blob Storage | Servicio externo (o Azurite en dev); cruce protegido por connection string/SAS, nunca por confianza implícita. |
| B3 | API → SQL Server | Vía `AppDbContext`/EF Core parametrizado — mismo patrón que Auth, sin SQL crudo. |
| B4 | API → NsfwSpy | In-process, pero es el punto donde bytes de un archivo NO confiable (subido por el usuario) entran a la lógica de parseo/inferencia de una librería de terceros. |

## Análisis STRIDE por componente (F-TM-01)

### 1. `POST /api/murals`

| STRIDE | Evaluación |
|---|---|
| Spoofing | Reutiliza el esquema de sesión ya auditado en FEAT-001a. Sin cambios. |
| Tampering | El `UserId` del mural NUNCA se lee del cuerpo de la petición — se deriva server-side vía `IHttpContextAccessor`/`ClaimTypes.NameIdentifier` (mismo patrón que `LogoutCommandHandler`), igual que FEAT-001a hizo con `Role` en `RegisterUserCommand` (mitigación R1 de ese threat model). |
| Repudiation | Cubierto por `CreatedAt`+`UserId` persistidos y por `LoggingBehavior` (ya en el pipeline). |
| Information Disclosure | Ver R7 (logging de la SAS URL). |
| **Denial of Service** | 🟠 **R2** — ver abajo. |
| Elevation of Privilege | El `Role` nunca se acepta del cliente (no hay campo `Role` en el comando); no aplica. |

### 2. `GET /api/murals/{id}`

| STRIDE | Evaluación |
|---|---|
| Spoofing | Igual que arriba. |
| Tampering | N/A (solo lectura). |
| Repudiation | N/A. |
| **Information Disclosure** | 🟠 **R1 (IDOR)** — ver abajo. |
| DoS | Riesgo bajo (lookup por Id, sin listados). |
| Elevation of Privilege | El check de `Administrador` lee `ClaimTypes.Role` del mismo `ClaimsPrincipal` que arma `SessionAuthenticationHandler` a partir de `User.Role` en la DB — no es un valor que el cliente pueda influenciar. Riesgo bajo, heredado de un mecanismo ya auditado. |

### 3. `IBlobStorageService`/Azure Storage

| STRIDE | Evaluación |
|---|---|
| Spoofing | Depende de la credencial de Storage (connection string/account key). Ver R5. |
| **Tampering** | 🟠 **R4 (path traversal / overwrite del blob)** — ver abajo. |
| Repudiation | N/A (no requerido por el PRD para este subsistema). |
| **Information Disclosure** | Es el núcleo de NFR-03: contenedor privado (`PublicAccessType.None`) + SAS de solo lectura de corta duración. Ver R7 para el riesgo residual de logging. |
| DoS | Disponibilidad de Azure Storage está fuera del control de este ticket — riesgo aceptado implícitamente, igual que ya se acepta para SQL Server. |
| Elevation of Privilege | Una account key comprometida sería crítica — mitigado no hardcodeándola (R5); fuera de alcance usar Managed Identity en este ticket. |

### 4. `INsfwContentScanner`/NsfwSpy

| STRIDE | Evaluación |
|---|---|
| Spoofing | N/A. |
| Tampering | N/A. |
| Repudiation | N/A. |
| Information Disclosure | N/A (no expone nada fuera del proceso). |
| **Denial of Service** | 🟡 **R6 (archivo malformado cuelga/crashea el scan)** — ver abajo. |
| Elevation of Privilege | N/A. |

### 5. Validación de archivo subido (transversal)

| STRIDE | Evaluación |
|---|---|
| **Tampering / Unrestricted Upload** | 🟠 **R3 (content-type/extensión falsificables)** — ver abajo. |

## Riesgos y mitigaciones (F-TM-03)

| # | Riesgo | STRIDE | Likelihood | Impact | Mitigación (se pliega al spec) |
|---|---|---|---|---|---|
| R1 | **IDOR en `GET /api/murals/{id}`**: si el check dueño-o-Admin falla o se omite, cualquier usuario autenticado puede ver la foto y ubicación de un mural ajeno "pendiente"/"rechazado" (viola FR-16 y el espíritu de RF-013). | Information Disclosure | Medium | High | Check de autorización OBLIGATORIO en el Handler, sobre TODA la respuesta (no solo `photoUrl`) cuando `Status` sea `Pending`/`Rejected`: dueño (`UserId` del claim == `Mural.UserId`) o `Role == Administrator`. Denegar con **404** (no 403) para no confirmar la existencia del recurso a quien no tiene acceso. Test obligatorio: dueño ve, Admin ve, un tercer usuario autenticado NO ve (404). |
| R2 | **DoS por upload sin límite**: `IFormFile.Length` en FluentValidation solo valida DESPUÉS de bufferizar el cuerpo; sin un límite a nivel de request, un cliente puede enviar un multipart enorme y agotar memoria antes de que la validación de 10MB corra. | Denial of Service | Medium | High | Configurar `[RequestSizeLimit]` (o `RequestFormLimits`) en la acción `Create` del controller con un tope (~11 MB, margen sobre los 10MB de NFR-01) para que ASP.NET Core rechace el request ANTES de bufferizar el cuerpo completo. Se suma a (no reemplaza) la validación de FluentValidation. |
| R3 | **Spoofing de tipo de archivo**: `Content-Type`/extensión son controlados por el cliente y falsificables — un archivo ejecutable renombrado a `.jpg` con `Content-Type: image/jpeg` pasaría una validación que solo mire esos dos campos. | Tampering (Unrestricted Upload, CWE-434) | Medium | High | Validación de firma de bytes (magic bytes) server-side, obligatoria y no opcional, para JPEG/PNG/WebP — ya estaba en el diseño del Bloque 4, se explicita como requisito de la validación (no solo extensión/content-type). Test obligatorio: archivo no-imagen renombrado a `.jpg` es rechazado. |
| R4 | **Path traversal / overwrite de blob**: si el nombre del blob se derivara del nombre de archivo que manda el cliente, podría contener `../` o colisionar con el blob de otro mural. | Tampering (CWE-22) | Low | High | El nombre del blob SIEMPRE se genera server-side (`{Guid.NewGuid()}{extensión validada}`), nunca a partir del nombre de archivo original del cliente. |
| R5 | **Credenciales de Storage hardcodeadas**: si el connection string/account key de producción quedara en `appsettings.json` committeado, sería una fuga de secreto (F-SAST-01). | Spoofing (vía credencial robada) | Low | Critical | `appsettings.json` (no-Development) NO lleva un connection string de Storage real — se provee vía variable de entorno/User Secrets/Key Vault en despliegue. El valor bien conocido de Azurite (`UseDevelopmentStorage=true`) SÍ puede committearse en `appsettings.Development.json` porque no es un secreto real. |
| R6 | **Archivo malformado cuelga o crashea el scan NSFW**: una imagen corrupta o adversarial podría colgar el modelo de NsfwSpy indefinidamente, bloqueando el request. | Denial of Service | Low | Medium | El wrapper `INsfwContentScanner` corre la inferencia con un timeout/`CancellationToken` explícito además del `try/catch` ya planeado; cualquier excepción, timeout o resultado no concluyente se trata igual que "no responde" (FR-10): el mural queda "pendiente", nunca bloquea el flujo. |
| R7 | **SAS URL en logs**: la URL firmada es, durante su ventana de 5 minutos, una credencial de acceso de solo lectura — si quedara persistida en logs de servidor de larga duración, extendería su exposición más allá de la respuesta HTTP legítima. | Information Disclosure | Low | Low | Revisar en CODE que `LoggingBehavior` no vuelque el cuerpo completo de la respuesta de `GetMuralByIdQuery` a logs persistentes; la URL solo debe viajar en el body de la respuesta HTTP al cliente autorizado. Sin cambio de diseño, solo punto de atención para el bloque 5. |
| R8 | **Riesgo de cadena de suministro de NsfwSpy**: paquete NuGet de terceros no auditado previamente en este repo. | (W-TM-01) | Low | Low | Verificar antes de agregarlo que no tenga CVEs Critical/High conocidos (práctica estándar, no bloqueante — `daw-security-sast` igual lo re-verificará vía F-SAST-13). |

No hay riesgos CRITICAL. Los 4 HIGH (R1–R4) tienen mitigación concreta que se pliega al spec antes de escribirlo — ninguno queda como riesgo aceptado.

## Clasificación de datos sensibles (F-TM-05)

| Dato | Clasificación | Control |
|---|---|---|
| Fotografía del mural (mientras "pendiente"/"rechazado") | Contenido restringido (no PII clásica, pero de acceso controlado por FR-16/RF-013) | Contenedor privado + SAS de solo lectura de 5 min + check dueño-o-Admin (R1) |
| Connection string / account key de Azure Storage | Credencial | Nunca hardcodeada en producción (R5); variable de entorno/secret manager |
| Coordenadas GPS del mural | No sensible per se — su propósito final (FEAT-001d) es ser pública una vez publicado el mural; durante "pendiente"/"rechazado" queda bajo el mismo control de acceso que la foto porque `GetMuralByIdQuery` deniega la respuesta completa, no solo `photoUrl` (ver R1) | Mismo control que la foto mientras el mural no esté publicado |
| `UserId` que vincula un mural a una cuenta | Dato personal indirecto, ya cubierto por el modelo de cuenta de FEAT-001a | Sin cambios adicionales |

## Cifrado (F-TM-07)

- **En tránsito**: HTTPS ya forzado por `UseHttpsRedirection()` (Program.cs, existente); el SDK de Azure Storage usa HTTPS por defecto contra el endpoint de blobs.
- **En reposo**: Azure Storage cifra por defecto con claves administradas por Microsoft — sin configuración adicional requerida para este ticket.
- **Credenciales**: no se cifran, se evita que existan en texto plano en el repo (R5) — es el control equivalente para un secreto, no un dato cifrado en reposo.

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling FEAT-001b — PASSED                 │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Attack surfaces identified: 5                            │
│  Trust boundaries declared: 4                              │
│                                                          │
│  Risks:                                                  │
│    🟠 HIGH: R1 IDOR en GET /api/murals/{id} — Mitigación:│
│       check dueño-o-Admin sobre toda la respuesta, 404   │
│    🟠 HIGH: R2 DoS por upload sin límite — Mitigación:    │
│       RequestSizeLimit ~11MB antes de bufferizar          │
│    🟠 HIGH: R3 Spoofing de tipo de archivo — Mitigación:  │
│       validación de firma de bytes obligatoria            │
│    🟠 HIGH: R4 Path traversal en nombre de blob —          │
│       Mitigación: blob name siempre generado server-side  │
│    🟡 MEDIUM: R5 credenciales de Storage hardcodeadas —   │
│       Mitigación: fuera de appsettings.json de producción │
│    🟡 MEDIUM: R6 archivo malformado cuelga el scan NSFW — │
│       Mitigación: timeout explícito + fail-open a pending │
│    🟢 LOW: R7 SAS URL en logs — punto de atención en CODE │
│    🟢 LOW: R8 cadena de suministro de NsfwSpy — verificar │
│       CVEs antes de agregar el paquete                    │
│                                                          │
│  Mitigations to fold into the spec:                      │
│    1. GetMuralByIdQuery: check dueño-o-Admin sobre toda   │
│       la respuesta cuando Status ∈ {Pending, Rejected},   │
│       404 si no autorizado                                │
│    2. MuralsController.Create: [RequestSizeLimit] ~11MB   │
│    3. CreateMuralCommandValidator: validación de firma de │
│       bytes del archivo (magic bytes), no solo content-   │
│       type/extensión                                      │
│    4. AzureBlobStorageService: nombre de blob siempre      │
│       generado server-side (GUID + extensión validada)    │
│    5. appsettings.json (no-Development): sin connection   │
│       string de Storage real committeado                  │
│    6. INsfwContentScanner: timeout/CancellationToken       │
│       explícito alrededor de la inferencia                │
│                                                          │
│  ─────────────────────────────────────────────────────   │
│  Risks: C:0 H:4 M:2 L:2                                   │
│  Report: docs/daw/security/threat-FEAT-001b.md            │
└─────────────────────────────────────────────────────────┘
```
