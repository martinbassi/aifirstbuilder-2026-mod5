# PRD FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Tracker | none |
| Date | 2026-08-23 |
| PRD loops | 0 |

## Context and Problem

FEAT-002 agregó el logo compartido a `/login` y `/register`, pero ambas pantallas heredan un layout
sin estilo propio: el bloque logo+formulario queda pegado arriba a la izquierda de una página en
blanco, sin centrar ni agrupar visualmente — hallazgo detectado durante la verificación visual de
FEAT-002. El usuario pidió ir más allá de solo centrar: agrupar el logo y los inputs dentro de una
card centrada, para que las pantallas de entrada a la aplicación se vean terminadas en vez de
"toscas".

## Goals

- Centrar horizontal y verticalmente el bloque de login/register en la pantalla.
- Agrupar logo + formulario dentro de una card visual (borde/sombra/padding), no un bloque suelto.
- Aplicar el mismo tratamiento a `/login` y `/register` (misma card, mismo layout).
- Reusar el componente `nz-card` de ng-zorro (ya en el stack, sin dependencia nueva) y las variables
  de tema ya definidas en FEAT-002 (`--ant-primary-color`, `--app-color-secondary`), sin introducir
  colores nuevos hardcodeados.

## Functional Requirements

- FR-01: El sistema DEBE mostrar, en `/login`, una card centrada horizontal y verticalmente en el
  viewport, conteniendo el logo y el formulario de login.
- FR-02: El sistema DEBE mostrar, en `/register`, una card centrada horizontal y verticalmente en el
  viewport, conteniendo el logo y el formulario de registro.
- FR-03: El logo DEBE ubicarse dentro de la card, por encima de los campos del formulario.
- FR-04: El fondo de la pantalla, fuera de la card, DEBE ser blanco/neutro liso (sin degradado ni
  color sólido de marca).
- FR-05: La card DEBE usar el componente `nz-card` de ng-zorro (ya instalado), sin agregar una
  dependencia nueva.
- FR-06: La card DEBE mantenerse completamente visible sin overflow horizontal en viewports de al
  menos 320px de ancho (mobile mínimo razonable).
- FR-07: `/login` y `/register` DEBEN compartir el mismo layout de card (mismo ancho máximo, mismo
  padding, mismo tratamiento de logo) — no dos implementaciones visualmente distintas.

## Non-Functional Requirements

- NFR-01: El ancho de la card DEBE estar acotado a un máximo de 420px, para mantener legibilidad en
  viewports grandes (desktop) sin que el formulario se estire de borde a borde.

## Acceptance Criteria

- AC-01: WHEN el usuario navega a `/login`, THE sistema SHALL mostrar una card centrada horizontal y
  verticalmente que contiene el logo y el formulario de login. (FR-01)
- AC-02: WHEN el usuario navega a `/register`, THE sistema SHALL mostrar una card centrada horizontal
  y verticalmente que contiene el logo y el formulario de registro, con el mismo tratamiento visual
  que `/login`. (FR-02)
- AC-03: WHEN la card se renderiza, THE sistema SHALL ubicar el logo por encima de los campos del
  formulario, dentro de la misma card. (FR-03)
- AC-04: WHEN las pantallas `/login`/`/register` se renderizan, THE sistema SHALL mostrar un fondo
  blanco/neutro liso fuera de la card, sin degradado ni color sólido de marca. (FR-04)
- AC-05: WHEN la card se implementa, THE sistema SHALL usar el componente `nz-card` de ng-zorro como
  contenedor, sin agregar una dependencia nueva. (FR-05)
- AC-06: WHILE el viewport tiene un ancho de al menos 320px, THE sistema SHALL mantener la card
  completamente visible, sin overflow horizontal ni contenido cortado. (FR-06)
- AC-07: IF el viewport es más angosto que el ancho máximo de la card (420px), THEN THE sistema SHALL
  adaptar el ancho de la card al viewport disponible (no desbordar ni forzar scroll horizontal).
  (FR-06)
- AC-08: WHEN se compara el layout de `/login` contra `/register`, THE sistema SHALL usar el mismo
  ancho máximo de card y el mismo padding en ambas pantallas. (FR-07)

## Out of Scope

- Corregir el contraste WCAG del botón primary (ticket de seguimiento aparte, ya identificado).
- Rediseñar `/discover` o `/moderation` — este ticket es exclusivo de login/register.
- Animaciones o transiciones de entrada/salida de la card.
- Modo oscuro / soporte de temas alternativos.
- Cambiar la lógica de validación de los formularios (mensajes de error, reglas de password, etc.) —
  solo layout/estilo visual.
- Un header/navbar global — ya excluido explícitamente en el PRD de FEAT-002 y sigue sin estar en
  alcance acá.

## Risks and Mitigations

- **Riesgo:** los estilos por defecto de `nz-card` podrían no heredar la tipografía Quicksand o los
  colores de marca definidos en `styles.css` (FEAT-002), si `nz-card` aplica sus propios estilos
  aislados. **Mitigación:** verificar en CODE que la card renderiza con la fuente y paleta ya
  definidas globalmente (sin overrides de color hardcodeados dentro del componente); si `nz-card`
  necesita ajuste, hacerlo vía las variables CSS ya existentes, no con colores nuevos.
- **Riesgo:** achicar el ancho de la card en mobile podría cortar el logo (256×256 render en un
  espacio angosto). **Mitigación:** el logo ya se muestra hoy sin controlar su tamaño en CSS (spec de
  FEAT-002 no fijó dimensiones) — este ticket puede necesitar acotar el tamaño del logo dentro de la
  card responsivamente; se resuelve en CODE, no cambia el contrato de `LogoComponent`.

## Dependencies

- FEAT-002 (identidad visual: Quicksand, paleta, `LogoComponent`) — ya mergeado a `main`. Este ticket
  reutiliza `LogoComponent` y las variables de tema que introdujo, sin modificarlos.
