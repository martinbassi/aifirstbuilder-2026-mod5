# ADR-004: Reemplazar InvariantGlobalization por CultureInfo.DefaultThreadCurrentCulture

| Field | Value |
|-------|-------|
| Date | 2026-08-22 |
| Ticket | FEAT-001b |
| Status | Accepted |
| Supersedes | ADR-002 |

## Context

Corriendo la suite completa de tests (closeout de CODE, Block 8) contra una instancia real de SQL
Server (no InMemory), los 5 tests que abren una conexión real (`MuralPersistenceTests`,
`AuthPersistenceTests`) fallaron con `System.NotSupportedException: Globalization Invariant Mode is
not supported`, lanzada por `Microsoft.Data.SqlClient` al intentar abrir la conexión.

Se confirmó empíricamente (quitando el flag y volviendo a correr) que `<InvariantGlobalization>true
</InvariantGlobalization>` (ADR-002) es la causa: `Microsoft.Data.SqlClient` no soporta ese modo
para conectarse a SQL Server. El costo que ADR-002 daba por "nulo en la práctica" no lo era —
rompe cualquier código que abra una conexión real a la base, no solo comparación/casing de strings
fuera de ASCII como se había evaluado.

## Options considered

### Option A: mantener InvariantGlobalization, aislar SqlClient de alguna forma
- **Pros:** no toca la decisión ya aceptada.
- **Cons:** no existe tal aislamiento — el flag es un `AppContext` switch de proceso entero; no hay
  forma de excluir solo a `Microsoft.Data.SqlClient`. Descartada.

### Option B: `CultureInfo.DefaultThreadCurrentCulture` en `Program.cs` (Opción B de ADR-002)
- **Pros:** ya evaluada en ADR-002 en el momento de esa decisión — cubre el mismo caso (parseo de
  `double`/`decimal`/`DateTime` en cualquier endpoint, sin depender de `LANG`/`LC_ALL` del SO), sin
  deshabilitar ICU, por lo que no interfiere con `SqlClient`. El con original de ADR-002 ("no
  resuelve el problema en el proceso de test a menos que se configure ahí explícitamente") no
  aplica: al fijarlo en el código de nivel superior de `Program.cs`, `WebApplicationFactory<Program>`
  ejecuta ese mismo código al bootstrapear el host de test, así que el default queda fijado también
  ahí.
- **Cons:** menos descubrible que una propiedad del `.csproj`; código de arranque que un futuro
  `RequestLocalizationMiddleware` podría pisar sin darse cuenta (mismo trade-off ya documentado en
  ADR-002).

## Decision

Se adopta la **Opción B**. Se agrega en `Program.cs`, antes de `WebApplication.CreateBuilder`:
`CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture` (+ `...UICulture`), y se
quita `<InvariantGlobalization>true</InvariantGlobalization>` de `Paretto.Api.csproj` y
`Paretto.Api.Tests.csproj`. Se verificó que el bug original de ADR-002 (parseo de `Latitude`/
`Longitude` bajo locale `es_ES`) sigue resuelto: la suite completa (53/53) pasa corriendo con
`LC_ALL=es_ES.utf8` / `LANG=es_ES.utf8`.

## Consequences

- `Program.cs`: agrega el bloque de `CultureInfo.DefaultThreadCurrentCulture`/`...UICulture`.
- `Paretto.Api.csproj` y `Paretto.Api.Tests.csproj`: se quita `InvariantGlobalization`.
- ADR-002 pasa a estado `Superseded by ADR-004`.
- No hace falta reintroducir el `InvariantDoubleModelBinder` puntual que ADR-002 había removido: la
  cobertura sigue siendo global (todo endpoint, todo tipo).
- Limitación aceptada (heredada de ADR-002): si el producto necesitara en el futuro formateo/
  comparación culture-aware, habría que fijar `CultureInfo` explícito en ese punto puntual.
