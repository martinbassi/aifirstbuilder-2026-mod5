# Fix QUICK-FIX-004: Regenerar el cliente NSwag automáticamente al correr dotnet run

- **Bug**: hoy, tocar un DTO/endpoint del backend y correr `dotnet run` no actualiza
  `frontend/src/app/core/api-client/api-client.generated.ts` — hay que correr manualmente
  `nswag run nswag.json` (documentado en AGENTS.md/spec-FEAT-011.md), un paso extra fácil de
  olvidar que deja el cliente TypeScript desincronizado del backend real.
- **Change**: `backend/src/Paretto.Api/Program.cs` — dentro del bloque `if
  (app.Environment.IsDevelopment())` ya existente (el mismo que gatea Swagger/CORS), registrar
  `app.Lifetime.ApplicationStarted.Register(() => Process.Start("nswag", "run nswag.json"))`.
  `nswag.json` lee el contrato desde `https://localhost:7126/swagger/v1/swagger.json` **en vivo**
  (no desde un archivo estático) — por eso no puede ser un paso de build (pre/post-build target):
  necesita a Kestrel ya escuchando, que es exactamente lo que `ApplicationStarted` garantiza.
  Fire-and-forget, sin bloquear el arranque ni las requests; si `nswag` no está instalado o falla,
  el error queda contenido en el callback (la infraestructura de hosting lo loguea, no tira abajo
  el proceso) y el flujo manual existente sigue funcionando como fallback.
- **Regression test**: no aplica un test automatizado (dispara un proceso externo real contra un
  archivo generado, no es lógica de negocio verificable con mocks). **Verificación manual**: correr
  `dotnet run` en `Paretto.Api`, tocar un campo de un DTO existente (p. ej. agregar una propiedad a
  `AddressSuggestionDto`), matar y volver a correr `dotnet run`, confirmar que
  `api-client.generated.ts` refleja el cambio sin correr `nswag run nswag.json` a mano.
- **Risk**: none — cambio gateado por `IsDevelopment()` (mismo patrón ya usado para Swagger/CORS en
  este archivo), nunca se ejecuta en producción. Sin dependencias nuevas (`nswag` CLI ya está
  instalado y documentado como parte del flujo existente). Si el proceso falla, degrada
  silenciosamente al flujo manual ya conocido — no rompe nada que hoy funcione.
