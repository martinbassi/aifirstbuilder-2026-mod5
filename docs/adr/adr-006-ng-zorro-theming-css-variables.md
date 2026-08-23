# ADR-006: Theming de ng-zorro vía CSS custom properties (`.variable.css`), no Less

| Field | Value |
|-------|-------|
| Date | 2026-08-23 |
| Ticket | FEAT-002 |
| Status | Accepted |

## Context

`frontend/angular.json` carga hoy `ng-zorro-antd.min.css` — el CSS de ng-zorro completamente
compilado. Es un archivo minificado de una sola línea, así que `grep -c` sobre él siempre da como
máximo 1 (cuenta líneas, no ocurrencias); la comprobación real es `grep -o` contando ocurrencias:
`var(--ant-primary-color` da 0 en `min.css` (sí tiene otras variables de subsistemas distintos,
como `--antd-wave-shadow-color`, irrelevantes para esta decisión) contra 281 en `variable.css`.
FEAT-002 exige que la
paleta de colores (primario `#FE6944`, secundario `#0D2348`) impacte **todos** los componentes de
ng-zorro sobreescribiendo variables de theming globales, no con estilos ad-hoc por componente (FR-07
del PRD) — es infraestructura de build compartida por toda la app, no algo propio de una feature
(mismo criterio que motivó ADR-003 para `nswag.json`).

## Options considered

### Option A: Less theming clásico
- **Pros:** mecanismo "histórico" de ant-design v4/ng-zorro, documentado ampliamente.
- **Cons:** requiere reemplazar `styles.css` por `.less`, importar
  `~ng-zorro-antd/ng-zorro-antd.less` con overrides de variables Less (`@primary-color: ...`) antes
  del import, y `less` pasa de ser peer-dependency opcional de `@angular/build` a una dependencia
  real y explícita del build. Cambia el pipeline de compilación de estilos del proyecto entero.

### Option B: `ng-zorro-antd.variable.css` + overrides con CSS custom properties
- **Pros:** el paquete ya trae este archivo alternativo — sus reglas de componente usan
  `var(--ant-primary-color, ...)` en vez de valores fijos (confirmado: 361 ocurrencias). Cambiar de
  hoja de estilos es una línea en `angular.json`; los overrides son `:root { --ant-primary-color:
  #FE6944; ... }` en el `styles.css` ya existente. No agrega ninguna dependencia npm nueva, no cambia
  el builder ni la extensión de los archivos de estilo.
- **Cons:** ant-design v4/ng-zorro no tiene un slot "secondary" nativo entre sus variables —
  `--app-color-secondary` se define como variable propia del proyecto en el mismo bloque `:root`, no
  como parte del catálogo oficial de ng-zorro.

## Decision

Se eligió la **Opción B**. Verificado con `grep` sobre `node_modules/ng-zorro-antd/`: el archivo
`.variable.css` referencia `var(--ant-primary-color...)` 361 veces en las reglas reales de los
componentes (botones, inputs, alerts, paginación, etc.), mientras que el `.min.css` en uso hoy no usa
ninguna variable — confirma que cambiar la hoja de estilos por sí sola alcanza para que la
sobreescritura en `:root` propague a todos los componentes, sin tocar Less ni el pipeline de build.

## Consequences

- `frontend/angular.json`: en el array `styles`, `ng-zorro-antd.min.css` → `ng-zorro-antd.variable.css`.
- `frontend/src/styles.css`: bloque `:root` con `--ant-primary-color` (+ hover/active/outline
  derivados — `outline` es la usada por ng-zorro en el box-shadow de foco de inputs/botones) y
  `--app-color-secondary` (+ derivados) — variable propia del proyecto, no del catálogo de ng-zorro.
- Ningún componente existente cambia: los overrides son transversales, sin tocar código de features.
- Si en el futuro se necesita un mecanismo de theming más allá de lo que expone
  `.variable.css` (por ejemplo, tokens que ese archivo no cubre), la vía es evaluar Less en un ADR
  propio — no agregarlo por atajo dentro de este ticket.
