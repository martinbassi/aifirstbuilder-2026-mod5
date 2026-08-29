# Threat Model FEAT-009: Migrar búsqueda de murales cercanos a geography + NetTopologySuite

| Field | Value |
|-------|-------|
| Ticket | FEAT-009 |
| Date | 2026-08-29 |
| Scope | `Mural.cs`, `AppDbContext.cs`/`AppDbContextFactory.cs`, nueva migración, `CreateMuralCommand.cs`, `GetNearbyMuralsQuery.cs`, `DiscoveryMappingConfig.cs`/`MuralMappingConfig.cs` (backend) |

## Contexto arquitectónico

Este cambio reemplaza el almacenamiento interno de la ubicación del mural (`double`/`double` →
`geography`) y el mecanismo de cálculo de distancia (Haversine en memoria → consulta espacial nativa
de SQL Server), sin modificar ningún contrato público, ningún endpoint nuevo, ni el modelo de
autenticación/autorización. La superficie de ataque nueva es estrictamente interna: el dato que
entra (latitud/longitud) y el que sale (los mismos campos) no cambian; lo que cambia es cómo se
almacena y se consulta en el medio.

**Trust boundary relevante:** sin cambios — cliente (no confiable) → backend API (confiable), y
backend → SQL Server (confiable). Este ticket no introduce un cruce nuevo.

## Componentes nuevos/modificados y su superficie

| Componente | Acepta input de usuario | Expone datos sensibles | Cruza un trust boundary |
|---|---|---|---|
| `Mural.Location` (Point) | Indirectamente (vía lat/lng ya validados) | No | No |
| Migración de backfill (SQL crudo) | No (SQL estático, sin interpolación de datos externos) | No | No |
| `GetNearbyMuralsQueryHandler` (consulta espacial) | Sí (lat/lng/radius, igual que hoy) | No | No (mismo boundary que ya existía) |
| Dependencia `NetTopologySuite` | N/A | N/A | Supply chain (ver riesgo R4) |

## Análisis STRIDE

| Categoría | Aplica a este cambio | Evaluación |
|---|---|---|
| **Spoofing** | No | Sin cambios de identidad/autenticación. |
| **Tampering** | Sí (ver R1) | La consulta espacial nueva podría construirse con concatenación de strings en vez de parámetros, reabriendo una superficie de inyección SQL que la implementación anterior (LINQ-to-Entities con Haversine en memoria) no tenía. |
| **Repudiation** | No | Sin cambios de logging/auditoría. |
| **Information Disclosure** | No | Mismos campos expuestos que hoy; ver R2 para el riesgo de que se filtre *más* de lo esperado por un mapeo incompleto. |
| **Denial of Service** | Bajo (ver R3) | La migración de backfill + `DropColumn` corre en el mismo `Up()`; si se interrumpe a mitad de camino sin transacción, deja la tabla en un estado inconsistente. |
| **Elevation of Privilege** | No | El endpoint sigue siendo `[AllowAnonymous]`, sin cambios de rol/permiso. |

## Riesgos identificados

| Riesgo | STRIDE | Likelihood | Impact | Mitigación propuesta |
|---|---|---|---|---|
| R1: si la consulta espacial en `GetNearbyMuralsQueryHandler` se construye con SQL crudo interpolando `lat`/`lng`/`radius` como strings en vez de parámetros, se reabre una inyección SQL (OWASP A03) que la implementación anterior (LINQ puro) no tenía. | Tampering | Baja (evitable por diseño) | Alto si ocurriera | El spec debe exigir explícitamente LINQ-to-Entities (`Location.Distance(...)`) o, si se usa SQL crudo, parametrización obligatoria vía `FormattableString`/`SqlParameter` — nunca interpolación de string. Se folded al spec como requisito no negociable de Block 2. |
| R2: el remapeo explícito de `Location.Y`/`Location.X` en `DiscoveryMappingConfig.cs`/`MuralMappingConfig.cs` puede quedar mal (ej. invertir X/Y) y filtrar coordenadas incorrectas sin que ningún test lo note si el test usa un punto donde lat≈lng. | Information Disclosure (bajo, coordenada errónea no es dato sensible) | Media | Bajo | Ya identificado como riesgo en el PRD; el spec debe exigir tests con lat≠lng bien diferenciados (ej. -34.6 / -58.3) para que un swap de ejes falle un test. |
| R3: la migración hace backfill + `DropColumn` de `Latitude`/`Longitude` en el mismo `Up()`; si se interrumpe a mitad de camino (timeout, fallo de conexión), la tabla queda en un estado intermedio inconsistente. | Denial of Service (integridad de datos, no del servicio) | Baja | Medio | EF Core envuelve cada migración en una transacción por defecto (a menos que se use `Suppress­TransactionalMigration` explícitamente, que este plan no usa) — el spec debe confirmar explícitamente que no se desactiva ese comportamiento, y documentar el rollback (`Down()`) simétrico. |
| R4: `NetTopologySuite` es una dependencia de terceros nueva (supply chain). | Tampering (cadena de suministro) | Baja | Bajo | Cubierto por el SAST del cierre de CODE (`npm audit`/`dotnet list package --vulnerable` ya es parte del gate estándar de este proyecto) — no requiere mitigación adicional en el diseño. |

No se identifican riesgos CRITICAL. R1 es HIGH en impacto pero baja probabilidad y completamente
evitable por diseño (constraint explícito en el spec, verificado en CODE/VERIFY) — se folds como
requisito de spec, no queda como riesgo abierto.

## Clasificación de datos sensibles (F-TM-05)

- **Ubicación del mural (`Point`/`geography`):** mismo dato que hoy (`Latitude`/`Longitude`),
  clasificado igual — "Pending" hasta moderación, "Public" una vez `Published`. Sin cambios de
  clasificación.
- No hay PII, credenciales ni datos financieros en este cambio.

## Riesgos aceptados

Ninguno queda como riesgo abierto: R1 y R3 se convierten en requisitos explícitos del spec (no
opcionales), R2 en un requisito de test, y R4 ya está cubierto por el gate de SAST existente del
proyecto.

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 4                             │
│  Trust boundaries declared: 2 (cliente→API, API→SQL       │
│    Server), sin cambios respecto al diseño existente         │
│                                                                │
│  Risks:                                                          │
│    🟠 HIGH (impacto, no probabilidad): R1 — inyección SQL en      │
│       la consulta espacial si se construye por concatenación —      │
│       Mitigation: LINQ-to-Entities o SQL parametrizado, exigido        │
│       en el spec, no opcional                                            │
│    🟡 MEDIUM: R3 — migración backfill+drop sin transacción              │
│       explícita — Mitigation: confirmar transacción por defecto           │
│       de EF Core, documentar Down() simétrico                               │
│    🟢 LOW: R2 — swap de ejes X/Y en el remapeo — Mitigation: tests            │
│       con lat≠lng bien diferenciados                                            │
│    🟢 LOW: R4 — dependencia nueva (NetTopologySuite) — cubierta por               │
│       el gate de SAST existente                                                     │
│                                                                                         │
│  Mitigations to fold into the spec:                                                       │
│    1. Consulta espacial vía LINQ-to-Entities o SQL parametrizado                             │
│       (nunca concatenación) — Block 2.                                                          │
│    2. Confirmar transacción de la migración + Down() simétrico —                                   │
│       Block 1.                                                                                        │
│    3. Tests con coordenadas lat≠lng en Block 3 (mapeos).                                                 │
│                                                                                                              │
│  ─────────────────────────────────────────────────────                                                       │
│  Risks: C:0 H:1 (mitigado por diseño) M:1 L:2                                                                    │
│  Report: docs/daw/security/threat-FEAT-009.md                                                                      │
└─────────────────────────────────────────────────────────┘
```
