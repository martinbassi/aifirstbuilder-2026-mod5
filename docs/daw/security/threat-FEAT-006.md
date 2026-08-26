# Threat Model FEAT-006: Mostrar título y fecha de creación al hacer click en un marcador del mapa de /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-006 |
| Date | 2026-08-26 |

## Diseño analizado

**Block 1** (nuevo): `discovery-map.component.ts`, en `renderMarkers()`, agrega
`marker.bindPopup(...)` con título + fecha de creación del mural. **Block 2**: solo tests/limpieza
sobre `discovery-list.component.*` (sin cambios de superficie de ataque).

## Componentes y superficies de ataque

- **`discovery-map.component.ts` (Block 1):** consume `NearbyMuralItemResponse.title`, texto libre
  ingresado por CUALQUIER usuario autenticado al crear un mural (`CreateMuralCommandValidator` solo
  valida `NotEmpty()` + `MaximumLength(50)` — sin whitelist de caracteres, sin sanitización HTML) y
  lo renderiza en un popup visible para CUALQUIER visitante del mapa público (`/discover`, sin
  sesión). Esto cruza el trust boundary "contenido generado por un usuario" ↔ "renderizado a otros
  usuarios" — el mismo boundary que ya cruza `discovery-list.component.html` al interpolar
  `{{ item.title }}` (seguro por defecto: la interpolación de Angular escapa HTML).
- **`discovery-list.component.*` (Block 2):** sin cambios de superficie — solo tests y eliminación
  de código muerto (`selectedItem`, docstring desactualizado). No introduce ni modifica ningún flujo
  de datos.

## Análisis STRIDE (Block 1 — Block 2 no introduce superficie nueva)

| Categoría | Evaluación |
|---|---|
| **Spoofing** | No aplica — no cambia identidad ni autenticación. |
| **Tampering** | No aplica — solo lectura/renderizado, sin escritura desde el cliente. |
| **Repudiation** | No aplica — sin acciones de seguridad relevantes que loguear. |
| **Information Disclosure** | `title`/`createdAt` ya son públicos para murales `Published` (ya se muestran en la lista y en la respuesta del endpoint); el popup no expone ningún campo nuevo. Riesgo: bajo. |
| **Denial of Service** | No aplica — `bindPopup` es una operación DOM liviana, sin cómputo nuevo relevante. |
| **Elevation of Privilege** | No aplica — sin cambios de autorización. |

## Riesgos identificados

| Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|
| **XSS almacenado vía título del mural en el popup del mapa.** Confirmado empíricamente en el código fuente de Leaflet (`leaflet-src.js:10027`, `Popup._updateContent`): si `bindPopup`/`setContent` recibe un **string**, lo inserta con `node.innerHTML = content` (HTML crudo, sin escapar). Como `title` es texto libre sin sanitización backend, un mural con un título tipo `<img src=x onerror=alert(document.cookie)>` ejecutaría JS arbitrario en el navegador de CUALQUIER visitante que haga click en ese marcador — sin sesión, en la página pública `/discover`. | Tampering / Information Disclosure (vía script injection) | Media (cualquier usuario autenticado puede setear el título al crear un mural, sin restricción de caracteres) | **Alto** (ejecución de JS arbitrario en el navegador de visitantes anónimos del mapa público — robo de sesión, phishing, etc.) | El diseño ya lo evita **por construcción**: el contenido del popup se arma con `document.createElement` + `.textContent` (nunca interpolación de string HTML), y se pasa a `bindPopup()` el `HTMLElement` resultante — `Popup._updateContent` lo trata como Node (`appendChild`, sin parsear HTML) en vez de string (`innerHTML`). Confirmado en el código fuente citado arriba, no es una suposición. **Mandatorio para CODE:** ningún template string / concatenación de HTML con `item.title` en `discovery-map.component.ts` — solo `createElement`/`textContent`. |
| Código muerto (`selectedItem`) y documentación desactualizada en `discovery-list.component.ts` (Block 2) quedan sin corregir si no se prioriza. | — (no es de seguridad, es de mantenibilidad) | Baja | Bajo | Ya incluido en el alcance de Block 2 (impact scan lo señaló). |

No se identificaron riesgos CRITICAL. El único riesgo HIGH (XSS vía título en el popup) ya está
mitigado por el diseño propuesto — no requiere cambiar la arquitectura, solo implementarlo tal como
está especificado (DOM API, nunca strings HTML).

## Datos sensibles

`title` y `createdAt` de un mural `Published`: dato público (ya expuesto por el mismo endpoint en
la lista y en `GetMuralByIdQuery`). No es PII ni credencial — sin requisitos de cifrado adicionales
a los ya vigentes (HTTPS en tránsito).

## Resultado

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 1 (popup del mapa con título    │
│    de usuario — Block 2 sin superficie nueva)                  │
│  Trust boundaries declared: 1 (contenido generado por un        │
│    usuario ↔ renderizado a cualquier visitante del mapa           │
│    público, sin sesión)                                             │
│                                                                        │
│  Risks:                                                                 │
│    🟠 HIGH: XSS almacenado vía título del mural en el popup del          │
│       mapa (confirmado en el código fuente de Leaflet: string →           │
│       innerHTML, Node → appendChild) — Mitigación: el diseño ya            │
│       usa createElement/textContent, nunca strings HTML; mandatorio         │
│       verificarlo en la revisión de CODE                                     │
│    🟢 LOW: código muerto en discovery-list.component.ts (Block 2,             │
│       ya en alcance)                                                            │
│                                                                                    │
│  Mitigations to fold into the spec:                                                 │
│    1. discovery-map.component.ts: popup del mapa construido EXCLUSIVAMENTE           │
│       vía DOM API (createElement + textContent), nunca interpolación de               │
│       string HTML — criterio de aceptación explícito del Block 1                       │
│                                                                                            │
│  ─────────────────────────────────────────────────────                                    │
│  Risks: C:0 H:1 M:0 L:1                                                                     │
│  Report: docs/daw/security/threat-FEAT-006.md                                                │
└─────────────────────────────────────────────────────────┘
```
