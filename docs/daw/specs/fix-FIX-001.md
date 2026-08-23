# Fix-plan FIX-001: Configurar CORS para desarrollo local

| Campo | Valor |
|-------|-------|
| Ticket | FIX-001 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-001.md |
| Date | 2026-08-23 |
| Spec loops | 0 |

## Problem

Al correr el frontend (`ng serve`, `http://localhost:4200`) contra la API (`dotnet run`, perfil
`https`, `https://localhost:7126`) en local, el navegador bloquea toda request desde el frontend con:

```
Access to XMLHttpRequest at 'https://localhost:7126/api/auth/register' from origin
'http://localhost:4200' has been blocked by CORS policy: Response to preflight request doesn't
pass access control check: No 'Access-Control-Allow-Origin' header is present on the requested
resource.
```

Esto bloquea cualquier flujo end-to-end probado manualmente contra un navegador real (registro,
login, cualquier endpoint), aunque los tests de integración automatizados sigan pasando.

## Root cause

`backend/src/Paretto.Api/Program.cs` nunca registró `builder.Services.AddCors(...)` ni invocó
`app.UseCors(...)`. Detalle completo en `docs/daw/specs/rca-FIX-001.md`.

## Solution — steps

1. `backend/src/Paretto.Api/Program.cs` — dentro del bloque `if (builder.Environment.IsDevelopment())`
   existente (antes de `var app = builder.Build();`, junto al resto del registro de servicios),
   agregar:
   ```csharp
   if (builder.Environment.IsDevelopment())
   {
       var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
           ?? Array.Empty<string>();
       builder.Services.AddCors(options =>
       {
           // Exclusiva de desarrollo local — producción, cuando exista, necesita su propia
           // policy con el dominio real; no reutilizar "DevelopmentCors" (mitigación R2 del
           // threat model).
           options.AddPolicy("DevelopmentCors", policy =>
               policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader());
       });
   }
   ```
   El `?? Array.Empty<string>()` es una segunda defensa (mitigación R1 del threat model): incluso si
   el bloque se moviera fuera del gate `IsDevelopment()` en el futuro, nunca lanza
   `ArgumentNullException` — registra una policy sin orígenes permitidos (fail-safe: deniega todo en
   vez de crashear el arranque).

2. `backend/src/Paretto.Api/Program.cs` — dentro del bloque `if (app.Environment.IsDevelopment())`
   ya existente (donde hoy se registra Swagger), agregar `app.UseCors("DevelopmentCors");` después de
   `app.UseSwaggerUI();`. La invocación final del bloque queda:
   ```csharp
   if (app.Environment.IsDevelopment())
   {
       app.UseSwagger();
       app.UseSwaggerUI();
       app.UseCors("DevelopmentCors");
   }

   app.UseHttpsRedirection();
   ```
   Nota de orden: `UseCors()` debe ejecutarse antes de `UseAuthentication()`/`UseAuthorization()` y
   antes de `MapControllers()` (documentación oficial de ASP.NET Core) — colocarlo en este punto,
   antes de `UseHttpsRedirection()`/`UseRateLimiter()`/`UseAuthentication()`/`UseAuthorization()`,
   cumple esa restricción.

3. `backend/src/Paretto.Api/appsettings.Development.json` — agregar la sección:
   ```json
   "Cors": {
     "AllowedOrigins": ["http://localhost:4200"]
   }
   ```

4. `backend/src/Paretto.Api/appsettings.json` (base/Production) — **no se toca.** El bloque de
   `AddCors` completo vive dentro de `IsDevelopment()`, así que Production nunca necesita esta
   sección (mitigación R1).

## Dependencies between steps

Ninguna — los 4 pasos son independientes entre sí y pueden aplicarse en cualquier orden; el orden de
arriba es el más legible (servicios → pipeline → configuración → confirmación de que no se toca
Production).

## Error handling

- Si `Cors:AllowedOrigins` falta en `appsettings.Development.json` (por ejemplo, tras un merge que
  la pisó): `Get<string[]>()` devuelve `null`, el `?? Array.Empty<string>()` lo convierte en un array
  vacío, y `WithOrigins()` registra una policy sin orígenes permitidos. El navegador seguirá
  bloqueado por CORS (mismo síntoma original), pero el proceso arranca sin excepción — el fallo es
  visible y diagnosticable, no un crash silencioso.
- Ningún otro código de error nuevo: `UseCors` solo agrega el header `Access-Control-Allow-Origin`
  (y responde el preflight `OPTIONS`) cuando el origen coincide con la lista permitida; en cualquier
  otro caso, el comportamiento es el mismo que hoy (sin ese header, el navegador bloquea la lectura
  de la respuesta — la request al servidor sí se procesa igual, la autorización real sigue
  ocurriendo en `UseAuthentication()`/`UseAuthorization()`, sin cambios).

## Tests

- [ ] **Regression test** — `backend/tests/Paretto.Api.Tests/CorsTests.cs`,
  `Request_with_Origin_localhost_4200_in_Development_receives_Access_Control_Allow_Origin_header`:
  usa `WebApplicationFactory<Program>` con `.UseEnvironment("Development")`, envía una request con
  header `Origin: http://localhost:4200` a un endpoint público existente (p. ej.
  `GET /api/discovery/nearby-murals`), y afirma que la respuesta incluye
  `Access-Control-Allow-Origin: http://localhost:4200`. Reproduce el bug original: sin el fix, ese
  header está ausente.
- [ ] **Sad path** — `Request_with_a_different_Origin_does_not_receive_Access_Control_Allow_Origin_header`:
  misma request, con `Origin: http://evil.example.com`, afirma que el header
  `Access-Control-Allow-Origin` está ausente de la respuesta (la policy no lo agrega para orígenes
  fuera de la whitelist).
- [ ] **Production safety** — `AddCors_is_not_registered_when_the_host_runs_outside_Development`:
  usa `WebApplicationFactory<Program>` con `.UseEnvironment("Production")` y afirma que el host
  arranca sin excepción (verifica la mitigación R1: ausencia de `Cors:AllowedOrigins` en
  `appsettings.json` base no rompe el arranque en Production).

## Regression risk

**Bajo.** El cambio es aditivo (nuevo bloque `AddCors`/`UseCors`, gateado por `IsDevelopment()`) y no
modifica ningún endpoint, controller, ni el pipeline de autenticación/autorización existente. El
único riesgo real —crash en Production por config faltante— ya está cubierto por el diseño (gate +
fallback) y por el test de "Production safety" de arriba.

## Rollback plan

- **Trivial: revertir el commit.** El cambio se limita a 2 archivos (`Program.cs`,
  `appsettings.Development.json`), ambos aditivos — revertir el commit deja el comportamiento
  exactamente como estaba antes del fix (bloqueado por CORS en desarrollo local, sin efecto en
  ningún otro entorno).
- **Indicadores para aplicarlo:** si tras el fix algún endpoint expuesto solo por sesión/token
  empezara a responder distinto para requests cross-origin no autenticadas (no debería, ya que
  `UseAuthentication()`/`UseAuthorization()` siguen corriendo después de `UseCors()` sin cambios),
  o si el arranque en cualquier entorno lanzara una excepción nueva relacionada a `Cors`.

## Evidencia TDD

Los 3 tests de `CorsTests.cs` se escribieron completos antes de tocar `Program.cs`/
`appsettings.Development.json`. Para dejar constancia del rojo→verde (hallazgo de VERIFY ronda 1,
`docs/daw/reports/verify-FIX-001.md`), se revirtió temporalmente el fix con `git stash` (dejando
`CorsTests.cs` en su lugar) y se corrió `dotnet test --filter "FullyQualifiedName~CorsTests"`:

**Sin el fix (rojo):**
```
[FAIL] Request_with_Origin_localhost_4200_in_Development_receives_Access_Control_Allow_Origin_header
Mensaje de error: Expected Access-Control-Allow-Origin header to be present.
Con error: 1, Superado: 2, Omitido: 0, Total: 3
```

- `Request_with_Origin_localhost_4200_in_Development_receives_Access_Control_Allow_Origin_header`
  → **FAIL**. Es el test que reproduce el bug original: sin `AddCors`/`UseCors`, ASP.NET Core nunca
  agrega `Access-Control-Allow-Origin` a la respuesta.
- `Request_with_a_different_Origin_does_not_receive_Access_Control_Allow_Origin_header` → pasaba
  igual sin el fix (sin CORS activo, ningún origen recibe el header — trivialmente cierto, no
  ejercita la corrección en sí, ejercita la mitigación R3 del threat model).
- `AddCors_is_not_registered_when_the_host_runs_outside_Development` → pasaba igual sin el fix (sin
  `AddCors` registrado en absoluto, el host arranca sin excepción en cualquier entorno — cierto con
  o sin el fix, ejercita la mitigación R1).

**Con el fix restaurado (`git stash pop`) — verde:**
```
Correctas! - Con error: 0, Superado: 3, Omitido: 0, Total: 3
```

Solo el primer test es el que efectivamente detecta la regresión (rojo→verde); los otros dos
validan mitigaciones del threat model que ya se cumplían por razones distintas antes del fix
(ausencia total de CORS / ausencia total de `AddCors`), no el bug en sí — están para que una
regresión futura en esas mitigaciones específicas también quede cubierta.
