# Fix QUICK-FIX-001: DiscoveryClient sin registrar en app.config.ts

- **Bug**: `/discover` (y el redirect de la raíz `/` cuando hay sesión activa, vía
  `rootRedirectGuard`) falla con `NG0201: No provider found for DiscoveryClient` apenas carga —
  `DiscoveryService` inyecta `DiscoveryClient` (el cliente NSwag generado), pero `DiscoveryClient`
  nunca se agregó al array `providers` de `app.config.ts`, a diferencia de `AuthClient`,
  `ModerationClient` y `MuralsClient`, que sí están registrados. Mismo patrón de bug que el que
  FEAT-001c ya corrigió una vez para `MuralsClient` (comentario en `app.config.ts:37-39`).
- **Change**: `frontend/src/app/app.config.ts:15-20` (import) y `:35-40` (array `providers`) —
  agregar `DiscoveryClient` al import desde `./core/api-client/api-client.generated` y al array
  `providers`, junto a `AuthClient`/`ModerationClient`/`MuralsClient`.
- **Regression test**: `app.config.spec.ts` (nuevo) — instancia el `ApplicationConfig` real (o monta
  un componente que inyecte `DiscoveryService`) y verifica que resolver `DiscoveryClient` vía el
  injector no lanza `NullInjectorError`/`NG0201`. Falla antes del fix (el provider no existe), pasa
  después.
- **Risk**: ninguno — agrega un provider que faltaba, sin quitar ni modificar ningún otro. Mismo
  patrón ya aplicado y verificado para `MuralsClient` en FEAT-001c sin efectos secundarios.
