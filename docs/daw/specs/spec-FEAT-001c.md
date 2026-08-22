# Spec FEAT-001c: Moderación mínima

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| PRD | docs/daw/prd/prd-FEAT-001c.md |
| Tier | FEATURE |
| Date | 2026-08-22 |
| Spec loops | 0 |

## Summary

Cierra el ciclo de vida de un mural agregando el estado "publicado" y tres operaciones admin-only
(listar pendientes, aprobar, rechazar), gateadas con `[Authorize(Roles = "Administrator")]` —
declarativo, sin código de autorización a mano, apoyado en el claim de rol que
`SessionAuthenticationHandler` ya emite en cada request (FEAT-001a). Reutiliza el mapeo
`Mural`→`MuralResponse` y el patrón de URL firmada de FEAT-001b. En el frontend, expone el rol del
usuario en la sesión (hoy ausente) para gatear una pantalla Angular mínima de moderación, con su
propia ruta protegida — sin conectarla a la ruta raíz (ver "Fuera de alcance" abajo).

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 2 |
| FR-02 | Block 3 |
| FR-03 | Block 4 |
| FR-04 | Block 2, Block 3, Block 4 |
| NFR-01 | Strategy: reutiliza `[Authorize]` + la sesión de 7 días de `SessionAuthenticationHandler` (FEAT-001a) — ningún mecanismo de sesión nuevo. |
| AC-01 | Block 2 |
| AC-02 | Block 2 |
| AC-03 | Block 3 |
| AC-04 | Block 3 |
| AC-05 | Block 4 |
| AC-06 | Block 4 |

## Dependencies between blocks

```
Block 1 (rol en login) ──────────────────────────────┐
                                                        v
Block 2 (listar pendientes) ──┬──> Block 3 (aprobar) ─┼──> Block 5 (rol en sesión frontend) ──┐
                               └──> Block 4 (rechazar) ┘                                       v
                                                              Block 6 (guard + servicio) ──> Block 7 (pantalla)
```

- Block 2 crea `ModerationController`; Block 3 y Block 4 lo modifican (agregan acciones) — deben
  ejecutarse después de Block 2.
- Block 3 agrega `MuralStatus.Published` y define `ModeratedMuralNotFoundException` /
  `MuralNotPendingException`, que Block 4 reutiliza — Block 4 depende de Block 3.
- Block 5 depende de Block 1 (necesita `Role` en la respuesta de login para propagarlo al store).
- Block 6 depende de Block 5 (el guard de admin lee el rol de la sesión) y de Block 2/3/4 (el
  servicio llama a los tres endpoints).
- Block 7 depende de Block 6.

## Fuera de alcance de este spec (gap de split documentado)

RF-050 ("pantalla de entrada según sesión") no está en el PRD de FEAT-001c y este spec no lo
implementa. El PRD de FEAT-001d, al momento del split, dejó explícitamente sin cubrir la rama de
Administrador de RF-050 ("no existe una pantalla de moderación construida en FEAT-001c... queda
como ajuste menor para cuando ese sub-ticket la agregue" — `prd-FEAT-001d.md`, "Out of Scope").
Ahora que este spec construye esa pantalla, ese "ajuste menor" sigue sin ticket propio: **ni
FEAT-001c ni FEAT-001d (con su FR-08 actual) resuelven qué pantalla ve un Administrador al abrir la
aplicación en `/`.** Queda como gap conocido para un ticket chico aparte. La pantalla de moderación
de este spec vive en su propia ruta (`/moderation`), alcanzable directamente, no en `/`.

## Block 1 — Exponer el rol en el login

**Files**
- `backend/src/Paretto.Api/Features/Auth/Commands/LoginCommand.cs` (modified) — `LoginResponse`
  gana `Role` (string); el Handler lo completa con `user.Role.ToString()`.
- `backend/tests/Paretto.Api.Tests/LoginTests.cs` (modified) — nueva aserción sobre `Role` en la
  respuesta.

**Logic**
`LoginCommandHandler.Handle` ya tiene el `User` cargado (necesita `user.PasswordHash` para verificar
credenciales); agrega `Role = user.Role.ToString()` al `LoginResponse` que ya construye.

**API contract**
- Method + path: `POST /api/auth/login` (ya existe — se modifica el contrato de respuesta)
- Response: agrega `role: string` (`"Standard"` | `"Administrator"`) a `LoginResponse`
- Error codes: sin cambios (401 `InvalidCredentialsException`, ya existente)
- Auth: sin cambios (endpoint público)

**Error handling**
Sin errores nuevos — cambio aditivo sobre un endpoint existente.

**Required tests**
- [ ] Login exitoso con usuario `Standard` devuelve `role: "Standard"`.
- [ ] Login exitoso con usuario `Administrator` devuelve `role: "Administrator"`.

**Completion criterion**
`LoginTests.cs` pasa, incluyendo las dos aserciones nuevas sobre `role`.

## Block 2 — Listar murales pendientes (admin)

**Files**
- `backend/src/Paretto.Api/Features/Moderation/Queries/GetPendingMuralsQuery.cs` (new) — Query,
  Handler, Response.
- `backend/src/Paretto.Api/Api/Controllers/ModerationController.cs` (new) — `[Authorize(Roles =
  "Administrator")]` a nivel clase (cubre las tres acciones de este spec), constructor con
  `IMediator` únicamente, igual que `MuralsController`/`AuthController`.
- `backend/tests/Paretto.Api.Tests/GetPendingMuralsTests.cs` (new).

**Logic**
`GetPendingMuralsQueryHandler` consulta `AppDbContext.Murals.Where(m => m.Status ==
MuralStatus.Pending)`, ordena por `CreatedAt` ascendente (el pendiente más viejo primero — cola de
moderación, evita que un mural quede esperando indefinidamente detrás de ingresos más nuevos), y
pagina con `Skip((page - 1) * pageSize).Take(pageSize)` (mitigación de threat model R4 — sin esto,
un volumen alto de pendientes devuelve una lista sin límite). Mapea cada `Mural` a `MuralResponse`
con la config de Mapster ya registrada en `MuralMappingConfig` (Features/Murals), y completa
`PhotoUrl` por ítem con `IBlobStorageService.GenerateReadSasUrl` — mismo patrón que
`GetMuralByIdQueryHandler`. No necesita chequeo de rol manual: `[Authorize(Roles =
"Administrator")]` en el controller ya lo resuelve declarativamente (401 sin sesión, 403 con sesión
pero sin el rol — comportamiento default de `AuthorizationMiddlewareResultHandler`, sin código
nuevo).

**API contract**
- Method + path: `GET /api/moderation/murals/pending?page={page}&pageSize={pageSize}`
- Request: `page` (query, int, opcional, default `1`, mínimo `1`), `pageSize` (query, int, opcional,
  default `20`, rango `1..50`)
- Response: `GetPendingMuralsResponse { Murals: MuralResponse[], Page: int, PageSize: int,
  TotalCount: int }` — `TotalCount` le permite al frontend calcular si hay página siguiente sin un
  segundo request.
- Error codes: `401` (sin sesión), `403` (sesión sin rol Administrator), `400` (`page` < 1 o
  `pageSize` fuera de `1..50`, FluentValidation)
- Auth: `[Authorize(Roles = "Administrator")]`

**Input validation**
`GetPendingMuralsQueryValidator` (FluentValidation, mismo patrón que
`CreateMuralCommandValidator`): `page >= 1`; `pageSize` entre `1` y `50` inclusive (cota dura —
mitigación de R4, evita que un cliente pida `pageSize=1000000` y recree el problema sin límite que
la paginación existe para resolver).

**Error handling**
Sin excepciones de dominio nuevas en este bloque — 401/403 los produce el pipeline de autorización
de ASP.NET Core antes de llegar al Handler; 400 lo produce `ValidationBehavior` (pipeline de
FluentValidation ya existente) ante un `page`/`pageSize` fuera de rango.

**Required tests**
- [ ] Un Administrador autenticado recibe 200 con los murales `Pending` de la página pedida,
  ordenados por `CreatedAt` ascendente, cada uno con `photoUrl` (AC-01).
- [ ] Sin `page`/`pageSize`, aplica el default (`page=1`, `pageSize=20`) y devuelve `TotalCount`.
- [ ] Con más pendientes que `pageSize`, pedir `page=2` devuelve el resto (sin solapar con `page=1`).
- [ ] `page=0` o `pageSize=51` (fuera de `1..50`) devuelve 400 (sad path).
- [ ] Un usuario `Standard` autenticado recibe 403 (AC-02, sad path).
- [ ] Una request sin sesión recibe 401 (sad path).

**Completion criterion**
`GetPendingMuralsTests.cs` pasa; `GET /api/moderation/murals/pending` devuelve 200 con la página
pedida y `TotalCount` para un admin, 400 con `page`/`pageSize` inválidos, 403 para un no-admin
autenticado, 401 sin sesión.

## Block 3 — Aprobar mural (admin)

**Files**
- `backend/src/Paretto.Domain/Enums/MuralStatus.cs` (modified) — agrega `Published = 2` (sin
  migración EF Core: la columna `Status` se persiste como `int` plano, sin `HasConversion<string>`
  ni `CHECK constraint`, confirmado contra `AppDbContext.cs` y la migración `AddMurals`).
- `backend/src/Paretto.Api/Features/Moderation/Commands/ApproveMuralCommand.cs` (new) — Command,
  Handler, Response, `ModeratedMuralNotFoundException` (404), `MuralNotPendingException` (409).
- `backend/src/Paretto.Api/Api/Controllers/ModerationController.cs` (modified) — agrega la acción
  `POST {id}/approve`.
- `backend/tests/Paretto.Api.Tests/ApproveMuralTests.cs` (new).

**Logic**
`ApproveMuralCommandHandler` busca el `Mural` por id; si no existe, lanza
`ModeratedMuralNotFoundException`. Si `mural.Status != MuralStatus.Pending`, lanza
`MuralNotPendingException` — es un chequeo de estado leído-y-verificado, **no** control de
concurrencia real (RF-051 queda fuera de alcance del PRD como riesgo aceptado; ver
`docs/daw/prd/prd-FEAT-001c.md`, "Risks and Mitigations"). Si pasa ambos, setea
`Status = MuralStatus.Published` y guarda.

**API contract**
- Method + path: `POST /api/moderation/murals/{id:guid}/approve` (route constraint `:guid`, mismo
  patrón que `MuralsController.GetById`)
- Request: `id` (route param, Guid — tipo forzado por el constraint de ruta, ASP.NET responde 400
  automáticamente si no matchea antes de llegar al Handler)
- Response: `ModerationActionResponse { Id: Guid, Status: string }`
- Error codes: `401`, `403` (declarativos, como Block 2), `404` (`ModeratedMuralNotFoundException`),
  `409` (`MuralNotPendingException`)
- Auth: `[Authorize(Roles = "Administrator")]` (heredado del controller)

**Data model**
`MuralStatus` (enum, sin cambio de esquema): agrega el miembro `Published = 2`.

**Error handling**
- Mural inexistente → `ModeratedMuralNotFoundException` (404, mensaje genérico "Mural not found.").
- Mural existente pero no `Pending` (ya `Published` o `Rejected`) → `MuralNotPendingException` (409,
  mensaje genérico indicando el estado actual esperado).
- Ambas heredan `AppException`; `ExceptionHandlingMiddleware` las traduce sin tocar el middleware
  (mismo patrón que `InvalidCredentialsException`/`MuralAccessDeniedException`).

**Required tests**
- [ ] Un Administrador aprueba un mural `Pending` → 200, `Status: "Published"` (AC-03).
- [ ] Un usuario `Standard` intenta aprobar → 403, el mural sigue `Pending` (AC-04, sad path).
- [ ] Aprobar un `id` inexistente → 404 (sad path).
- [ ] Aprobar un mural que ya está `Published` o `Rejected` → 409 (sad path).

**Completion criterion**
`ApproveMuralTests.cs` pasa; el mural aprobado queda `Published` en la base; los tres sad paths
devuelven el código correcto sin mutar el estado del mural.

## Block 4 — Rechazar mural (admin)

**Files**
- `backend/src/Paretto.Api/Features/Moderation/Commands/RejectMuralCommand.cs` (new) — Command,
  Handler, Response; reutiliza `ModeratedMuralNotFoundException`/`MuralNotPendingException` de
  `ApproveMuralCommand.cs`.
- `backend/src/Paretto.Api/Api/Controllers/ModerationController.cs` (modified) — agrega la acción
  `POST {id}/reject`.
- `backend/tests/Paretto.Api.Tests/RejectMuralTests.cs` (new).

**Logic**
Misma forma que `ApproveMuralCommandHandler`, pero setea `Status = MuralStatus.Rejected`.

**API contract**
- Method + path: `POST /api/moderation/murals/{id:guid}/reject` (route constraint `:guid`, mismo
  patrón que `MuralsController.GetById`)
- Request: `id` (route param, Guid — tipo forzado por el constraint de ruta, ASP.NET responde 400
  automáticamente si no matchea antes de llegar al Handler)
- Response: `ModerationActionResponse { Id: Guid, Status: string }`
- Error codes: `401`, `403`, `404`, `409` (mismos que Block 3)
- Auth: `[Authorize(Roles = "Administrator")]` (heredado del controller)

**Error handling**
Idéntico a Block 3, reutilizando las mismas dos excepciones.

**Required tests**
- [ ] Un Administrador rechaza un mural `Pending` → 200, `Status: "Rejected"` (AC-05).
- [ ] Un usuario `Standard` intenta rechazar → 403, el mural sigue `Pending` (AC-06, sad path).
- [ ] Rechazar un `id` inexistente → 404 (sad path).
- [ ] Rechazar un mural que ya está `Published` o `Rejected` → 409 (sad path).

**Completion criterion**
`RejectMuralTests.cs` pasa; el mural rechazado queda `Rejected` en la base; los tres sad paths
devuelven el código correcto sin mutar el estado del mural.

## Block 5 — Rol en la sesión del frontend

**Files**
- `frontend/src/app/features/auth/state/session.store.ts` (modified) — `SessionUser` gana `role?:
  string`.
- `frontend/src/app/features/auth/data/auth.service.ts` (modified) — `login()` propaga
  `response.role` a `sessionStore.setSession`.
- `frontend/src/app/features/auth/data/auth.service.spec.ts` (modified) — la aserción existente
  `expect(sessionStore.user()).toEqual({ username: 'ana' })` (línea 63) se rompe al propagar
  siempre `role`; se actualiza para incluirlo.
- `frontend/src/app/core/api-client/api-client.generated.ts` (regenerated) — vía NSwag desde el
  OpenAPI del backend una vez desplegado Block 1. **Nunca editado a mano.**

**Logic**
`AuthService.login()` ya lee `response.token`; agrega la lectura de `response.role` y lo pasa como
parte del objeto `SessionUser` a `sessionStore.setSession(response.token, { username:
request.username, role: response.role })`.

**Error handling**
Sin cambios — mismo flujo de error ya existente en `login()`.

**Required tests**
- [ ] Tras un login exitoso, `sessionStore.user()?.role` refleja el valor devuelto por el backend.
- [ ] El test existente de `auth.service.spec.ts` (línea 63) sigue pasando con el campo `role`
  incluido en la aserción.

**Completion criterion**
`auth.service.spec.ts` pasa; `SessionStore.user()` incluye `role` tras un login exitoso.

## Block 6 — Guard de administrador + servicio de moderación

**Files**
- `frontend/src/app/app.routes.ts` (modified) — nuevo `adminGuard` (junto al `authGuard`
  existente), nueva ruta `moderation` con `canActivate: [authGuard, adminGuard]`.
- `frontend/src/app/app.routes.spec.ts` (modified) — agrega `describe('adminGuard', ...)` junto al
  `describe('authGuard', ...)` ya existente (líneas 9–47).
- `frontend/src/app/app.config.ts` (modified) — registra `ModerationClient` en `providers`, mismo
  patrón que `AuthClient`. **Incluye además la corrección de un bug preexistente de FEAT-001b:**
  `MuralsClient` tampoco está registrado ahí hoy (los clientes NSwag no son `providedIn: 'root'`,
  así que crear un mural falla con `NullInjectorError` en producción — enmascarado en tests porque
  `mural.service.spec.ts` provee `MuralsClient` manualmente en el `TestBed`). Se agrega también su
  provider en este mismo bloque, ya que se toca el mismo archivo y la misma lista.
- `frontend/src/app/features/moderation/data/moderation.service.ts` (new) — envuelve
  `ModerationClient`, mismo patrón que `MuralService`/`AuthService`.
- `frontend/src/app/features/moderation/data/moderation.service.spec.ts` (new).

**Logic**
`adminGuard` replica la forma de `authGuard`: si `sessionStore.isAuthenticated()` es falso, redirige
a `/login` (delegado a `authGuard`, que corre antes en el array `canActivate`); si es verdadero pero
`sessionStore.user()?.role !== 'Administrator'`, redirige a `/` (o a una ruta neutra — sin pantalla
de "acceso denegado" dedicada, fuera de alcance del PRD). `ModerationService` expone `getPending(page, pageSize)`,
`approve(id)`, `rejectMural(id)`, cada uno envolviendo el método correspondiente de
`ModerationClient` con el mismo `catchError(toApiError)` que `MuralService`. `getPending()` propaga
`page`/`pageSize` al cliente generado (Block 2) y devuelve `{ murals, page, pageSize, totalCount }`
tal cual los expone `GetPendingMuralsResponse`.

> **Nota de seguridad (threat model, ver `docs/daw/security/threat-FEAT-001c.md`):** `adminGuard` es
> control de UX únicamente — decide qué pantalla mostrar, nada más. El `role` que lee viene del
> `SessionStore` (afirmado por el cliente, tamperable vía devtools). La autorización real de los
> tres endpoints de moderación NUNCA depende de este valor: `[Authorize(Roles = "Administrator")]`
> re-verifica server-side, en cada request, contra el claim de rol que `SessionAuthenticationHandler`
> lee fresco de `Session.User.Role` en la base de datos — nunca contra nada que el cliente envíe. Un
> usuario que manipule su `role` local en el store como mucho ve la pantalla; sus llamadas a
> aprobar/rechazar siguen devolviendo 403.

**Error handling**
`ModerationService` propaga `ApiError` igual que `MuralService`/`AuthService` — nunca un
`catchError` que silencia el error.

**Required tests**
- [ ] `adminGuard` permite el acceso con `role: 'Administrator'`.
- [ ] `adminGuard` redirige cuando `role` es `'Standard'` o no hay sesión.
- [ ] `ModerationService.getPending()` mapea la respuesta del cliente generado, incluyendo
  `page`/`pageSize`/`totalCount`.
- [ ] `ModerationService.approve()`/`rejectMural()` propagan `ApiError` en caso de fallo (sad path).

**Completion criterion**
`app.routes.spec.ts` y `moderation.service.spec.ts` pasan; `ModerationClient` y `MuralsClient`
quedan registrados en `app.config.ts`.

## Block 7 — Pantalla de moderación

**Files**
- `frontend/src/app/features/moderation/ui/pending-murals-list.component.ts` (new) — standalone,
  signals (`signal`/`computed`), lista los murales pendientes con foto, ubicación y fecha, y un par
  de botones aprobar/rechazar por ítem.
- `frontend/src/app/features/moderation/ui/pending-murals-list.component.html` (new).
- `frontend/src/app/features/moderation/ui/pending-murals-list.component.spec.ts` (new).

**Logic**
Al inicializar, llama a `ModerationService.getPending(page)` (`pageSize` fijo en el default del
backend, 20 — sin selector de tamaño de página, "moderación mínima") y guarda `murals`/`page`/
`totalCount` en signals. Botones "Siguiente"/"Anterior" (signal `page`, arranca en `1`) — "Anterior"
deshabilitado en `page === 1`, "Siguiente" deshabilitado cuando `page * pageSize >= totalCount`
(`computed` sobre esos tres signals); cada click pide la página nueva. Cada botón "Aprobar"/
"Rechazar" llama a `approve(id)`/`rejectMural(id)` y, en éxito, quita el ítem de la lista local (sin
recargar toda la página). Un error de `ApiError` se muestra inline, sin interrumpir la vista.

**Input validation**
No aplica — el componente no recibe input de formulario, solo acciones sobre ítems ya cargados.

**Error handling**
Un fallo de `getPending()` muestra un estado de error simple en la pantalla (sin reintento
automático). Un fallo de `approve`/`rejectMural` deja el ítem en la lista y muestra el error, sin
sacarlo silenciosamente.

**Required tests**
- [ ] Renderiza la lista de pendientes devuelta por el servicio (AC-01).
- [ ] Aprobar un ítem lo remueve de la lista tras una respuesta exitosa (AC-03).
- [ ] Rechazar un ítem lo remueve de la lista tras una respuesta exitosa (AC-05).
- [ ] Un error del servicio al listar se muestra sin romper el componente (sad path).
- [ ] Un error de `approve`/`rejectMural` deja el ítem en la lista y muestra el error, sin removerlo
  (sad path).
- [ ] "Anterior" está deshabilitado en `page=1`; "Siguiente" está deshabilitado cuando
  `page * pageSize >= totalCount`.
- [ ] Click en "Siguiente" pide `page + 1` al servicio y reemplaza la lista mostrada.

**Completion criterion**
`pending-murals-list.component.spec.ts` pasa; la ruta `/moderation` (Block 6) renderiza el
componente para una sesión con rol Administrator.

## Final verification

- Los 4 FR y las 6 AC del PRD tienen al menos un test pasando que los valida (ver "Coverage" arriba).
- `dotnet test` (backend) y `ng test` (frontend) en verde.
- `npx tsc --noEmit` (frontend, declarado en `AGENTS.md` tras `/daw-context-check`) sin errores.
- Ningún mural `Pending`/`Rejected` queda expuesto en un endpoint público (sin cambios en ese
  frente — este spec solo toca superficie admin-only).
- El gap RF-050 (sección "Fuera de alcance" arriba) queda documentado, no resuelto por este spec.
