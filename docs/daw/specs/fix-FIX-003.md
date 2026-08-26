# Fix-plan FIX-003: Revisar y corregir tests rotos por Title obligatorio en Mural + converters UTC

| Field | Value |
|-------|-------|
| Ticket | FIX-003 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-003.md |
| Date | 2026-08-26 |
| Spec loops | 0 |

## Problem

El commit `9cecf21` (hecho directo en `main`, fuera del pipeline DAW) agregó `Title` como campo
obligatorio en la entidad `Mural` y dos `ValueConverter` de fechas UTC. Al revisar si esto rompió la
suite de tests existente, se confirmaron dos causas:

1. 4 tests de `CreateMuralTests.cs` que esperan `201 Created` reciben `422 UnprocessableEntity` en su
   lugar, porque el helper de construcción del request nunca envía `Title`.
2. El converter de fechas UTC pensado para las respuestas de la API está registrado en el
   `JsonOptions` equivocado y no aplica a ningún endpoint real — un hallazgo relacionado, no una
   rotura de test existente, pero directamente atado al mismo commit auditado.

## Root cause

**Causa #1:** `CreateMuralTests.cs::BuildMultipartContent` (línea ~211) construye el
`multipart/form-data` de `POST /api/murals` agregando solo `Photo`, `Latitude` y `Longitude`.
`CreateMuralCommandValidator` exige `RuleFor(x => x.Title).NotEmpty().MaximumLength(50)` desde el
commit `9cecf21`. El helper de test quedó desactualizado respecto al contrato nuevo — no es un bug
de producción, es deuda de test que el propio commit dejó sin actualizar.

**Causa #2:** `JsonDateTimeUtcConverter` se registra vía `builder.Services.ConfigureHttpJsonOptions(...)`
(`Program.cs:154`), que configura `Microsoft.AspNetCore.Http.Json.JsonOptions` — las opciones que usan
los endpoints de Minimal API. El proyecto usa controladores MVC clásicos
(`AddControllers()`/`MapControllers()`: `MuralsController`, `DiscoveryController`, `AuthController`,
`ModerationController`), que serializan con `Microsoft.AspNetCore.Mvc.JsonOptions` — un objeto de
configuración distinto que no hereda de `ConfigureHttpJsonOptions`. El converter (y el resto de la
config del mismo bloque) nunca llega a aplicarse a una respuesta real.

## Solution — steps

1. `backend/tests/Paretto.Api.Tests/CreateMuralTests.cs:211` — agregar el parámetro
   `string title = "Mural de prueba"` a `BuildMultipartContent`, e incluirlo en el contenido:
   `content.Add(new StringContent(title), "Title");`.
2. `backend/tests/Paretto.Api.Tests/CreateMuralTests.cs` — las 12 invocaciones reales de
   `BuildMultipartContent` (líneas 258, 298, 313, 328, 355, 382, 394, 409, 434, 462, 484, 507) siguen
   compilando sin cambios (el nuevo parámetro tiene default). No requieren edición salvo que un test
   necesite forzar un valor de `title` específico (ver paso 3).
3. `backend/tests/Paretto.Api.Tests/CreateMuralTests.cs` — agregar 2 tests nuevos junto a los
   existentes de creación (cubren FR-17/AC-15/AC-16 de `prd-FEAT-001b.md`):
   - `CreateMural_SinTitle_Retorna422`: llama `BuildMultipartContent(..., title: "")` (o construye el
     multipart sin el campo `Title`) → assert `HttpStatusCode.UnprocessableEntity`.
   - `CreateMural_TitleExcede50Caracteres_Retorna422`: llama `BuildMultipartContent(..., title: new
     string('a', 51))` → assert `HttpStatusCode.UnprocessableEntity`.
4. `backend/src/Paretto.Api/Program.cs:154` (junto al bloque de `ConfigureHttpJsonOptions`) —
   agregar:
   ```csharp
   builder.Services.AddControllers().AddJsonOptions(options =>
   {
       options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
       options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
       options.JsonSerializerOptions.Converters.Add(new JsonDateTimeUtcConverter());
   });
   ```
   `PropertyNamingPolicy.CamelCase` no se replica: ya es el default de `AddControllers()` en .NET, y
   agregarlo sería redundante — solo se replica lo que efectivamente cambia el comportamiento
   default.
5. `backend/tests/Paretto.Api.Tests/GetMuralByIdTests.cs` — agregar un assert (regex
   `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$`) sobre el campo `createdAt` del JSON de respuesta.
6. `backend/tests/Paretto.Api.Tests/GetNearbyMuralsTests.cs` — mismo assert sobre `createdAt` en los
   items del array de respuesta.
7. `backend/tests/Paretto.Api.Tests/LoginTests.cs` — mismo assert sobre `expiresAt` en la respuesta
   de login (gap detectado por el impact scan: el cambio de `AddJsonOptions` es global, no solo
   afecta a Murals).

## Dependencies between steps

Pasos 1-3 (Fix A) y pasos 4-7 (Fix B) son independientes entre sí — pueden implementarse en
cualquier orden. Dentro de cada grupo: el paso 1 debe preceder al 3 (los tests nuevos usan el
parámetro `title` que el paso 1 agrega); el paso 4 debe preceder a los pasos 5-7 (los tests nuevos
verifican el comportamiento que el paso 4 habilita).

## Error handling

- `CreateMuralCommandValidator` ya maneja el caso de `Title` ausente/excedido con un mensaje de
  validación explícito (`RuleFor(x => x.Title).NotEmpty().MaximumLength(50)`) — este fix no toca esa
  lógica, solo agrega cobertura de test sobre un comportamiento que ya existe en producción.
- El `AddJsonOptions` nuevo no introduce manejo de errores adicional: es configuración de
  serialización, no lógica de negocio.

## Tests

- [ ] **Regression test (causa #1)** — los 4 tests existentes que assertan `HttpStatusCode.Created`
  (líneas 262, 359, 413, 438 de `CreateMuralTests.cs`) fallan ANTES del fix (reciben 422) y pasan
  DESPUÉS.
- [ ] `CreateMural_SinTitle_Retorna422` — nuevo, AC-15.
- [ ] `CreateMural_TitleExcede50Caracteres_Retorna422` — nuevo, AC-15.
- [ ] `GetMuralById_CreatedAt_TieneFormatoUtcCompleto` — nuevo, cubre causa #2 sobre
  `GetMuralByIdQuery`.
- [ ] `GetNearbyMurals_CreatedAt_TieneFormatoUtcCompleto` — nuevo, cubre causa #2 sobre
  `GetNearbyMuralsQuery`.
- [ ] `Login_ExpiresAt_TieneFormatoUtcCompleto` — nuevo, cubre el gap de `LoginCommand.ExpiresAt`
  detectado por el impact scan.
- [ ] Suite completa (backend + frontend) vía `/daw-test` en CODE, no solo el área tocada — el
  threat model recomienda esto explícitamente dado el blast radius global del paso 4.

## Regression risk

**Low.** Fix A es exclusivamente código de test (sin superficie de producción). Fix B es un cambio
de serialización JSON de bajo blast radius: el impact scan confirmó que ninguna `*Response` DTO
expone una propiedad de tipo enum (así que `JsonStringEnumConverter` no cambia ningún campo
existente), no hay asserts existentes que dependan de que un campo `null` esté presente en el JSON
(así que `DefaultIgnoreCondition.WhenWritingNull` no rompe nada), y se verificó manualmente que el
frontend consume `createdAt`/`expiresAt` vía `new Date(...)` / el pipe `date` de Angular, ambos
compatibles con el nuevo formato ISO 8601 `yyyy-MM-ddTHH:mm:ssZ`.

## Rollback plan

Revertir el commit de este fix:
- Restaura `BuildMultipartContent` sin `Title` → los 4 tests de `HttpStatusCode.Created` vuelven a
  fallar con 422 (el estado reportado originalmente por el usuario).
- Remueve el `AddJsonOptions` de `Program.cs` → el converter UTC vuelve a no aplicar a los
  controllers (sin romper nada existente, ya que hoy tampoco aplica).
- Sin impacto en datos persistidos: los `ValueConverter` de EF Core (`UtcDateTimeConverter`,
  registrado model-wide en `AppDbContext.ConfigureConventions`) no se tocan en este fix y siguen
  aplicando igual antes y después del rollback.

**Indicadores para aplicar el rollback:** si tras el fix la suite completa (`/daw-test` en CODE)
revela una rotura no anticipada fuera del área de Murals/Login relacionada con el formato de fecha
global, o si VERIFY detecta que algún consumidor del frontend no cubierto por este plan depende del
formato de fecha anterior.
