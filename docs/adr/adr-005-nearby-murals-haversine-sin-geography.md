# ADR-005: Descubrimiento de murales cercanos con bounding box + Haversine, sin `geography`

| Field | Value |
|-------|-------|
| Date | 2026-08-22 |
| Ticket | FEAT-001d |
| Status | Accepted |

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

## Decision

Se elige la **Option A** para este sub-ticket (decisión tomada en PLAN, con el usuario). El índice
`IX_Murals_Status_Latitude_Longitude` (B-tree compuesto sobre `Status`, `Latitude`, `Longitude`) y
`GeoDistanceCalculator` (función pura en `Paretto.Domain`, sin dependencias de EF Core/MediatR:
`HaversineKm` y `BoundingBox`) son la base de `GetNearbyMuralsQuery` (Block 2): primero se acota el
dataset en SQL con el bounding box aproximado, después se calcula la distancia exacta y se filtra en
memoria sobre ese subconjunto ya acotado.

## Consequences

- NFR-01 (tiempo de respuesta del endpoint de descubrimiento) se cumple para el volumen esperado de
  murales por zona de un MVP: el índice evita el scan completo de la tabla, y el cap de 200
  resultados (Block 2) acota el costo de ordenar/serializar en el peor caso.
- No se agrega ninguna dependencia NuGet nueva en este sub-ticket.
- **La Opción B queda anotada explícitamente como mejora futura.** Disparador concreto: cuando los
  tiempos de respuesta reales en producción dejen de cumplir NFR-01 por volumen de murales por zona
  (es decir, cuando el candidate set del bounding box en una región densa empiece a degradar el
  cálculo en memoria de forma medible) — no antes, para no introducir la dependencia y la migración de
  esquema sin una necesidad observada.
- Si en el futuro se migra a la Opción B, el candidato natural para reemplazar es
  `GeoDistanceCalculator` y el índice `IX_Murals_Status_Latitude_Longitude` de este ADR — no
  `GetNearbyMuralsQuery` en sí, cuyo contrato de entrada/salida (lat/lng/radiusKm →
  murales ordenados por distancia) no necesita cambiar.
