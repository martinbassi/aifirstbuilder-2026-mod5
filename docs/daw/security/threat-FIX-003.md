# Threat Model FIX-003: Revisar y corregir tests rotos por Title obligatorio en Mural + converters UTC

| Field | Value |
|-------|-------|
| Ticket | FIX-003 |
| Date | 2026-08-26 |

## Diseño analizado

**Fix A** (causa raíz #1): `CreateMuralTests.cs::BuildMultipartContent` gana un parámetro `title`
(default válido) incluido en el multipart; se actualizan las 12 invocaciones reales; se agregan 2
tests nuevos (título ausente → 422, título >50 caracteres → 422) cubriendo FR-17/AC-15/AC-16 de
`prd-FEAT-001b.md`. Cambio 100% en código de test, sin efecto en producción.

**Fix B** (causa raíz #2): `Program.cs` agrega `builder.Services.AddControllers().AddJsonOptions(...)`
replicando la configuración ya existente en `ConfigureHttpJsonOptions` (`JsonStringEnumConverter`,
`DefaultIgnoreCondition.WhenWritingNull`, `PropertyNamingPolicy.CamelCase`,
`JsonDateTimeUtcConverter`), para que efectivamente aplique a las respuestas de los controllers MVC
(`MuralsController`, `DiscoveryController`, `AuthController`, `ModerationController`). Se agregan
tests de formato de fecha en `GetMuralByIdTests.cs`/`GetNearbyMuralsTests.cs` y se extiende
cobertura a `LoginTests.cs` para `LoginCommand.ExpiresAt` (gap detectado por el impact scan: el
cambio es global, no solo afecta a Murals).

## Componentes y superficies de ataque

- **CreateMuralTests.cs (Fix A):** código de test, no compilado en el artefacto de producción. No
  introduce superficie de ataque nueva.
- **Program.cs / `AddJsonOptions` (Fix B):** afecta la serialización de **todas** las respuestas
  JSON servidas por los 4 controllers MVC existentes. Cruza el **trust boundary** cliente
  (navegador, no confiable) ↔ servidor (API, confiable) — es exactamente el punto donde entra
  cualquier riesgo de exposición de datos.

## Análisis STRIDE (sobre Fix B — Fix A no tiene superficie de producción)

| Categoría | Evaluación |
|---|---|
| **Spoofing** | No aplica — el cambio es de formato de serialización, no toca autenticación/identidad. |
| **Tampering** | No aplica — los campos serializados (`CreatedAt`, `ExpiresAt`, `Title`, etc.) ya eran de solo lectura desde el cliente; el cambio no abre un nuevo vector de modificación. |
| **Repudiation** | Sin cambios — no se toca logging ni auditoría. |
| **Information Disclosure** | Repasado. `DefaultIgnoreCondition.WhenWritingNull` **reduce** exposición (omite campos null en vez de emitirlos). `JsonStringEnumConverter` no aplica a ningún campo hoy: confirmado por impact scan que ninguna `*Response` DTO expone una propiedad de tipo enum directamente (`MuralStatus`/`UserRole` viven en las entidades de dominio y se mapean a `string` manualmente en los Response). El cambio de formato de fecha (`yyyy-MM-ddTHH:mm:ssZ`, sin fracciones de segundo) expone **menos** precisión que el formato round-trip anterior, no más. Riesgo: bajo. |
| **Denial of Service** | No aplica — no cambia límites de tamaño, rate limiting ni procesamiento. |
| **Elevation of Privilege** | No aplica — no toca autorización ni roles. |

## Riesgos identificados

| Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|
| El cambio de `JsonOptions` es GLOBAL (afecta las 4 controllers, no solo Murals) y el blast radius no está 100% cubierto por tests nuevos — un consumidor no cubierto por la suite podría depender del formato de fecha viejo sin que ningún test lo detecte. | Information Disclosure (parcial, más bien de correctitud) | Media | Bajo | Confirmado con el frontend (`api-client.generated.ts`, `discovery-list.component.html`) que todo consumo de `createdAt`/`expiresAt` pasa por `new Date(...)` o el pipe `date` de Angular — ambos parsean ISO 8601 sin problema, formato que `yyyy-MM-ddTHH:mm:ssZ` respeta. Se agrega cobertura explícita para los 3 campos `DateTime` que el impact scan encontró en Response DTOs (`GetMuralByIdQuery.CreatedAt`, `GetNearbyMuralsQuery.CreatedAt`, `LoginCommand.ExpiresAt`) — no quedan campos de fecha sin test. Adicionalmente, correr la suite completa (frontend + backend) en CODE, no solo la del área tocada. |
| Test-only (Fix A): un default de `title` mal elegido en `BuildMultipartContent` podría enmascarar un futuro bug real de validación de título si todos los tests usan el mismo valor. | — (no es de seguridad, es de calidad de test) | Baja | Bajo | Los 2 tests nuevos (título ausente, título >50 chars) prueban explícitamente los bordes del campo — el default "feliz" no es el único caso cubierto. |

No se identificaron riesgos CRITICAL ni HIGH. Ambos hallazgos son MEDIUM/LOW y ya tienen mitigación
folded en el diseño (arriba), sin requerir cambios de arquitectura adicionales.

## Datos sensibles

Ninguno de los dos fixes introduce manejo nuevo de PII o credenciales. `LoginCommand.ExpiresAt`
(fecha de expiración de sesión, no un secreto en sí) ya se transmite sobre HTTPS en producción
(fuera del alcance de este ticket); el fix solo cambia su formato de string, no su transporte.

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 2 (test-only sin superficie;  │
│    JSON serialization global de los 4 controllers MVC)     │
│  Trust boundaries declared: 1 (cliente ↔ servidor, vía      │
│    serialización JSON de respuesta)                          │
│                                                            │
│  Risks:                                                    │
│    🟡 MEDIUM: blast radius de AddJsonOptions global —         │
│       Mitigación: cobertura de test en los 3 campos DateTime │
│       de Response DTOs + verificación manual de consumo       │
│       frontend (ISO 8601 compatible) + suite completa en CODE │
│    🟢 LOW: default de título en tests podría enmascarar        │
│       un bug futuro — Mitigación: tests de borde explícitos    │
│                                                            │
│  Mitigations to fold into the spec:                        │
│    1. Test de formato de fecha en GetMuralByIdTests.cs y     │
│       GetNearbyMuralsTests.cs                                 │
│    2. Test de formato de fecha en LoginTests.cs (ExpiresAt)   │
│    3. Tests de borde de Title en CreateMuralTests.cs           │
│       (ausente, >50 chars)                                     │
│                                                            │
│  ─────────────────────────────────────────────────────    │
│  Risks: C:0 H:0 M:1 L:1                                    │
│  Report: docs/daw/security/threat-FIX-003.md                │
└─────────────────────────────────────────────────────────┘
```
