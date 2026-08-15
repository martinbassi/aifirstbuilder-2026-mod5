# PRD FEAT-001d: Descubrir murales cercanos

| Field | Value |
|-------|-------|
| Ticket | FEAT-001d |
| Tracker | none |
| Date | 2026-08-15 |
| PRD loops | 0 |

## Context and Problem

Es el cuarto y último sub-ticket de FEAT-001 (ver `docs/daw/prd/prd-FEAT-001.md`, índice del split).
Depende de FEAT-001b (murales existentes, servicio de fotos firmadas) y de FEAT-001c (murales que
pueden llegar a "publicado" — sin eso, este sub-ticket construiría una búsqueda que nunca tiene nada
real que devolver). Con a+b+c+d completos, el producto cierra por primera vez el circuito completo
descrito en el objetivo de `docs/daw/prd/PRD.md`: que una persona capture un mural y otra pueda
descubrirlo cerca de su ubicación.

## Goals

- Que cualquier persona, con o sin sesión, pueda encontrar y explorar los murales publicados cerca
  de una ubicación.
- Que el mapa y la búsqueda solo muestren contenido que ya pasó por moderación (FEAT-001c).
- Que la aplicación decida explícitamente qué pantalla mostrar al abrirse, según haya o no sesión.

## Functional Requirements

- FR-01: El sistema debe mostrar murales publicados dentro de un radio configurable alrededor de la
  ubicación del usuario, con 5 km como valor por defecto. (RF-005)
- FR-02: El sistema debe excluir de las búsquedas y del mapa públicos los murales en estado
  "pendiente" o "rechazado". (RF-013)
- FR-03: El sistema debe mostrar un marcador por cada mural publicado dentro del área visible del
  mapa. (RF-006)
- FR-04: El sistema debe mostrar la fotografía, fecha de creación y ubicación de un mural publicado
  seleccionado. (RF-007)
- FR-05: El sistema debe ordenar los resultados de murales cercanos de menor a mayor distancia
  respecto de la ubicación del usuario. (RF-008)
- FR-06: El sistema debe informar al usuario cuando no existan murales publicados dentro del radio
  de búsqueda configurado. (RF-012)
- FR-07: El sistema debe permitir acceder a la pantalla de exploración de murales (mapa/lista) sin
  requerir sesión activa. (parte de RF-050 — la exploración en sí no exige autenticación)
- FR-08: El sistema debe mostrar la pantalla de inicio de sesión al abrir la aplicación cuando no
  hay sesión activa, y la pantalla de exploración de murales cuando sí la hay. (RF-050, adaptado —
  sin la rama de Administrador, que no tiene pantalla propia en este sub-ticket)

## Non-Functional Requirements

- NFR-01: La lista de murales cercanos debe mostrarse en menos de 3 segundos para el 95% de las
  consultas. (RNF-001)

## Acceptance Criteria

- AC-01: WHEN el usuario solicita murales cercanos sin modificar el radio de búsqueda, THE sistema
  SHALL mostrar únicamente murales publicados dentro de los 5 kilómetros configurados por defecto.
  (FR-01)
- AC-02: IF un mural está en estado "pendiente" o "rechazado", THEN THE sistema SHALL excluirlo de
  las búsquedas y del mapa públicos, sin importar la ubicación de quien consulta. (FR-02)
- AC-03: WHEN existen murales publicados dentro del área visible del mapa, THE sistema SHALL
  mostrar un marcador por cada mural en su ubicación correspondiente. (FR-03)
- AC-04: WHEN el usuario selecciona un mural publicado desde la lista o el mapa, THE sistema SHALL
  mostrar su fotografía (mediante la URL firmada del servicio de FEAT-001b), fecha de creación y
  ubicación. (FR-04)
- AC-05: WHEN se muestran los resultados de murales cercanos, THE sistema SHALL ordenarlos de menor
  a mayor distancia respecto de la ubicación del usuario. (FR-05)
- AC-06: IF no existen murales publicados dentro del radio de búsqueda configurado, THEN THE sistema
  SHALL informar al usuario que no se encontraron resultados, sin ofrecer una ampliación automática
  del radio. (FR-06)
- AC-07: WHEN un visitante sin sesión activa navega directamente a la pantalla de exploración de
  murales, THE sistema SHALL mostrársela sin exigir autenticación. (FR-07)
- AC-08: WHEN un usuario sin sesión activa abre la aplicación, THE sistema SHALL mostrarle la
  pantalla de inicio de sesión. (FR-08)
- AC-09: WHEN un usuario con sesión activa abre la aplicación, THE sistema SHALL mostrarle la
  pantalla de exploración de murales (mapa/lista). (FR-08)

## Out of Scope

- **RF-021** Ampliar el radio de búsqueda automáticamente en pasos (5→10→20 km) cuando no hay
  resultados. De baja prioridad frente al circuito completo del producto; se corta para no agrandar
  este sub-ticket y queda para un ticket de mejoras de búsqueda posterior. FR-06/AC-06 cubren el
  caso sin resultados con un mensaje informativo, sin la ampliación automática.
- **RF-014** Reportar mural.
- **RF-031, RF-032** "Mis murales" (listado propio, eliminación) — ya excluido en FEAT-001b.
- **RF-025, RF-027, RF-028, RF-029, RF-030** Moderación por administrador — cubierta en FEAT-001c;
  este sub-ticket solo consume el estado "publicado" que ella produce, no lo modifica.
- **RF-035, RNF-005** Idioma automático / i18n. Las pantallas de este sub-ticket se escriben
  únicamente en español.
- **RNF-007** Instalabilidad como PWA y comportamiento offline del shell.
- **Pantalla de moderación en la ruta raíz.** El rol Administrador no tiene una rama propia en FR-08
  de este sub-ticket porque no existe una pantalla de moderación construida en FEAT-001c con su
  propia vista de entrada; queda como ajuste menor para cuando ese sub-ticket la agregue.

## Risks and Mitigations

### Nada que mostrar si FEAT-001c no está desplegado

**Riesgo:** este sub-ticket depende funcionalmente de que existan murales en estado "publicado". Sin
FEAT-001c desplegado, la búsqueda siempre devuelve "sin resultados" (comportamiento correcto, pero
sin valor demostrable).

**Mitigación:** el orden de implementación a→b→c→d (ver PRD índice) garantiza que FEAT-001c esté
completo antes de este sub-ticket. Para pruebas automatizadas, los murales "publicados" se pueden
sembrar directamente en la base de datos sin pasar por la UI de moderación.

### Rendimiento de la búsqueda por cercanía

**Riesgo:** una consulta geoespacial mal indexada puede no cumplir el límite de 3 segundos (NFR-01)
a medida que crece el volumen de murales.

**Mitigación:** a definir en PLAN — índice espacial en SQL Server sobre la columna de ubicación,
evaluado contra el volumen esperado para este sub-ticket.

## Dependencies

- **FEAT-001b**: murales existentes con estado y ubicación, y el servicio de fotos vía URL firmada
  que este sub-ticket reutiliza para el detalle (FR-04).
- **FEAT-001c**: murales en estado "publicado" — sin ellos, la búsqueda pública no tiene contenido
  real que mostrar.
- Leaflet (vía Angular) para el mapa de murales (RF-006), ya cubierto por el stack declarado en
  `AGENTS.md` sin necesitar justificación adicional.
- SQL Server 2025 + EF Core para la consulta geoespacial de murales cercanos.
