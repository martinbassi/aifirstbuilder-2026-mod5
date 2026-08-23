# PRD FEAT-002: Identidad visual: tipografía Quicksand, logo y paleta de colores

| Field | Value |
|-------|-------|
| Ticket | FEAT-002 |
| Tracker | none |
| Date | 2026-08-23 |
| PRD loops | 0 |

## Context and Problem

La aplicación no tiene identidad visual propia hoy: tipografía por defecto del sistema/navegador,
sin logo en ninguna pantalla, favicon genérico de Angular sin personalizar, y sin una paleta de
colores definida (ng-zorro usa su theming por defecto). El usuario eligió un logo concreto (variante
"A": ícono de aerosol + pin de ubicación sobre fondo azul marino, con el texto "paretto — urban art
discovery" en coral) y quiere adoptarlo como base de la identidad visual del sistema completo:
tipografía, logo y paleta de colores, con impacto global (no ajustes puntuales por componente).

## Goals

- Adoptar Quicksand como tipografía del sistema completo, auto-hosteada (sin dependencia de red a
  terceros).
- Mostrar el logo en las pantallas de entrada a la aplicación (login, registro).
- Reemplazar el favicon genérico por el ícono del logo.
- Establecer una paleta de colores (primario/secundario) extraída del logo, aplicada globalmente
  sobreescribiendo las variables de theming de ng-zorro — para que todos los componentes existentes
  (botones, alerts, inputs, paginación, etc.) hereden la paleta sin tocarlos uno por uno.

## Functional Requirements

- FR-01: El sistema DEBE cargar la tipografía Quicksand (pesos regular y bold como mínimo) desde
  archivos servidos por el propio dominio (self-hosted), sin depender de Google Fonts en runtime.
- FR-02: Quicksand DEBE ser la tipografía por defecto de toda la aplicación (`font-family` global),
  incluyendo los componentes de ng-zorro.
- FR-03: El logo (variante A del usuario) DEBE mostrarse en la pantalla `/login`.
- FR-04: El logo DEBE mostrarse también en la pantalla `/register`, con el mismo tratamiento visual
  que en `/login`.
- FR-05: El favicon de la aplicación DEBE reemplazarse por una versión del ícono del logo (el
  aerosol/pin, sin el texto "paretto"), reemplazando el favicon genérico actual de Angular.
- FR-06: El sistema DEBE definir una paleta con color primario `#FE6944` (coral naranja) y color
  secundario `#0D2348` (azul marino), ambos extraídos por muestreo de píxeles del archivo del logo
  provisto por el usuario.
- FR-07: La paleta DEBE aplicarse sobreescribiendo las variables globales de theming de ng-zorro
  (Less), no mediante estilos ad-hoc por componente — de forma que cualquier componente existente o
  futuro de ng-zorro (`nz-button` tipo primary, `nz-alert`, `nz-pagination`, etc.) herede los nuevos
  colores automáticamente, sin requerir cambios en cada componente que los usa hoy.

## Non-Functional Requirements

- NFR-01: El self-hosting de Quicksand no debe requerir ampliar la Content Security Policy actual de
  `index.html` (`style-src`, `connect-src`) — los archivos de fuente se sirven como asset estático
  del propio origen (`'self'`), igual que cualquier otro recurso ya permitido.

## Acceptance Criteria

- AC-01: WHEN se carga cualquier pantalla de la aplicación, THE sistema SHALL renderizar el texto
  con la tipografía Quicksand (verificable por `font-family` computado en el DOM), sin request de
  red hacia `fonts.googleapis.com` ni `fonts.gstatic.com`. (FR-01, FR-02)
- AC-02: IF los archivos de fuente Quicksand no estuvieran disponibles en el bundle, THEN THE
  navegador SHALL usar la fuente de fallback declarada en el stack de `font-family` (degradación
  visible pero sin error de carga bloqueante). (FR-01)
- AC-03: WHEN se navega a `/login`, THE sistema SHALL mostrar el logo (variante A) en la pantalla.
  (FR-03)
- AC-04: WHEN se navega a `/register`, THE sistema SHALL mostrar el logo (variante A) en la
  pantalla, con el mismo tratamiento visual que en `/login`. (FR-04)
- AC-05: WHEN se carga la aplicación en el navegador, THE sistema SHALL mostrar el ícono del logo
  como favicon de la pestaña (no el favicon genérico anterior de Angular). (FR-05)
- AC-06: WHEN se renderiza un botón `nz-button` con `nzType="primary"` en cualquier pantalla de la
  aplicación, THE sistema SHALL mostrarlo con el color primario `#FE6944` (o su variante de
  hover/active derivada por ng-zorro), sin que el componente que lo usa haya declarado ese color
  explícitamente. (FR-06, FR-07)
- AC-07: IF se inspecciona el archivo de estilos globales del tema, THEN THE color primario y
  secundario SHALL estar definidos como variables de theming de ng-zorro (no como una clase CSS
  aislada aplicada solo a un componente puntual). (FR-07)

## Out of Scope

- Un header/navbar global con el logo — hoy no existe un layout compartido entre pantallas (cada
  feature es standalone); crear uno es un cambio de arquitectura de navegación fuera del alcance de
  este ticket.
- Rediseño de componentes más allá de lo que la sobreescritura de variables de ng-zorro logra
  automáticamente (por ejemplo, no se rediseñan layouts, espaciados ni iconografía nueva).
- Dark mode / theming dinámico — la paleta se aplica como tema único y fijo.
- Ilustraciones o assets gráficos adicionales (por ejemplo, para empty-states) — solo el logo
  provisto por el usuario.

## Risks and Mitigations

- **Riesgo:** sobreescribir variables globales de ng-zorro puede tener efectos secundarios
  inesperados en componentes ya implementados (contraste insuficiente en algún estado, por ejemplo
  `nz-alert` de error sobre el nuevo primario). **Mitigación:** verificación visual de las pantallas
  existentes (login, register, discovery, moderation, create-mural-form) tras el cambio de tema,
  como parte de VERIFY.
- **Riesgo:** el archivo de fuente Quicksand aumenta el tamaño del bundle inicial.
  **Mitigación:** usar `font-display: swap` y limitar los pesos incluidos a los efectivamente
  usados (regular + bold), evaluado contra el presupuesto de bundle ya ajustado en FEAT-001d.

## Dependencies

- Ninguna funcional. Toca infraestructura visual transversal (`frontend/src/styles`, tema de
  ng-zorro, `index.html`) y dos pantallas ya existentes (`login-form`, `register-form`) de
  FEAT-001a.
- FEAT-001d: el presupuesto de bundle de producción (`angular.json`, budgets) ya fue ajustado una
  vez en ese ticket (por Leaflet); el peso agregado por Quicksand self-hosted se evalúa contra ese
  mismo presupuesto, no contra el original.
