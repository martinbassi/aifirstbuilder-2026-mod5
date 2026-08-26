# Spec FEAT-007: Rehidratar sesión (rol de usuario) al recargar la página

| Field | Value |
|-------|-------|
| Ticket | FEAT-007 |
| PRD | docs/daw/prd/prd-FEAT-001a.md |
| Tier | FEATURE |
| Date | 2026-08-26 |
| Spec loops | 0 |

## Summary

El token de sesión sobrevive a un F5 (persistido en `sessionStorage`), pero el usuario/rol solo vive
en un signal en memoria que arranca en `null`. Se agrega un endpoint `GET /api/auth/session`
(`[Authorize]`) que devuelve `{ Username, Role }` de la sesión actual — `Role` leído directamente del
claim ya presente en el `ClaimsPrincipal` (mismo dato que `SessionAuthenticationHandler` resuelve
fresco desde la DB en cada request), `Username` buscado en la DB por el `Id` del claim, ya que no
viaja en ningún claim hoy. Del lado Angular, un `provideAppInitializer` llama a ese endpoint al
arrancar la app (solo si hay un token guardado) y repuebla `SessionStore` antes de que el router
resuelva cualquier ruta protegida — así el sidebar y `adminGuard`, que ya leen `sessionStore.user()`
correctamente, dejan de ver `null` tras un refresh.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-08 | Block 1, Block 3 |
| NFR-04 | Strategy: `provideAppInitializer` bloquea el bootstrap de la app hasta que la rehidratación resuelve (Block 3) |
| AC-07 | Block 3 |
| AC-08 | Block 3 (delegado al interceptor HTTP existente, sin duplicar su manejo de 401) |
| AC-09 | Block 1, Block 3 |

## Dependencies between blocks

Block 1 → Block 2 (el cliente NSwag se regenera contra el endpoint del Block 1) → Block 3 (usa el
método generado). Ningún bloque es paralelo a otro.

## Block 1 — Backend: endpoint `GET /api/auth/session`

**Files**
- `backend/src/Paretto.Api/Features/Auth/Queries/GetCurrentSessionQuery.cs` (new) — query, handler y
  `GetCurrentSessionResponse`.
- `backend/src/Paretto.Api/Api/Controllers/AuthController.cs` (modified) — nueva acción `Session()`.
- `backend/tests/Paretto.Api.Tests/GetCurrentSessionTests.cs` (new) — tests de integración
  (`WebApplicationFactory`).
- `backend/tests/Paretto.Api.Tests/GetCurrentSessionQueryHandlerTests.cs` (new) — test unitario del
  caso defensivo.

**Logic**

`GetCurrentSessionQuery` no tiene parámetros propios (igual que `LogoutCommand`) — todo sale del
`ClaimsPrincipal` de la request actual. El handler:

1. Lee `ClaimTypes.NameIdentifier` y `ClaimTypes.Role` directamente del `ClaimsPrincipal` vía
   `IHttpContextAccessor` — **no** vuelve a consultar el rol contra la base, porque
   `SessionAuthenticationHandler` (`backend/src/Paretto.Infrastructure/Auth/
   SessionAuthenticationHandler.cs:76-79`) ya lo resolvió fresco desde `Session.User.Role` para esta
   misma request, con la mitigación de elevación de privilegios ya aplicada ahí (mismo patrón que
   `GetMuralByIdQuery.ReadCallerIdentity`, que lee ambos claims sin re-consultar la DB).
2. Parsea el `NameIdentifier` a `Guid` y busca `AppDbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId)`
   — esta consulta es SOLO para `Username`, que no viaja en ningún claim hoy.
3. Devuelve `new GetCurrentSessionResponse { Username = user.Username, Role = roleClaim }`.

Caso defensivo: si el `User` no existe en la DB (debería ser inalcanzable — `[Authorize]` ya exige
una sesión válida, y la FK `Session.UserId → User.Id` lo garantiza), lanzar `InvalidOperationException`
— mismo criterio que `CreateMuralCommandHandler.ReadUserId`: no amerita una excepción de dominio
propia para un caso que no debería ejecutarse nunca en la práctica.

`AuthController.Session()`: `[HttpGet("session")]`, `[Authorize]` (cualquier rol autenticado, no
restringido a Administrador), despacha la query y devuelve `Ok(response)` — mismo patrón que
`Login`/`Logout`.

**API contract**

- Method + path: `GET /api/auth/session`
- Request: sin body, sin parámetros — el `Authorization: Bearer {token}` es lo único que importa.
- Response (200): `{ username: string, role: string }`
- Error codes: `401` (sin token, token inválido o expirado — manejado por `[Authorize]`/
  `SessionAuthenticationHandler`, sin lógica adicional en este endpoint)
- Auth: `[Authorize]` (cualquier rol autenticado)

**Error handling**

Ningún manejo de error propio más allá de lo que `[Authorize]` ya resuelve (401 automático si la
sesión no es válida). El caso defensivo de usuario no encontrado se documenta arriba.

**Required tests**

- [ ] `GetCurrentSessionTests`: sesión válida (Administrador) → 200 con `{ username, role:
  "Administrator" }` — valida AC-09
- [ ] `GetCurrentSessionTests`: sesión válida (Standard) → 200 con `{ username, role: "Standard" }`
- [ ] `GetCurrentSessionTests`: sin header `Authorization` → 401
- [ ] `GetCurrentSessionTests`: token inexistente/no correspondiente a ninguna sesión → 401
- [ ] `GetCurrentSessionQueryHandlerTests` (unit, sin `WebApplicationFactory`): claim
  `NameIdentifier` con un `Guid` válido que no corresponde a ningún `User` en la DB →
  `InvalidOperationException` — ejercita el caso defensivo documentado arriba (inalcanzable vía
  `[Authorize]` en producción, pero reproducible construyendo el handler directamente con un
  `AppDbContext` InMemory vacío y un `ClaimsPrincipal` de prueba)

**Completion criterion**

Los 5 tests pasan; `dotnet build` limpio; `GET /api/auth/session` con un token de sesión válido
devuelve 200 con el `username`/`role` reales del usuario dueño de ese token.

## Block 2 — Regenerar el cliente NSwag

**Files**
- `frontend/src/app/core/api-client/api-client.generated.ts` (regenerated — nunca editado a mano)

**Logic**

Con el backend de Block 1 corriendo localmente (`dotnet run` en `Paretto.Api`), correr la
regeneración de NSwag ya configurada (`nswag run nswag.json` desde `backend/src/Paretto.Api/`) contra
el `swagger.json` servido en `https://localhost:7126/swagger/v1/swagger.json`, para que
`AuthClient.session()` aparezca en el cliente generado. El archivo generado se commitea tal cual sale
de la herramienta (está trackeado en git desde FEAT-001a, no en `.gitignore`).

**Required tests**

- [ ] Confirmar que `AuthClient.session()` existe en el archivo regenerado y que el resto de la
  suite frontend sigue compilando/pasando sin cambios (ningún test depende de la lista exacta de
  métodos de `AuthClient` — confirmado en el impact scan).

**Completion criterion**

`AuthClient.session()` existe en `api-client.generated.ts`; `npx tsc --build --noEmit tsconfig.json`
limpio.

## Block 3 — Frontend: `AuthService` + rehidratación al arrancar

**Files**
- `frontend/src/app/features/auth/data/auth.service.ts` (modified) — nuevo método
  `getCurrentSession()`.
- `frontend/src/app/core/bootstrap/session-rehydration.initializer.ts` (new) —
  `rehydrateSessionOnStartup()`.
- `frontend/src/app/app.config.ts` (modified) — registrar
  `provideAppInitializer(rehydrateSessionOnStartup)`.
- `frontend/src/app/features/auth/data/auth.service.spec.ts` (modified) — tests del método nuevo.
- `frontend/src/app/core/bootstrap/session-rehydration.initializer.spec.ts` (new).

**Logic**

`AuthService.getCurrentSession(): Observable<SessionUser>` envuelve `authClient.session()`, mapea la
respuesta a `{ username, role }`, llama a `sessionStore.setUser(...)` en éxito (mismo patrón que
`login()`/`register()` ya aplican) y propaga el error mapeado con `toApiError` en fallo — sin manejo
de 401 propio, eso ya lo cubre el interceptor.

`rehydrateSessionOnStartup()` (función standalone, usable con `provideAppInitializer`):

```ts
export function rehydrateSessionOnStartup(): Promise<void> {
  const sessionStore = inject(SessionStore);
  const authService = inject(AuthService);

  if (sessionStore.token() === null) {
    return Promise.resolve();
  }

  return firstValueFrom(authService.getCurrentSession()).then(
    () => undefined,
    () => undefined, // el interceptor ya limpia la sesión y redirige ante un 401 (AC-08);
                      // cualquier otro error no debe bloquear el arranque de la app.
  );
}
```

`app.config.ts` agrega `provideAppInitializer(rehydrateSessionOnStartup)` al array de `providers` —
Angular espera esta promesa antes de resolver el router (NFR-04): con token nulo, resuelve
sincrónicamente sin red; con token presente, la app espera la respuesta (éxito o fallo) antes de
renderizar cualquier ruta.

**Error handling**

- `getCurrentSession()` en fallo: no llama a `sessionStore.setUser`, propaga `ApiError` — quien lo
  invoque por fuera del initializer (si lo hubiera) puede reaccionar; el initializer mismo lo
  descarta silenciosamente (ver arriba).
- `rehydrateSessionOnStartup()` nunca rechaza — un error de red aquí no debe dejar la app sin
  arrancar.

**Required tests**

- [ ] `auth.service.spec.ts`: `getCurrentSession()` éxito → llama a `sessionStore.setUser({
  username, role })` con los valores de la respuesta — valida AC-09
- [ ] `auth.service.spec.ts`: `getCurrentSession()` error de API → NO llama a `setUser`, propaga un
  `ApiError` mapeado
- [ ] `session-rehydration.initializer.spec.ts`: sin token (`sessionStore.token()` es `null`) →
  `getCurrentSession()` nunca se invoca, la promesa resuelve — valida el camino "nada que
  rehidratar"
- [ ] `session-rehydration.initializer.spec.ts`: con token, `getCurrentSession()` resuelve → la
  promesa del initializer resuelve sin error — valida AC-07
- [ ] `session-rehydration.initializer.spec.ts`: con token, `getCurrentSession()` falla → la
  promesa del initializer IGUAL resuelve (nunca rechaza) — valida AC-08 (el initializer no bloquea
  el arranque; el interceptor es quien limpia/redirige)

**Completion criterion**

Los 5 tests pasan; `sessionStore.user()` refleja el rol correcto inmediatamente después de que
`provideAppInitializer` resuelve, sin requerir un nuevo login, en un escenario con token válido
persistido.

## Final verification

Con los 3 bloques completos: un usuario Administrador logueado que hace F5 en cualquier ruta ve el
ítem "Moderación" en el sidebar y puede navegar a `/moderation` sin ser expulsado por `adminGuard` —
sin volver a loguearse. Un token inválido/expirado sigue redirigiendo a `/login` exactamente como
hoy. La suite completa (backend + frontend) pasa sin regresiones.
