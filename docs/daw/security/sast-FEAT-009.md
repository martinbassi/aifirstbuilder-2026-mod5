# SAST FEAT-009: Migrar búsqueda de murales cercanos a geography + NetTopologySuite

| Field | Value |
|-------|-------|
| Ticket | FEAT-009 |
| Date | 2026-08-29 |
| Scope | Cierre de CODE — diff completo del ticket (commits `32deb88`, `e0b4922`, `7b1a933`) |

## Archivos escaneados

- `backend/src/Paretto.Domain/Entities/Mural.cs`
- `backend/src/Paretto.Infrastructure/Data/AppDbContext.cs`, `AppDbContextFactory.cs`
- `backend/src/Paretto.Api/Program.cs`
- `backend/src/Paretto.Infrastructure/Data/Migrations/20260829153015_MuralLocationGeography.cs`
- `backend/src/Paretto.Api/Features/Murals/Commands/CreateMuralCommand.cs`
- `backend/src/Paretto.Api/Features/Discovery/Queries/GetNearbyMuralsQuery.cs`
- 7 archivos de test (`MuralPersistenceTests.cs`, `AuthPersistenceTests.cs`,
  `GetNearbyMuralsTests.cs`, `GetMuralByIdTests.cs`, `GetPendingMuralsTests.cs`,
  `ApproveMuralTests.cs`, `RejectMuralTests.cs`, `DiscoveryControllerTests.cs`)
- `Paretto.Domain.csproj`, `Paretto.Infrastructure.csproj`

## Resultado

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                  │
├─────────────────────────────────────────────────────────────┤
│                                                                │
│  Secrets:                                                      │
│    ✅ F-SAST-01: sin API keys/passwords/tokens/connection        │
│       strings en el diff. Sin cambios en `.gitignore`/`.env`.       │
│                                                                          │
│  Injection:                                                              │
│    ✅ F-SAST-02: la migración usa `migrationBuilder.Sql(...)` con           │
│       4 strings ESTÁTICOS (backfill, índice espacial, rollback) —             │
│       ninguno interpola una variable externa o input de usuario,                │
│       solo referencian nombres de columna literales                                │
│       (`Latitude`, `Longitude`, `Location.Lat`, `Location.Long`).                     │
│       Verificado leyendo las 4 líneas `Sql(...)` del archivo completo.                  │
│    ✅ F-SAST-02: `GetNearbyMuralsQuery.cs` usa exclusivamente                             │
│       LINQ-to-Entities (`Location.Distance(searchPoint)`) — cero                            │
│       `FromSqlRaw`/`ExecuteSqlRaw`/interpolación de string en todo el                          │
│       archivo (confirmado en las dos revisiones de Block 2, re-confirmado                        │
│       acá). Este era el riesgo R1 (HIGH-por-impacto) del threat model —                             │
│       mitigado por diseño, no queda abierto.                                                          │
│    ✅ F-SAST-03/05: N/A — sin comandos de sistema, sin paths de archivo                                  │
│       construidos con input de usuario.                                                                    │
│                                                                                                                │
│  XSS y funciones inseguras:                                                                                     │
│    ✅ F-SAST-06: N/A — sin superficie HTML/frontend en este ticket                                                │
│       (100% backend).                                                                                               │
│    ✅ F-SAST-04: sin `eval()`, sin deserialización insegura.                                                          │
│    ✅ F-SAST-08: sin criptografía en este diff.                                                                          │
│                                                                                                                              │
│  Otras categorías obligatorias:                                                                                                │
│    ✅ F-SAST-07 (SSRF): N/A.                                                                                                      │
│    ✅ F-SAST-09 (debug mode): sin flags de debug ni cambios de entorno.                                                              │
│    ✅ F-SAST-10 (logging de datos sensibles): sin `Console.Write`/logging                                                              │
│       nuevo en código de producción.                                                                                                       │
│    ✅ F-SAST-11 (upload sin restricción): N/A, este ticket no toca el                                                                          │
│       endpoint de carga de fotos ni sus límites (RNF-003 sin cambios).                                                                             │
│    ✅ F-SAST-12 (CSRF): N/A, sin cambios de autenticación/autorización.                                                                                │
│       `GetNearbyMuralsQuery` sigue `[AllowAnonymous]`, sin cambios.                                                                                        │
│    ✅ F-SAST-14 (validación de input incompleta): `CreateMuralCommandValidator`/                                                                              │
│       `GetNearbyMuralsQueryValidator` sin cambios — el rango de lat/lng/radius                                                                                    │
│       sigue validado antes de que el Handler construya el `Point`.                                                                                                   │
│    ✅ F-SAST-15 (error handling que filtra internals): la migración falla con                                                                                          │
│       el error nativo de SQL Server ante coordenadas inválidas (AC-05),                                                                                                   │
│       dentro de una transacción — no hay manejo custom que exponga detalles                                                                                                  │
│       internos al usuario final (esto ocurre en tiempo de deploy, no en runtime                                                                                                │
│       de la API expuesta a usuarios).                                                                                                                                              │
│                                                                                                                                                                                        │
│  Dependencias:                                                                                                                                                                          │
│    ✅ F-SAST-13/16: `dotnet list package --vulnerable --include-transitive`                                                                                                               │
│       → 0 vulnerabilidades en los 4 proyectos del backend, incluyendo                                                                                                                        │
│       `NetTopologySuite` y `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite`                                                                                                            │
│       (las 2 dependencias nuevas de este ticket).                                                                                                                                                  │
│                                                                                                                                                                                                        │
│  Suppressions: 0                                                                                                                                                                                        │
│                                                                                                                                                                                                            │
│  ─────────────────────────────────────────────────────────────                                                                                                                                             │
│  Total: 14 clean, 0 vulnerabilities (0 critical, 0 high, 0 medium)                                                                                                                                            │
│  Report: docs/daw/security/sast-FEAT-009.md                                                                                                                                                                     │
└─────────────────────────────────────────────────────────────┘
```
