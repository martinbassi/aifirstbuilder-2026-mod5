# PRD FEAT-001c: Moderación mínima

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| Tracker | none |
| Date | 2026-08-15 |
| PRD loops | 0 |

## Context and Problem

Es el tercer sub-ticket de FEAT-001 (ver `docs/daw/prd/prd-FEAT-001.md`, índice del split). Depende
de FEAT-001a (rol Administrador) y FEAT-001b (murales en estado "pendiente" para revisar, y el
servicio de fotos firmadas para verlos). Sin este sub-ticket, ningún mural creado en FEAT-001b tiene
un camino de vuelta a "publicado" — el producto nunca cierra el circuito completo de crear → moderar
→ publicar → descubrir, y FEAT-001d (descubrir murales cercanos) no tendría nada real que mostrar.

## Goals

- Que un usuario con rol Administrador pueda revisar los murales pendientes y decidir si se
  publican o se rechazan.
- Que ningún usuario sin rol Administrador pueda ejecutar estas acciones.
- Que exista, aunque sea mínimo, un cierre real del ciclo de vida de un mural: pendiente → publicado
  o pendiente → rechazado.

## Functional Requirements

- FR-01: El sistema debe permitir a un usuario con rol Administrador obtener el listado de murales
  en estado "pendiente". (RF-029)
- FR-02: El sistema debe permitir a un usuario con rol Administrador cambiar el estado de un mural
  de "pendiente" a "publicado". (RF-025)
- FR-03: El sistema debe permitir a un usuario con rol Administrador cambiar el estado de un mural
  de "pendiente" a "rechazado". (RF-027)
- FR-04: El sistema debe rechazar cualquier intento de listar murales pendientes, aprobar o rechazar
  un mural, realizado por un usuario sin rol Administrador.

## Non-Functional Requirements

- NFR-01: Las acciones de moderación (listar, aprobar, rechazar) deben requerir la misma sesión
  autenticada de 7 días definida en FEAT-001a; no se introduce un mecanismo de sesión distinto.

## Acceptance Criteria

- AC-01: WHEN un usuario administrador solicita el listado de murales pendientes, THE sistema SHALL
  devolver todos los murales en ese estado, incluyendo el acceso a su fotografía mediante el
  servicio de URL firmada de FEAT-001b. (FR-01)
- AC-02: IF un usuario sin rol Administrador solicita el listado de murales pendientes, THEN THE
  sistema SHALL rechazar la solicitud. (FR-04)
- AC-03: WHEN un usuario administrador aprueba un mural en estado "pendiente", THE sistema SHALL
  cambiar su estado a "publicado". (FR-02)
- AC-04: IF un usuario sin rol Administrador intenta cambiar el estado de un mural pendiente a
  "publicado", THEN THE sistema SHALL rechazar la acción y mantener el mural en estado "pendiente".
  (FR-04)
- AC-05: WHEN un usuario administrador rechaza un mural en estado "pendiente", THE sistema SHALL
  cambiar su estado a "rechazado". (FR-03)
- AC-06: IF un usuario sin rol Administrador intenta cambiar el estado de un mural pendiente a
  "rechazado", THEN THE sistema SHALL rechazar la acción y mantener el mural en estado "pendiente".
  (FR-04)

## Out of Scope

- **RF-028** Despublicar un mural ya publicado por reporte. Depende de RF-014 (reportar mural), que
  está fuera de alcance de todo FEAT-001.
- **RF-030** Listar murales reportados. Misma dependencia de RF-014.
- **RF-051** Conflicto de concurrencia en moderación (dos administradores moderando el mismo mural
  casi al mismo tiempo). De baja prioridad para este sub-ticket mínimo: se documenta como riesgo
  conocido, no como comportamiento garantizado — ver "Risks and Mitigations".
- **Panel de administración avanzado** (analíticas, gestión de usuarios/roles, auditoría) — ya
  marcado como fuera de alcance del producto en `docs/daw/prd/PRD.md`.
- **Asignación del rol Administrador vía interfaz.** Se asume que ya existe al menos una cuenta con
  ese rol, provista por el mecanismo operativo que defina FEAT-001a (seed/config) — este sub-ticket
  no agrega una forma de otorgarlo.

## Risks and Mitigations

### Conflicto de concurrencia entre administradores

**Riesgo:** dos administradores podrían moderar el mismo mural pendiente casi al mismo tiempo (uno
aprueba, el otro rechaza), y sin control de concurrencia la segunda acción podría aplicarse
silenciosamente sobre un estado que ya cambió.

**Mitigación:** aceptado como limitación conocida del MVP incremental — RF-051 queda fuera de
alcance (ver "Out of Scope"). Es un caso de baja probabilidad con un solo administrador operando en
la etapa inicial del producto; se revisa cuando exista más de un administrador activo.

### Contenido inapropiado que la validación automática no detectó

**Riesgo:** la validación NSFW automática de FEAT-001b puede no detectar todo el contenido
inapropiado (falsos negativos).

**Mitigación:** la revisión manual del administrador (FR-02, FR-03) es la segunda capa de defensa
prevista en el PRD de producto para este caso — puede rechazar manualmente un mural que la
validación automática dejó pasar.

## Dependencies

- **FEAT-001a**: rol Administrador en el modelo de usuario, y la sesión autenticada que valida quién
  puede ejecutar estas acciones.
- **FEAT-001b**: murales en estado "pendiente" sobre los que operar, y el servicio de fotos vía URL
  firmada (con su regla de acceso "dueño o Administrador" ya definida ahí) para que el administrador
  pueda ver la imagen que está revisando.
