# ADR-005: Descubrimiento de murales cercanos — de bounding box + Haversine a `geography`

| Field | Value |
|-------|-------|
| Date | 2026-08-22 (revisado 2026-08-29) |
| Ticket | FEAT-001d (decisión original), FEAT-009 (revisión) |
| Status | Accepted — revisado: se adopta la Option B |

## Context

FEAT-001d agrega un endpoint público que devuelve los murales `Published` dentro de un radio
configurable alrededor de una ubicación, ordenados por distancia. La tabla `Murals` guarda
`Latitude`/`Longitude` como `double` (`float` en SQL Server) simple, sin columna `geography` ni
índice espacial — confirmado en el código (`Mural.cs`, `AppDbContext.cs`) y en el árbol de
dependencias: no hay `NetTopologySuite` ni `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`
en ningún `.csproj` del backend. Hay que decidir cómo calcular proximidad geográfica sobre ese
esquema existente, para el volumen esperado de un MVP.

## Options considered

### Option A: Bounding box en SQL + Haversine en memoria

- **Pros:** sin dependencias NuGet nuevas — usa `Latitude`/`Longitude` tal como están hoy. Un índice
  B-tree compuesto (`Status`, `Latitude`, `Longitude`) permite que SQL Server acote el dataset con un
  seek antes de traer filas a memoria (`GetNearbyMuralsQuery`, Block 2 de este spec); la distancia
  exacta (Haversine) se calcula después, solo sobre ese subconjunto ya acotado, y el resultado se
  ordena y se recorta a 200 registros. Suficiente para el volumen de murales por zona esperado en un
  MVP: el bounding box reduce el candidate set antes de que el cálculo en memoria se vuelva costoso.
- **Cons:** el bounding box es un rectángulo, no un círculo — hay esquinas dentro del box y fuera del
  radio real (se filtran en memoria con `HaversineKm`, sin problema de corrección, solo de que el
  candidate set intermedio es algo más grande que el círculo final). No escala tan bien como un
  índice espacial real si el volumen de murales por zona creciera mucho — el candidate set del
  bounding box crece con la densidad de murales en esa región, no con el resultado final.

### Option B: Migrar a `geography` + `NetTopologySuite`

- **Pros:** más correcto (distancia real desde el motor, sin aproximación de bounding box rectangular)
  y más escalable (índice espacial nativo de SQL Server, pensado para este tipo de consulta).
- **Cons:** requiere una dependencia NuGet nueva
  (`Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`, no justificada hasta ahora en ningún
  spec — AGENTS.md exige justificar cualquier paquete nuevo), una migración que convierte
  `Latitude`/`Longitude` a una columna `geography` (cambio de tipo de dato sobre una tabla con datos
  ya sembrados en tickets anteriores) y un índice espacial real (`CREATE SPATIAL INDEX`), sintaxis y
  comportamiento distintos de los índices B-tree ya usados en el resto del proyecto. Mayor superficie
  de cambio para un beneficio que, para el volumen actual de un MVP, no se traduce en una diferencia
  perceptible de tiempos de respuesta.

## Decision (original, FEAT-001d)

Se eligió la **Option A** para ese sub-ticket (decisión tomada en PLAN, con el usuario). El índice
`IX_Murals_Status_Latitude_Longitude` (B-tree compuesto sobre `Status`, `Latitude`, `Longitude`) y
`GeoDistanceCalculator` (función pura en `Paretto.Domain`, sin dependencias de EF Core/MediatR:
`HaversineKm` y `BoundingBox`) fueron la base de `GetNearbyMuralsQuery`: primero se acotaba el
dataset en SQL con el bounding box aproximado, después se calculaba la distancia exacta y se filtraba
en memoria sobre ese subconjunto ya acotado.

**La Opción B quedó anotada explícitamente como mejora futura**, con un disparador concreto: cuando
los tiempos de respuesta reales en producción dejaran de cumplir NFR-01 por volumen de murales por
zona — no antes, para no introducir la dependencia y la migración de esquema sin una necesidad
observada.

## Revision (FEAT-009, 2026-08-29)

**El disparador original NO se cumplió** — no hay evidencia de degradación de NFR-01 medida en
producción; de hecho, el proyecto todavía no tiene datos de producción reales (MVP no lanzado). La
migración a la **Option B se adopta de todos modos, por decisión explícita del usuario**, como mejora
técnica proactiva (arquitectura correcta a largo plazo) y no como respuesta a un problema medido.
Documentado así, en vez de simplemente cambiar la Decision de arriba, para que quede claro que el
criterio original de "no migrar sin necesidad observada" sigue siendo válido en general — este ticket
es una excepción consciente a ese criterio, no una corrección de un error de juicio anterior.

**Nueva decisión: Option B (`geography` + NetTopologySuite).**

- `Mural.Latitude`/`Longitude` (double) se reemplazan por `Mural.Location` (`Point` de
  NetTopologySuite, SRID 4326), mapeado a `geography` en SQL Server.
- `GeoDistanceCalculator` y el índice `IX_Murals_Status_Latitude_Longitude` se eliminan, tal como este
  mismo ADR ya anticipaba como "el candidato natural para reemplazar" si se migraba a la Opción B.
- El contrato de entrada/salida de `GetNearbyMuralsQuery` (lat/lng/radiusKm → murales ordenados por
  distancia) no cambia, confirmando lo que este ADR ya predecía al respecto.
- Detalle completo de la migración: `docs/daw/specs/spec-FEAT-009.md`.

## Consequences

- Se agregan las dependencias NuGet `NetTopologySuite` (Domain) y
  `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` (Infrastructure) — el motivo que el
  Cons de la Option A original consideraba insuficiente ("no justificada hasta ahora en ningún spec")
  queda resuelto: está justificado en `prd-FEAT-009.md` como el objetivo declarado del ticket.
- Se introduce el primer uso de SQL crudo (`migrationBuilder.Sql(...)`) en una migración de este
  proyecto, para el backfill de datos existentes y la creación del índice espacial
  (`CREATE SPATIAL INDEX`, sin soporte nativo en el Fluent API de EF Core).
- NFR-01 (<3s p95) se mantiene como criterio de aceptación — esta migración no se justifica por una
  mejora numérica medida, sino por la corrección arquitectónica a futuro.
