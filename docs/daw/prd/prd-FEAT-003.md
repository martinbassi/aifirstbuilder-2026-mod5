# PRD FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Tracker | none |
| Date | 2026-08-23 |
| PRD loops | 1 |

## Context and Problem

FEAT-002 agregó el logo compartido a `/login` y `/register`, pero ambas pantallas heredan un layout
sin estilo propio: el bloque logo+formulario queda pegado arriba a la izquierda de una página en
blanco, sin centrar ni agrupar visualmente — hallazgo detectado durante la verificación visual de
FEAT-002. El usuario pidió ir más allá de solo centrar: darle a las pantallas de entrada a la
aplicación una identidad visual terminada en vez de "toscas".

**Loop de PRD (2026-08-24):** la implementación se ejecutó siguiendo la línea original ("card
centrada chica con `nz-card` sobre fondo liso"), pero la dirección visual real que se construyó y
aprobó fue distinta: un layout split-screen con un panel de marca a pantalla completa. Este loop
reescribe Goals/FR/NFR/AC/Out of Scope/Risks/Dependencies para que el PRD describa lo que
efectivamente se construyó, en vez de lo que se planeó originalmente.

## Goals

- Dividir la pantalla en dos paneles: uno de marca (mensaje + imagen institucional, color primario
  de la paleta de FEAT-002) y uno de formulario (login o registro), en vez de una card chica sobre
  fondo blanco.
- Aplicar el mismo tratamiento a `/login` y `/register` (mismos paneles, mismos anchos, mismos
  breakpoints).
- Colapsar a un único panel (el de formulario, a ancho completo) en viewports angostos, para no
  perder usabilidad en mobile.
- Reusar la paleta de color ya definida en FEAT-002 (coral primario / navy secundario) para el panel
  de marca.

## Functional Requirements

- FR-01: El sistema DEBE mostrar, en `/login`, un layout de dos paneles (panel de marca a la
  izquierda, panel de formulario a la derecha) en viewports de al menos 900px de ancho.
- FR-02: El sistema DEBE mostrar, en `/register`, el mismo layout de dos paneles que `/login`.
- FR-03: El panel de marca DEBE mostrar un wordmark de texto ("Paretto."), un mensaje de marca y una
  imagen de fondo institucional, con el color primario de la paleta definida en FEAT-002.
- FR-04: El panel de formulario DEBE centrar su contenido (encabezado + formulario) verticalmente,
  con un ancho máximo de 420px.
- FR-05: Por debajo de un ancho de viewport de 700px, el panel de marca DEBE ocultarse por completo
  y el panel de formulario DEBE ocupar el 100% del ancho disponible.
- FR-06: `/login` y `/register` DEBEN compartir el mismo layout estructural (mismos anchos de panel,
  mismos breakpoints, mismo tratamiento visual) — solo cambia el contenido del formulario.
- FR-07: Ambas pantallas DEBEN incluir un botón "Continuar con Google", visualmente presente pero sin
  ninguna acción asociada en este ticket (no dispara autenticación real).

## Non-Functional Requirements

- NFR-01: El ancho del contenido del panel de formulario DEBE estar acotado a un máximo de 420px en
  viewports de al menos 700px de ancho, para mantener legibilidad sin estirarse de borde a borde.

## Acceptance Criteria

- AC-01: WHEN el usuario navega a `/login` en un viewport de al menos 900px de ancho, THE sistema
  SHALL mostrar el layout de dos paneles (marca + formulario). (FR-01)
- AC-02: WHEN el usuario navega a `/register` en un viewport de al menos 900px de ancho, THE sistema
  SHALL mostrar el mismo layout de dos paneles que `/login`. (FR-02)
- AC-03: WHEN el panel de marca se renderiza, THE sistema SHALL mostrar el wordmark "Paretto.", el
  mensaje de marca y la imagen de fondo, con el color primario de la paleta de FEAT-002. (FR-03)
- AC-04: WHEN el panel de formulario se renderiza, THE sistema SHALL centrar su contenido
  verticalmente y limitar su ancho a un máximo de 420px. (FR-04)
- AC-05: IF el viewport es más angosto que 700px, THEN THE sistema SHALL ocultar completamente el
  panel de marca y expandir el panel de formulario al 100% del ancho disponible. (FR-05)
- AC-06: WHEN se compara el layout de `/login` contra `/register`, THE sistema SHALL usar los mismos
  anchos de panel, los mismos breakpoints y el mismo tratamiento visual en ambas pantallas. (FR-06)
- AC-07: WHEN el usuario ve el formulario de login o de registro, THE sistema SHALL mostrar un botón
  "Continuar con Google" visible, sin que su interacción dispare ninguna request ni navegación en
  este ticket. (FR-07)

## Out of Scope

- Autenticación real vía Google (OAuth): el botón "Continuar con Google" es un placeholder visual sin
  lógica asociada; su implementación funcional queda para un ticket aparte.
- Reemplazar el wordmark de texto del panel de marca por el `LogoComponent` compartido de FEAT-002 —
  es un tratamiento visual intencional y distinto al del resto de la app, específico de esta pantalla.
- Corregir el contraste WCAG del botón primary (ticket de seguimiento aparte, ya identificado).
- Rediseñar `/discover` o `/moderation` — este ticket es exclusivo de login/register.
- Animaciones o transiciones de entrada/salida de los paneles.
- Modo oscuro / soporte de temas alternativos.
- Cambiar la lógica de validación de los formularios (mensajes de error, reglas de password, etc.) —
  solo layout/estilo visual.
- Un header/navbar global — ya excluido explícitamente en el PRD de FEAT-002 y sigue sin estar en
  alcance acá.

## Risks and Mitigations

- **Riesgo:** el color del panel de marca está hardcodeado en hex (`#ff6e48`, `#0d2348`) en vez de
  referenciar las variables de tema (`--ant-primary-color`, `--app-color-secondary`) definidas en
  FEAT-002, con riesgo de desincronización si la paleta cambia más adelante. **Mitigación:** en CODE,
  reemplazar los hex hardcodeados por las variables CSS existentes donde el valor coincida.
- **Riesgo:** el archivo de estilos del panel de marca tiene una regla `.brand-panel` duplicada (una
  con `#55c1b5`, otra con `#ff6e48` al final del archivo); la segunda gana por cascada, pero es una
  fuente de confusión y deuda técnica. **Mitigación:** consolidar en una sola regla en CODE.
- **Riesgo:** el import de `LogoComponent` en `login-form.component.ts`/`register-form.component.ts`
  queda sin uso en el template (código muerto), producto de la decisión de usar wordmark de texto en
  su lugar. **Mitigación:** eliminar el import no usado en CODE.

## Dependencies

- FEAT-002 (identidad visual: Quicksand, paleta coral/navy) — ya mergeado a `main`. Este ticket
  reutiliza la paleta de color del panel de marca (ver Riesgos sobre los valores hardcodeados), pero
  **no** reutiliza `LogoComponent` en el panel de marca — decisión explícita de usar un wordmark de
  texto para esta pantalla (ver Out of Scope).
