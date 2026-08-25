# Threat Model FEAT-005: Geolocalización funcional y refetch de murales por área en /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-005 |
| Spec | docs/daw/specs/spec-FEAT-005.md |
| Date | 2026-08-25 |

## Alcance

Cambio puramente frontend sobre `features/discovery/ui/` (`DiscoveryMapComponent`,
`DiscoveryPageComponent`). No agrega endpoints, no modifica el contrato de
`GET /api/discovery/nearby` (`GetNearbyMurals`, público, sin sesión — FR-07 de FEAT-001d), no agrega
persistencia ni nuevos campos. El único dato que viaja al backend (`latitude`/`longitude`) es el
mismo tipo de dato que FEAT-001d ya clasificó y protegió.

## Trust boundary

Sin boundary nuevo: se reutiliza el mismo cruce que FEAT-001d ya declaró — navegador del visitante
(sin autenticar) → `GetNearbyMurals` (público). Este ticket no mueve ni agrega ningún cruce; solo
cambia **cuándo** y **con qué frecuencia** el navegador puede disparar esa misma consulta desde la
UI (antes: una vez al cargar + reintentos manuales tipeando lat/lng; ahora, además: un click sobre el
centro actual del mapa).

## Datos sensibles

- **Centro actual del mapa (`lastMapCenter`, lat/lng)**: mismo tipo de dato que la "Ubicación del
  visitante" que FEAT-001d ya clasificó como **PII** (`docs/daw/security/threat-FEAT-001d.md:34`).
  Hereda la misma protección: nunca se persiste (`fetchNearbyMurals` solo la usa en memoria para el
  request), viaja por HTTPS (`UseHttpsRedirection()` ya activo, sin cambios en este ticket). F-TM-07:
  sin trabajo nuevo, la protección ya existe y este ticket no abre una ruta de persistencia nueva.
- **Pin distintivo del visitante en el mapa**: renderiza en el propio navegador del visitante la
  misma ubicación que él mismo ya autorizó compartir (o tipeó manualmente) — no se envía a ningún
  otro destino ni a otros usuarios. No es una superficie de disclosure nueva: es la misma PII de
  arriba, mostrada de vuelta a su dueño.

## Análisis STRIDE

| Componente | S | T | R | I | D | E |
|---|---|---|---|---|---|---|
| `DiscoveryMapComponent` (recentrado reactivo, pin, `mapMoved`) | N/A — sin identidad que suplantar | N/A — el centro lo calcula Leaflet a partir del propio viewport, no es texto libre editable por el usuario | N/A — sin acción que requiera trazabilidad (público, ya así en FEAT-001d) | Ver "Datos sensibles" — sin disclosure nuevo | Ver R1 abajo | N/A — sin roles/permisos involucrados |
| `DiscoveryPageComponent.searchThisArea()` (nuevo botón → `fetchNearbyMurals`) | N/A | N/A — mismo request shape que ya existe (`getNearbyMurals(lat, lng, radiusKm?)`) | N/A | Ver "Datos sensibles" | Ver R1 abajo | N/A |

## Riesgos

| ID | Riesgo | Categoría STRIDE | Likelihood | Impact | Mitigación propuesta |
|---|---|---|---|---|---|
| R1 | El botón "Buscar en esta área" baja la fricción para disparar la misma consulta pública repetidamente (antes había que retipear lat/lng en el form manual; ahora es un click) — uso abusivo/scripted podría generar más tráfico sobre la consulta geoespacial. | Denial of Service | Low | Low | **Ya mitigado por diseño (Block 4 del spec, sin trabajo adicional):** el botón queda `[disabled]="loading()"` mientras la consulta está en curso — un mismo cliente no puede encolar requests superpuestos, el mismo throttle que el form manual ya usaba. Rate-limiting server-side sigue siendo responsabilidad de la superficie pública que FEAT-001d ya expuso, sin cambios de alcance en este ticket. |
| R2 | Un movimiento programático del mapa (recentrado por geolocalización) podría confundirse con un movimiento real y disparar `mapMoved`/el botón sin intervención del usuario, exponiendo un flujo de refetch no solicitado. | Tampering (de la señal de intención del usuario, no de datos) | Medium (sin la guarda) | Low | **Ya mitigado por diseño (Block 3 del spec):** el flag `suppressNextMapMoved` distingue `setView()` propio de un `moveend`/`zoomend` real del usuario — cubierto por 2 tests dedicados en el spec. No es un riesgo de seguridad de datos, pero se documenta porque es la única lógica no trivial que este ticket introduce. |

Sin hallazgos CRITICAL ni HIGH — ambos riesgos identificados ya quedan mitigados por el propio diseño
del spec, sin requerir cambios adicionales.

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                           │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 2 (DiscoveryMapComponent,    │
│    DiscoveryPageComponent.searchThisArea)                 │
│  Trust boundaries declared: 1 (heredado de FEAT-001d, sin │
│    cambios)                                                │
│                                                            │
│  Risks:                                                    │
│    🟢 LOW: R1 — refetch de baja fricción (DoS) — ya         │
│       mitigado por el disabled/loading del botón            │
│    🟢 LOW: R2 — moveend programático confundido con real —  │
│       ya mitigado por el flag suppressNextMapMoved          │
│                                                            │
│  Mitigations to fold into the spec: ninguna adicional —     │
│    ambas ya están en el diseño (Block 3 y Block 4)          │
│                                                            │
│  ─────────────────────────────────────────────────────    │
│  Risks: C:0 H:0 M:0 L:2                                   │
│  Report: docs/daw/security/threat-FEAT-005.md              │
└─────────────────────────────────────────────────────────┘
```
