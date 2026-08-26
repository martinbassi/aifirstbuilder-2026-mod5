# RCA FIX-003: Revisar y corregir tests rotos por Title obligatorio en Mural + converters UTC

| Field | Value |
|-------|-------|
| Ticket | FIX-003 |
| Tracker | none |
| Date | 2026-08-25 |
| Related PRD | prd-FEAT-001b.md (actualizado con FR-17/AC-15/AC-16 en este mismo ticket) |

## Contexto

En el commit `9cecf21` ("Se agregó la propiedad Titulo a la entidad Mural - Se agregó un ISO
Converter a EFcore", commiteado directamente en `main`, fuera del pipeline DAW) se agregó la
propiedad `Title` a la entidad `Mural` como campo obligatorio, y se agregaron dos `ValueConverter`
de fechas (`UtcDateTimeConverter` en EF Core, `JsonDateTimeUtcConverter` en la serialización JSON).
El pedido de este ticket fue revisar si estos cambios rompieron la suite de tests existente.

## Causa raíz #1 (confirmada por lectura de código — bloquea tests)

**Componente:** `backend/tests/Paretto.Api.Tests/CreateMuralTests.cs`

El helper privado `BuildMultipartContent(...)` (línea ~211) construye el `multipart/form-data` del
request `POST /api/murals` agregando únicamente `Photo`, `Latitude` y `Longitude`. Nunca agrega el
campo `Title`.

`CreateMuralCommandValidator` (en `CreateMuralCommand.cs`) exige `RuleFor(x => x.Title).NotEmpty()`.
Un request sin `Title` es rechazado con `422 UnprocessableEntity`.

**Efecto:** los 13 tests que usan `BuildMultipartContent` reciben ahora una respuesta distinta a la
que asumían al escribirse. En particular, los 4 tests que afirman `HttpStatusCode.Created` (líneas
262, 359, 413, 438) van a fallar: esperan `201` y van a recibir `422`. El helper de test quedó
desactualizado respecto al contrato nuevo — no es un bug del código de producción, es deuda de test
que el propio commit dejó sin actualizar.

**Cadena de eventos:**
1. El commit agrega `Title` como campo obligatorio en el dominio, la migración EF Core y el
   `CreateMuralCommandValidator`.
2. El commit actualiza el frontend (`create-mural-form.component.ts`, `mural.service.ts`) para
   enviar `title` — el lado cliente quedó consistente.
3. El commit **no** actualiza `CreateMuralTests.cs` — el helper de construcción de requests de test
   quedó con el contrato viejo.
4. Cualquier test que dependa de `BuildMultipartContent` para simular una creación exitosa ahora
   choca con la validación nueva.

## Causa raíz #2 (hallazgo relacionado — no rompe un test existente hoy, pero el converter no está
surtiendo efecto)

**Componente:** `backend/src/Paretto.Api/Program.cs` + `JsonDateTimeUtcConverter.cs`

`JsonDateTimeUtcConverter` se registra vía `builder.Services.ConfigureHttpJsonOptions(...)`, que
configura `Microsoft.AspNetCore.Http.Json.JsonOptions` — las opciones que usan los endpoints de
Minimal API. El proyecto, sin embargo, usa controladores MVC clásicos
(`builder.Services.AddControllers()` + `app.MapControllers()`, ver `MuralsController`,
`DiscoveryController`, etc.), que serializan sus respuestas con
`Microsoft.AspNetCore.Mvc.JsonOptions` — un objeto de configuración **distinto**, que no hereda de
`ConfigureHttpJsonOptions` a menos que se replique explícitamente con
`.AddControllers().AddJsonOptions(...)`.

**Efecto:** el converter (y el `JsonStringEnumConverter`, `DefaultIgnoreCondition` y
`PropertyNamingPolicy` agregados en el mismo bloque) muy probablemente **no se aplican** a las
respuestas reales de `MuralsController`/`DiscoveryController` — el formato de fecha "completo para
el frontend" que el commit buscaba no llega a tener efecto donde se lo necesita. No hay hoy un test
que ejercite este comportamiento (por eso no aparece como una rotura), pero es un gap funcional real
que conviene corregir en este mismo ticket ya que está directamente relacionado con el commit
auditado.

## Componentes afectados

- `backend/tests/Paretto.Api.Tests/CreateMuralTests.cs` (causa #1)
- `backend/src/Paretto.Api/Program.cs` (causa #2)

## PRD relacionado

`docs/daw/prd/prd-FEAT-001b.md` — tenía un gap (no documentaba el título obligatorio). Ya actualizado
en este mismo ticket: FR-17, AC-15, AC-16, `PRD loops` → 1. Validación: PASSED (7/7, 1 warning no
bloqueante — falso positivo conocido del script sobre subsecciones `###`).

## Alcance esperado del fix (a confirmar en PLAN)

1. Actualizar `BuildMultipartContent` en `CreateMuralTests.cs` para incluir `Title` en el request,
   y ajustar los tests que necesiten variar ese campo específicamente (título ausente/excede 50
   caracteres) para cubrir FR-17/AC-15/AC-16.
2. Corregir el wiring del `JsonDateTimeUtcConverter` (y las demás opciones del mismo bloque) para
   que apliquen efectivamente a las respuestas de los controladores MVC.
3. Correr la suite completa (`/daw-test`, ya en CODE) para confirmar que no queda ninguna otra
   rotura no detectada por esta revisión estática.

## Rollback plan

Revertir a `9cecf21^` restaura el comportamiento anterior (sin `Title` obligatorio, sin los
converters de fecha). No es la opción recomendada — el campo `Title` y los converters son cambios
de producto/calidad deseados; el fix es hacer que el resto del sistema (tests, wiring del
converter) sea consistente con ellos, no deshacerlos.
