# Threat Model FEAT-001c: Moderación mínima

| Field | Value |
|-------|-------|
| Ticket | FEAT-001c |
| Spec | docs/daw/specs/spec-FEAT-001c.md |
| Date | 2026-08-22 |

## Componentes analizados

1. `LoginCommand`/`LoginResponse` (backend, modificado — Block 1) — agrega `Role` a la respuesta.
2. `ModerationController` + `GetPendingMuralsQuery` (backend, nuevo — Block 2).
3. `ApproveMuralCommand` (backend, nuevo — Block 3) — agrega `MuralStatus.Published`.
4. `RejectMuralCommand` (backend, nuevo — Block 4).
5. `SessionStore`/`AuthService` (frontend, modificado — Block 5) — rol en la sesión.
6. `adminGuard`/`ModerationService` (frontend, nuevo — Block 6).
7. `PendingMuralsListComponent` (frontend, nuevo — Block 7).

## Trust boundaries (F-TM-02)

| Boundary | Ya existía / es nuevo |
|---|---|
| Browser (no confiable) ↔ API (`Authorization: Bearer {token}`) | Ya existía (FEAT-001a). Este spec agrega una sub-superficie dentro de la misma frontera: 3 endpoints que además del token válido exigen el rol `Administrator`. |
| API ↔ SQL Server (EF Core, queries parametrizadas) | Ya existía. Sin cambios de patrón. |
| API ↔ Azure Storage (SAS de lectura, 5 min) | Ya existía (FEAT-001b), reutilizado sin cambios para las fotos de murales pendientes. |
| Frontend store (`SessionStore`, afirmado por el cliente) ↔ Backend (re-verificación server-side) | **Nueva, explícita en este ticket.** El `role` que el frontend guarda es solo para UX (Bloque 6) — nunca la fuente de verdad de autorización. |

## Datos sensibles (F-TM-05 / F-TM-07)

- **`role` (Standard/Administrator):** metadata operacional, no PII/credencial/financiero. No
  requiere cifrado adicional; viaja en el mismo canal HTTPS que ya protege el token de sesión.
- **Fotos de murales pendientes:** ya clasificadas por el threat model de FEAT-001b (contenido
  generado por usuario, acceso restringido vía SAS de corta duración). Este ticket no cambia esa
  clasificación, solo extiende el acceso a "pendiente" desde "dueño" a "dueño o Administrador" —
  mismo patrón ya usado por `GetMuralByIdQueryHandler`.
- Sin PII ni credenciales nuevas → **F-TM-07 no aplica** (nada nuevo que cifrar).

## Riesgos (STRIDE)

| # | Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|---|
| R1 | Un usuario `Standard` fuerza `role: "Administrator"` en su `SessionStore` local (devtools) para que `adminGuard` lo deje entrar a `/moderation` | Elevation of Privilege / Tampering | Medium | Low | **Mitigado.** `adminGuard` es control de UX únicamente. Los 3 endpoints re-verifican el rol server-side en cada request contra `Session.User.Role`, leído fresco de la base (`SessionAuthenticationHandler`), nunca contra el valor que el cliente afirma. Como mucho, el atacante ve la pantalla — sus acciones de aprobar/rechazar devuelven 403. Nota explícita agregada al spec (Block 6). |
| R2 | Un usuario pierde el rol Administrador (downgrade) pero su sesión existente sigue aceptándose para moderar, por rol cacheado en el token | Elevation of Privilege | Low | Medium | **Ya mitigado por diseño existente, sin trabajo nuevo.** `SessionAuthenticationHandler` lee `Session.User.Role` de la base en cada request (no cachea el rol en el token/claims al momento del login) — un downgrade se refleja en el siguiente request, no requiere revocar la sesión. |
| R3 | Falta de trazabilidad: no queda registro de qué Administrador aprobó/rechazó cada mural | Repudiation | Medium | Low | **Riesgo aceptado.** El PRD (`prd-FEAT-001c.md`, "Out of Scope") excluye explícitamente "Panel de administración avanzado (analíticas, gestión de usuarios/roles, auditoría)" — una bitácora de moderación cae dentro de esa exclusión. Aceptado por: el usuario (mismo alcance ya aprobado en DEFINE). Justificación: moderación mínima, un solo administrador esperado en esta etapa del producto — mismo argumento que el PRD ya usa para aceptar RF-051 (concurrencia). Revisar cuando exista más de un administrador activo o se aborde el panel avanzado (mismo disparador que RF-051). |
| R4 | `GetPendingMuralsQuery` no pagina — un volumen alto de murales pendientes devuelve una lista sin límite | Denial of Service | Low | Low | **Mitigado (decisión del usuario, no aceptado).** `GetPendingMuralsQuery` pagina con `page`/`pageSize`, `pageSize` acotado por FluentValidation a `1..50` — un cliente no puede pedir una página sin límite. Ver Block 2 de `spec-FEAT-001c.md`. |
| R5 | Un mural ya `Published`/`Rejected` es re-moderado por una condición de carrera entre dos administradores actuando casi simultáneamente | Tampering | Low | Low | **Ya aceptado en el PRD** (RF-051, "Risks and Mitigations" de `prd-FEAT-001c.md`) — no es un hallazgo nuevo de este threat model, se re-confirma que el diseño (chequeo de estado leído-y-verificado, sin locking real) es consistente con esa aceptación previa. |

Sin riesgos CRITICAL ni HIGH identificados. No se requiere ningún cambio de arquitectura.

## Mitigaciones plegadas al spec

1. Nota explícita en Block 6 (`spec-FEAT-001c.md`): `adminGuard`/`role` del frontend es únicamente
   control de UX; la autorización real vive exclusivamente en `[Authorize(Roles = "Administrator")]`
   del backend, re-verificado server-side en cada request (R1).
2. Paginación server-side en `GetPendingMuralsQuery` (Block 2), `pageSize` acotado a `1..50` por
   FluentValidation — decisión explícita del usuario de mitigar R4 en vez de aceptarlo.

Sin cambios adicionales de diseño — R2 y R5 ya estaban cubiertos por controles existentes; R3 queda
como riesgo aceptado, documentado arriba con las 3 condiciones que exige F-TM-04 (aceptado por el
usuario en esta conversación). R4 queda mitigado, no aceptado.
