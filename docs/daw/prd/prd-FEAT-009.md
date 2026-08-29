# PRD FEAT-009: Migrar búsqueda de murales cercanos a geography + NetTopologySuite

| Field | Value |
|-------|-------|
| Ticket | FEAT-009 |
| Tracker | none |
| Date | 2026-08-29 |
| PRD loops | 0 |

## Context and Problem

La búsqueda de murales cercanos (`GET /api/discovery/nearby-murals`, introducida en FEAT-001d) calcula
distancias con la fórmula de Haversine en memoria (`GeoDistanceCalculator.HaversineKm`), acotando
primero el dataset con un bounding box en SQL y un índice B-tree compuesto
(`IX_Murals_Status_Latitude_Longitude`). Esta decisión está documentada en **ADR-005**, que evaluó
explícitamente migrar a `geography` + NetTopologySuite y la descartó para el MVP, dejando como
condición de disparo *"cuando los tiempos de respuesta reales en producción dejen de cumplir NFR-01
por volumen de murales por zona"*.

Esa condición no se cumplió todavía — no hay evidencia de degradación de performance medida en
producción. Este ticket adelanta la migración de todos modos, como mejora técnica proactiva
(decisión explícita del usuario), no como respuesta a un problema real. **ADR-005 se actualiza en el
mismo lugar (in-place)** para reflejar esta decisión revisada, en vez de crear un ADR nuevo que lo
supersede.

## Goals

- Reemplazar el almacenamiento de la ubicación del mural (`Latitude`/`Longitude` como columnas
  `double` sueltas) por una columna `geography` (tipo `Point` de NetTopologySuite) como única fuente
  de verdad interna.
- Reemplazar el cálculo de distancia en memoria (Haversine + bounding box) por una consulta espacial
  nativa de SQL Server, acelerada por un índice espacial.
- No introducir ningún cambio visible en el contrato público del endpoint ni en el comportamiento
  observable de la búsqueda (mismos resultados, mismo orden, mismos campos de la respuesta).
- Eliminar el código que queda obsoleto (`GeoDistanceCalculator`, el índice B-tree anterior).

## Functional Requirements

- FR-01: El sistema debe almacenar la ubicación del mural como `geography` (`Point` de
  NetTopologySuite), reemplazando las columnas `Latitude`/`Longitude` de la entidad `Mural`.
- FR-02: El sistema debe calcular la distancia entre el punto de búsqueda y cada mural mediante una
  consulta espacial nativa de SQL Server, en vez de Haversine calculado en memoria.
- FR-03: El sistema debe crear un índice espacial sobre la nueva columna `geography` para acelerar la
  búsqueda por cercanía.
- FR-04: El request y la respuesta públicos de `GET /api/discovery/nearby-murals` deben mantener
  exactamente los mismos campos y tipos que hoy (`Latitude`/`Longitude` como `double` sueltos,
  `DistanceKm` en kilómetros), sin ningún cambio de contrato.
- FR-05: El flujo de creación de mural (`CreateMuralCommand`) debe persistir la ubicación como un
  `Point` construido a partir de la latitud/longitud recibidas, sin cambiar el contrato de entrada de
  ese endpoint.
- FR-06: La migración de EF Core debe volcar (backfill) los valores existentes de
  `Latitude`/`Longitude` a la nueva columna `geography` antes de eliminar esas columnas.
- FR-07: El sistema debe eliminar `GeoDistanceCalculator` (Haversine + bounding box) y el índice
  `IX_Murals_Status_Latitude_Longitude` una vez completada la migración.
- FR-08: **ADR-005** debe actualizarse in-place para reflejar esta decisión revisada, documentando
  por qué se migra ahora sin que se haya cumplido la condición de disparo que el propio ADR definía
  originalmente.

## Non-Functional Requirements

- NFR-01: La búsqueda de murales cercanos debe seguir mostrando resultados en menos de 3 segundos
  para el 95% de las consultas (mismo umbral que el NFR-01 de `prd-FEAT-001d.md`; la migración no
  debe degradarlo). No se exige una mejora numérica sobre el umbral actual, dado que no hay volumen
  real todavía para medirla de forma significativa.

## Acceptance Criteria

- AC-01: WHEN se solicita una búsqueda de murales cercanos con los mismos parámetros de entrada
  (latitud, longitud, radio) que antes de la migración, THE sistema SHALL devolver un resultado
  equivalente: los mismos murales, en el mismo orden por distancia, con el mismo `DistanceKm` dentro
  de una tolerancia menor a 0.01 km (10 metros) respecto al valor calculado por Haversine (FR-02,
  FR-04).
- AC-02: WHEN se inspecciona el contrato de `GET /api/discovery/nearby-murals` después de la
  migración, THE sistema SHALL exponer exactamente los mismos campos y tipos que antes
  (`Latitude`/`Longitude` como `double`, `DistanceKm` en km), sin cambios visibles (FR-04).
- AC-03: WHEN se crea un mural nuevo, THE sistema SHALL persistir su ubicación como un `Point`
  `geography` construido a partir de la latitud/longitud recibidas en la request (FR-01, FR-05).
- AC-04: WHEN la migración de EF Core corre contra una base con filas de murales ya existentes, THE
  sistema SHALL volcar la columna `geography` a partir de los valores de `Latitude`/`Longitude`
  existentes antes de eliminar esas columnas (FR-06).
- AC-05: IF una fila existente tiene valores de `Latitude`/`Longitude` fuera del rango válido para
  `geography` (coordenadas WGS84 inválidas), THEN THE migración SHALL fallar de forma explícita (no
  silenciar ni corromper la fila), para que se corrija el dato antes de continuar (FR-06).
- AC-06: WHEN la migración termina, THE sistema SHALL tener un índice espacial sobre la columna
  `geography`, y el índice `IX_Murals_Status_Latitude_Longitude` anterior SHALL dejar de existir
  (FR-03, FR-07).
- AC-07: WHEN se busca `GeoDistanceCalculator` en el código después de este ticket, THE sistema
  SHALL no mostrar ninguna referencia (eliminación completa, FR-07).
- AC-08: WHEN se revisa ADR-005 después de este ticket, THE sistema SHALL reflejar la decisión
  actualizada (adopción de `geography`) con la justificación de por qué se hace antes de que se
  cumpliera la condición de disparo original (FR-08).

## Out of Scope

- Cambios al frontend (mapa Leaflet, lista de discovery) — el contrato público no cambia, así que no
  hay nada que ajustar ahí.
- Cambios al radio de búsqueda por defecto o a cualquier otro comportamiento observable del
  descubrimiento de murales.
- Crear un ADR nuevo (ADR-007) que supersede a ADR-005 — se edita in-place por decisión explícita del
  usuario.
- Definir un umbral de performance más estricto que el NFR-01 actual — se mantiene el mismo, no se
  exige una mejora numérica medida.
- Cambios al contrato de entrada/salida de `CreateMuralCommand` (solo cambia cómo se persiste
  internamente, no qué recibe/devuelve el endpoint).

## Risks and Mitigations

- **Riesgo:** el tipo `geography` de SQL Server valida coordenadas de forma más estricta que un
  `double` suelto; una fila con `Latitude`/`Longitude` fuera de rango rompería la construcción del
  `Point` durante el backfill.
  **Mitigación:** la migración falla explícitamente ante ese caso (AC-05), en vez de silenciarlo o
  truncar el valor.
- **Riesgo:** `NetTopologySuite.Point` ordena sus coordenadas como `(X=longitud, Y=latitud)`, al
  revés de como se suele pensar `(latitud, longitud)` — un swap accidental de ejes rompería todos los
  cálculos de distancia sin que ningún test lo note si no se verifica explícitamente.
  **Mitigación:** a definir en PLAN — tests unitarios que verifiquen la asignación correcta de
  ejes al construir el `Point`.
- **Riesgo:** las funciones espaciales de SQL Server (`STDistance`) devuelven metros, no kilómetros —
  un error de conversión de unidades rompería silenciosamente `DistanceKm` sin fallar ningún test que
  no compare contra un valor conocido.
  **Mitigación:** a definir en PLAN — test con una distancia conocida (ej. dos puntos a exactamente
  1km) verificando la conversión metros→km.
- **Riesgo:** `NetTopologySuite` y
  `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` son dependencias NuGet nuevas.
  **Mitigación:** justificadas porque son, literalmente, el objetivo declarado de este ticket — no
  hay alternativa dentro del stack actual para modelar `geography` vía EF Core.

## Dependencies

- `NetTopologySuite` y `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` (nuevas
  dependencias NuGet).
- `ADR-005` (`docs/adr/adr-005-nearby-murales-haversine-sin-geography.md`), a actualizar in-place.
- Entidad `Mural` (`backend/src/Paretto.Domain/Entities/Mural.cs`), `AppDbContext`
  (`backend/src/Paretto.Infrastructure/Data/AppDbContext.cs`), `GetNearbyMuralsQuery.cs` y
  `CreateMuralCommand.cs` (`backend/src/Paretto.Api/Features/...`).
