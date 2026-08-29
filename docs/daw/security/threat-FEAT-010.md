# Threat Model FEAT-010: Marcador del centro de búsqueda en el mapa de /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-010 |
| Date | 2026-08-29 |
| Scope | `discovery-page.component.ts`, `discovery-map.component.ts`, `geo-distance.util.ts` (frontend, sin cambios de backend) |

## Contexto arquitectónico

Este cambio es 100% presentacional en el frontend: agrega un signal (`lastSearchCenter`), una
función pura de distancia (`haversineDistanceMeters`), y un segundo marcador opcional en un mapa
Leaflet que ya existe. No se agrega ningún endpoint, no se modifica ningún flujo de datos hacia el
backend, y no se introduce ningún input nuevo de usuario — las coordenadas que este ticket compara
(`center`, geolocalización/manual) y (`lastSearchCenter`, el mismo lat/lng que el propio frontend ya
envió al backend en la última búsqueda exitosa) ya existían y ya estaban validadas por el código de
FEAT-005/FEAT-001d.

**Trust boundary relevante:** sin cambios — navegador (mismo origen que controla el usuario) → mapa
Leaflet renderizado localmente. No hay ningún cruce nuevo hacia el backend ni hacia terceros.

## Componentes nuevos/modificados y su superficie

| Componente | Acepta input de usuario | Expone datos sensibles | Cruza un trust boundary |
|---|---|---|---|
| `lastSearchCenter` (signal) | No (deriva de datos ya validados) | No | No |
| `haversineDistanceMeters()` | No (función pura, sin I/O) | No | No |
| `SEARCH_CENTER_ICON` / marcador nuevo | No | No (misma coordenada que el usuario ya ve en el mapa) | No |

## Análisis STRIDE

| Categoría | Aplica a este cambio | Evaluación |
|---|---|---|
| **Spoofing** | No | Sin identidad/sesión nueva involucrada. |
| **Tampering** | No | Sin persistencia ni escritura nueva; `lastSearchCenter` vive solo en memoria del cliente. |
| **Repudiation** | No | Sin acción nueva a auditar — es una mejora visual sobre un flujo ya existente. |
| **Information Disclosure** | No | El marcador muestra al usuario una coordenada que **él mismo** ya controla (su geolocalización o el punto al que movió el mapa) — no expone nada a otros usuarios ni a ningún endpoint nuevo. |
| **Denial of Service** | No | `haversineDistanceMeters` es aritmética pura (`Math.sin`/`Math.cos`/`Math.atan2`), costo despreciable, invocada como máximo una vez por actualización de los signals de entrada. |
| **Elevation of Privilege** | No | Sin cambios de permisos/roles. |

## Riesgos identificados

No se identifica ningún riesgo de seguridad (CRITICAL/HIGH/MEDIUM/LOW) — el cambio no introduce una
superficie de ataque nueva: no hay endpoint nuevo, no hay dato nuevo que viaje a ningún lado, y las
coordenadas comparadas ya eran datos que el propio navegador del usuario controlaba y que el backend
ya había validado (rango lat/lng) en la request original que las originó.

Único punto no-funcional a notar (no es un riesgo de seguridad, es de accesibilidad, ya señalado
como riesgo de producto en `docs/daw/prd/prd-FEAT-005.md`, sección "Risks and Mitigations", ítem
agregado en PRD loop 1): dos marcadores distinguibles solo por color pueden ser difíciles de
diferenciar para usuarios con daltonismo — resuelto en el spec de PLAN con una forma distinta (no
solo color) para el marcador de centro de búsqueda, no es parte del threat model.

## Clasificación de datos sensibles (F-TM-05)

- **Coordenadas mostradas en el marcador:** las mismas que ya se mostraban en el marcador de "tu
  ubicación" existente (FEAT-005) — no hay un dato nuevo o más sensible expuesto.
- No hay PII, credenciales ni datos financieros involucrados en este cambio.

## Riesgos aceptados

Ninguno — no hay riesgos abiertos que requieran aceptación formal.

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 3                             │
│  Trust boundaries declared: 1 (navegador, sin cambios)        │
│                                                                │
│  Risks:                                                          │
│    (ninguno — 0 CRITICAL, 0 HIGH, 0 MEDIUM, 0 LOW)                  │
│                                                                        │
│  Mitigations to fold into the spec: ninguna de seguridad                 │
│    (accesibilidad de color ya prevista en el PRD, a resolver en CODE)      │
│                                                                                │
│  ─────────────────────────────────────────────────────                         │
│  Risks: C:0 H:0 M:0 L:0                                                          │
│  Report: docs/daw/security/threat-FEAT-010.md                                       │
└─────────────────────────────────────────────────────────┘
```
