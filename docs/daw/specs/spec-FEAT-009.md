# Spec FEAT-009: Migrar búsqueda de murales cercanos a geography + NetTopologySuite

| Field | Value |
|-------|-------|
| Ticket | FEAT-009 |
| PRD | docs/daw/prd/prd-FEAT-009.md |
| Tier | FEATURE |
| Date | 2026-08-29 |
| Spec loops | 0 |

## Summary

Reemplaza `Mural.Latitude`/`Longitude` (double) por `Mural.Location` (`Point` de NetTopologySuite,
SRID 4326, mapeado a `geography`). Para que el remapeo de `Latitude`/`Longitude` en las respuestas
públicas (`DiscoveryMappingConfig.cs`, `MuralMappingConfig.cs`) siga funcionando por convención de
Mapster **sin tocar esos dos archivos**, `Mural` gana propiedades computadas de solo lectura
(`Latitude => Location.Y`, `Longitude => Location.X`, ignoradas por EF) y un factory estático
`Mural.CreateLocation(lat, lon)` que centraliza en un único lugar la conversión entre el orden
`(lat, lon)` en que el resto del sistema piensa las coordenadas y el orden `(X=lon, Y=lat)` que usa
`Point` de NetTopologySuite (hallazgo del arch-auditor en PLAN: sin esto, la conversión quedaba
dispersa en 4 archivos). La búsqueda de cercanía pasa de Haversine+bounding box en memoria a una
consulta espacial vía LINQ-to-Entities (`Location.Distance(...)`, nunca SQL crudo con input de
usuario — mitigación del riesgo R1 del threat model). ADR-005 ya fue actualizado in-place en PLAN.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 |
| FR-02 | Block 2 |
| FR-03 | Block 1 |
| FR-04 | Block 1 (propiedades computadas), Block 2 (misma forma de respuesta), Block 3 (test end-to-end) |
| FR-05 | Block 1 |
| FR-06 | Block 1 |
| FR-07 | Block 1 (índice viejo), Block 2 (`GeoDistanceCalculator`) |
| FR-08 | Block 3 (verificación — el contenido ya fue actualizado en PLAN, este bloque lo confirma con un chequeo automatizado) |
| NFR-01 | Strategy: el índice espacial reemplaza al índice B-tree + Haversine en memoria como mecanismo de cumplimiento de <3s p95; no se mide una mejora numérica, solo se exige no degradar el umbral existente |
| AC-01 | Block 2 |
| AC-02 | Block 3 |
| AC-03 | Block 1 |
| AC-04 | Block 1 |
| AC-05 | Block 1 |
| AC-06 | Block 1 |
| AC-07 | Block 2 |
| AC-08 | Block 3 |

## Dependencies between blocks

Block 2 depende de Block 1 (usa `Mural.Location`/`Mural.CreateLocation` ya existentes). Block 3
depende de Block 1 y Block 2 (actualiza fixtures de test que construyen `Mural` y verifica el
comportamiento end-to-end de la query ya migrada). Orden de ejecución: Block 1 → Block 2 → Block 3.

## Block 1 — Domain + Infrastructure (schema, persistencia, factory)

**Files**
- `backend/src/Paretto.Domain/Entities/Mural.cs` (modified) — reemplaza `Latitude`/`Longitude`
  (double) por `Location` (`NetTopologySuite.Geometries.Point`, no nullable); agrega propiedades
  computadas de solo lectura `Latitude => Location.Y` y `Longitude => Location.X`; agrega
  `public static Point CreateLocation(double latitude, double longitude) => new Point(longitude,
  latitude) { SRID = 4326 };` — **único punto del código C# donde se decide el orden de ejes**
  (todo lo demás en C# debe llamar a este factory, nunca construir un `Point` a mano).
- `backend/src/Paretto.Infrastructure/Data/AppDbContext.cs` (modified) — en el registro de
  `UseSqlServer(...)`, encadenar `.UseNetTopologySuite()`; en `OnModelCreating`, bloque
  `modelBuilder.Entity<Mural>`: reemplazar `entity.Property(m => m.Latitude/Longitude).IsRequired()`
  por `entity.Property(m => m.Location).HasColumnType("geography").IsRequired();
  entity.Ignore(m => m.Latitude); entity.Ignore(m => m.Longitude);`; quitar el
  `entity.HasIndex(m => new { m.Status, m.Latitude, m.Longitude })...` existente.
- `backend/src/Paretto.Infrastructure/Data/AppDbContextFactory.cs` (modified) — agregar
  `.UseNetTopologySuite()` al mismo `UseSqlServer(...)` de tiempo de diseño (gap del Impact Scan: sin
  esto, `dotnet ef migrations add`/`database update` puede fallar al generar/aplicar la migración de
  este bloque).
- `backend/src/Paretto.Domain/Paretto.Domain.csproj` (modified) — agregar `PackageReference` a
  `NetTopologySuite`, con un comentario inline explicando que es una librería de geometría pura (sin
  EF Core/MediatR), justificada por `prd-FEAT-009.md` — mismo patrón de comentario justificativo que
  ya usan `Azure.Storage.Blobs`/`NsfwSpy` en `Paretto.Infrastructure.csproj`.
- `backend/src/Paretto.Infrastructure/Paretto.Infrastructure.csproj` (modified) — agregar
  `PackageReference` a `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` (misma versión que
  el resto de `Microsoft.EntityFrameworkCore.*`, `10.0.11`), con comentario justificativo análogo.
- `backend/src/Paretto.Infrastructure/Data/Migrations/{timestamp}_MuralLocationGeography.cs` (new)
  — ver "Data model" abajo para la secuencia exacta de `Up()`/`Down()`.
- `backend/src/Paretto.Api/Features/Murals/Commands/CreateMuralCommand.cs` (modified) — al construir
  el `Mural` a persistir, usar `Location = Mural.CreateLocation(request.Latitude, request.Longitude)`
  en vez de asignar `Latitude`/`Longitude` directamente (que ahora son de solo lectura).
- `backend/tests/Paretto.Api.Tests/MuralPersistenceTests.cs` (modified) — reemplazar
  `new Mural { Latitude = ..., Longitude = ..., ... }` por
  `new Mural { Location = Mural.CreateLocation(lat, lon), ... }`; el assert
  `Assert.Equal(-34.6037, persisted.Latitude)` sigue funcionando tal cual gracias a la propiedad
  computada.

**Data model**

- Entidad `Mural`: la columna `Location` es `geography` (`Point`), `NOT NULL`, SRID 4326. Las
  columnas `Latitude`/`Longitude` (`float`, `NOT NULL`) se eliminan. El índice
  `IX_Murals_Status_Latitude_Longitude` se elimina; se crea un índice espacial sobre `Location`
  (nombre sugerido: `SPATIAL_IX_Murals_Location`).
- **Secuencia exacta dentro de `Up()`** (hallazgo del arch-auditor: el orden importa porque los
  tests de persistencia corren contra SQL Server real, no en memoria):
  1. `migrationBuilder.AddColumn<Point>("Location", "Murals", type: "geography", nullable: true);`
  2. `migrationBuilder.Sql("UPDATE Murals SET Location = geography::Point(Latitude, Longitude, 4326);");`
     — comentario inline explicando que este es el backfill de AC-04, y que si alguna fila tiene
     coordenadas fuera de rango WGS84, `geography::Point` lanza un error de SQL Server y el `Up()`
     entero falla (AC-05: falla explícita, no silenciosa).
  3. `migrationBuilder.AlterColumn<Point>("Location", "Murals", type: "geography", nullable: false, oldNullable: true);`
  4. `migrationBuilder.Sql("CREATE SPATIAL INDEX SPATIAL_IX_Murals_Location ON Murals(Location) USING GEOGRAPHY_AUTO_GRID;")`
     — comentario inline explicando que es SQL crudo porque el Fluent API de EF Core no tiene soporte
     nativo para `CREATE SPATIAL INDEX` (primer uso de SQL crudo en una migración de este proyecto).
  5. `migrationBuilder.DropIndex("IX_Murals_Status_Latitude_Longitude", "Murals");`
  6. `migrationBuilder.DropColumn("Latitude", "Murals");`
  7. `migrationBuilder.DropColumn("Longitude", "Murals");`
- **`Down()` simétrico** (orden inverso): recrear `Latitude`/`Longitude` nullable → backfill inverso
  desde `Location.Lat`/`Location.Long` → `AlterColumn` not null → recrear el índice B-tree viejo →
  `DROP SPATIAL INDEX` (SQL crudo) → `DropColumn("Location")`.
- EF Core envuelve cada migración en una transacción por defecto — el spec NO debe desactivar ese
  comportamiento (`SuppressTransactionalMigration` no se usa en ningún lado de este bloque), para que
  un fallo a mitad de camino (ej. AC-05) revierta todo el `Up()` en vez de dejar la tabla a medio
  migrar (mitigación de R3 del threat model).

**Input validation**

- Sin cambios: `CreateMuralCommandValidator` sigue validando rango de latitud/longitud
  (-90..90/-180..180) antes de que el Handler llegue a construir el `Point` — el factory
  `Mural.CreateLocation` siempre recibe coordenadas ya validadas en el flujo de creación de mural.

**Error handling**

- Si la migración encuentra una fila con coordenadas fuera de rango WGS84 durante el backfill, SQL
  Server lanza una excepción en el paso 2 de `Up()` y toda la migración se revierte (transacción por
  defecto) — no hay manejo de errores adicional que agregar en C#, es responsabilidad de la
  transacción de la migración.

**Required tests**

- [ ] `Mural.CreateLocation(lat, lon)` con lat≠lon (ej. -34.6037, -58.3816) → `Location.Y` es la
      latitud y `Location.X` es la longitud (no al revés) — valida el eje correcto, mitigación de R2
      del threat model.
- [ ] Persistir un `Mural` con `Location = Mural.CreateLocation(...)` y releerlo desde la base real →
      `persisted.Latitude`/`persisted.Longitude` (propiedades computadas) devuelven los mismos
      valores originales — valida AC-03, round-trip de persistencia.
- [ ] Test de migración: aplicar las migraciones hasta la anterior a esta, insertar una fila con
      `Latitude`/`Longitude` válidos vía SQL crudo de test, aplicar esta migración, y verificar que
      `Location` quedó pobladada correctamente desde esos valores — valida AC-04 (backfill).
- [ ] Test de migración: repetir el test anterior pero con una fila con `Latitude` fuera de rango
      (ej. 200) antes de aplicar esta migración → la migración lanza una excepción y no se aplica —
      valida AC-05 (falla explícita ante datos inválidos).
- [ ] `AppDbContextFactory` produce un contexto que puede generar/aplicar esta migración sin error
      (smoke test de que `.UseNetTopologySuite()` está presente en el factory de diseño, no solo en
      runtime) — valida AC-06.

**Completion criterion**

Los 5 tests de este bloque pasan; `dotnet build` limpio; la migración aplicada contra una base
limpia y contra una base con datos preexistentes produce el mismo resultado final (columna `Location`
poblada, columnas viejas e índice viejo ausentes, índice espacial presente).

## Block 2 — Query layer

**Files**
- `backend/src/Paretto.Api/Features/Discovery/Queries/GetNearbyMuralsQuery.cs` (modified) —
  reemplazar el uso de `GeoDistanceCalculator.BoundingBox`/`HaversineKm` por: construir
  `var searchPoint = Mural.CreateLocation(query.Latitude, query.Longitude);` y filtrar/ordenar vía
  LINQ-to-Entities: `.Where(m => m.Location.Distance(searchPoint) <= radiusKm * 1000)`
  `.OrderBy(m => m.Location.Distance(searchPoint))`, con `DistanceKm` calculado como
  `m.Location.Distance(searchPoint) / 1000` en la proyección final. **Nunca SQL crudo con
  interpolación de `lat`/`lng`/`radius`** — LINQ-to-Entities parametriza automáticamente
  (mitigación de R1 del threat model, no negociable). Se elimina el cap manual de bounding box (el
  índice espacial ya acota el candidate set); se mantiene el cap `MaxResults = 200` existente sobre
  el resultado final.
- `backend/src/Paretto.Domain/Services/GeoDistanceCalculator.cs` (deleted).
- `backend/tests/Paretto.Api.Tests/GeoDistanceCalculatorTests.cs` (deleted).
- `backend/tests/Paretto.Api.Tests/GetNearbyMuralsTests.cs` (modified) — actualizar todo seed de
  murales para usar `Mural.CreateLocation(...)`; usar coordenadas con latitud≠longitud claramente
  distinguibles en los tests existentes (mitigación de R2); agregar un test de "distancia conocida"
  (dos puntos a ~1km exacto de separación) que verifique la conversión metros→km.

**Logic**

La query espacial reemplaza 1:1 el comportamiento de bounding box + Haversine: mismo contrato de
entrada (lat/lng/radiusKm), mismo contrato de salida (lista ordenada por distancia ascendente, cap de
200, `DistanceKm` en km). El índice espacial creado en Block 1 acelera el filtro `.Where(...)` sin
necesidad de un bounding box manual — SQL Server lo resuelve internamente.

**Input validation**

- Sin cambios: `GetNearbyMuralsQueryValidator` sigue validando rango de latitud/longitud
  (-90..90/-180..180) y radio antes de que el Handler construya `searchPoint` — este bloque no
  modifica el validador, solo qué hace el Handler con el input ya validado.

**Error handling**

- Sin cambios de casos de error respecto a la implementación anterior: `GetNearbyMuralsQueryValidator`
  ya rechaza lat/lng/radius fuera de rango antes de que el Handler construya la query.

**Required tests**

- [ ] Con murales sembrados a distintas distancias conocidas, la búsqueda devuelve el mismo conjunto
      y el mismo orden que la implementación anterior (test de equivalencia/regresión) — valida AC-01.
- [ ] Dos puntos a ~1km exacto de separación → `DistanceKm` ≈ 1.0 (tolerancia <0.01) — valida la
      conversión metros→km, mitigación de R2/AC-01.
- [ ] Ningún mural fuera del radio solicitado aparece en el resultado (ya existente, adaptado al
      nuevo storage) — valida AC-01.
- [ ] `grep -r "GeoDistanceCalculator" backend/` no devuelve resultados tras este bloque — valida
      AC-07.

**Completion criterion**

Los 4 tests de este bloque pasan; `GeoDistanceCalculator.cs` y su test ya no existen en el árbol;
`dotnet build`/`dotnet test` sin referencias rotas.

## Block 3 — Fixtures de test restantes y verificación end-to-end

**Files**
- `backend/tests/Paretto.Api.Tests/GetMuralByIdTests.cs` (modified) — `SeedMuralAsync` usa
  `Mural.CreateLocation(...)` en vez de asignar `Latitude`/`Longitude` directo.
- `backend/tests/Paretto.Api.Tests/GetPendingMuralsTests.cs` (modified) — mismo cambio.
- `backend/tests/Paretto.Api.Tests/DiscoveryControllerTests.cs` (modified) —
  `SeedPublishedMuralAsync` (que siembra vía `WebApplicationFactory`/DbContext directo) usa
  `Mural.CreateLocation(...)`.
- `backend/tests/Paretto.Api.Tests/CreateMuralTests.cs` (modified, si hace falta) — confirmar que
  sigue compilando y pasando sin cambios de aserciones (el contrato de respuesta no cambia).
- **`DiscoveryMappingConfig.cs` y `MuralMappingConfig.cs`: NO se modifican** — las propiedades
  computadas `Latitude`/`Longitude` de Block 1 hacen que Mapster siga mapeando por convención de
  nombre sin ningún cambio de código en estos dos archivos. Este bloque lo confirma con tests, no con
  un diff.

**Logic**

Este bloque no agrega comportamiento nuevo: cierra los gaps de compilación que dejaron los bloques
anteriores en las fixtures de test, y confirma con tests end-to-end (vía los endpoints reales, no
acceso directo al `DbContext`) que el contrato público completo (`GetMuralById`, `GetPendingMurals`,
`DiscoveryController`) sigue exponiendo `Latitude`/`Longitude` correctamente sin haber tocado sus
respectivos mapeos.

**Error handling**

- Sin casos nuevos: estos tests ejercitan rutas ya cubiertas por manejo de errores existente.

**Required tests**

- [ ] `GetMuralByIdTests`, `GetPendingMuralsTests`, `DiscoveryControllerTests`,
      `CreateMuralTests` — toda la suite existente en estos 4 archivos sigue pasando con las fixtures
      actualizadas — valida AC-02.
- [ ] Al menos un test en `DiscoveryControllerTests` o `GetMuralByIdTests` con latitud≠longitud
      claramente distinguibles, verificando que la respuesta JSON expone `Latitude`/`Longitude` en el
      campo correcto (no invertidos) — valida AC-02, mitigación final de R2 end-to-end (no solo a
      nivel de `Mural.CreateLocation`, sino a través de todo el pipeline de mapeo real).
- [ ] Verificación de `docs/adr/adr-005-nearby-murals-haversine-sin-geography.md`:
      `grep -c "Option B" docs/adr/adr-005-nearby-murals-haversine-sin-geography.md` devuelve al
      menos 1, y el campo `Status` de la tabla del header contiene "revisado" — confirma que el ADR
      quedó reflejando la decisión adoptada (no solo la original) — valida AC-08/FR-08.

**Completion criterion**

Los 4 archivos de test compilan y pasan sin modificar `DiscoveryMappingConfig.cs`/
`MuralMappingConfig.cs`; la verificación del ADR-005 pasa; la suite completa del backend (no solo
estos 4 archivos) pasa en verde.

## Final verification

- Los 12 tests nuevos/actualizados a lo largo de los 3 bloques pasan (AC-01 a AC-08 cubiertos,
  incluyendo la verificación del ADR-005 para AC-08/FR-08).
- `dotnet build`/`dotnet test` (suite completa del backend) sin errores.
- `grep -r "GeoDistanceCalculator\|IX_Murals_Status_Latitude_Longitude" backend/src backend/tests`
  no devuelve resultados.
- Verificación manual: `GET /api/discovery/nearby-murals` con los mismos parámetros que antes de la
  migración devuelve el mismo JSON (mismos campos, mismos valores, mismo orden) que la implementación
  anterior — cero cambios visibles, tal como pide el PRD.
