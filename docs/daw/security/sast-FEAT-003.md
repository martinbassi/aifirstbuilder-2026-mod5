# SAST FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| Date | 2026-08-24 |
| Scope | Archivos tocados por Block 1 y Block 2 (ver spec-FEAT-003.md) |

## Alcance del scan

Este ticket es puramente presentacional (frontend, sin cambios de backend ni de lógica de
negocio): reestructura `AuthCardComponent` como split-screen y corrige un bug de UI (ícono de
Google) más deuda de CSS en `login-form`/`register-form`. Se escanearon los archivos tocados por
ambos bloques:

- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.{ts,html,css,spec.ts}`
- `frontend/src/app/features/auth/ui/login-form.component.{ts,html,spec.ts}`
- `frontend/src/app/features/auth/ui/register-form.component.{ts,html,spec.ts}`
- `frontend/src/app/features/auth/ui/auth-form.css`
- `frontend/src/app/app.routes.spec.ts` (fix puntual de providers de test)

## Hallazgos

**Secretos (F-SAST-01):**
✅ Sin API keys, passwords, tokens ni connection strings hardcodeados (grep sobre los 12 archivos
del scope).

**Inyección (F-SAST-02/03/05):**
✅ No aplica — sin queries, sin llamadas a `exec`/`spawn`, sin paths derivados de input de usuario.
Componentes puramente presentacionales o de formulario reactivo (`ReactiveFormsModule`), sin acceso
directo a `HttpClient` (van vía `AuthService` inyectado, sin cambios de este ticket).

**XSS y funciones inseguras (F-SAST-04/06):**
✅ Sin `innerHTML`/`dangerouslySetInnerHTML` de escritura, sin `eval()`, sin
`bypassSecurityTrust*`. La única ocurrencia de `innerHTML` en el scope es una **lectura** en
`auth-card.component.spec.ts:69` (`expect(brandPanel.innerHTML).not.toContain('carousel-dots')`),
no una escritura con input de usuario.

**Crypto débil (F-SAST-08):** ✅ No aplica — sin operaciones de crypto en este scope.

**SSRF / debug mode / logging sensible (F-SAST-07/09/10):**
✅ No aplica — sin llamadas HTTP nuevas, sin `console.log`/`console.debug` en el scope, sin flags
de debug agregados.

**Upload / CSRF (F-SAST-11/12):** ✅ No aplica — este ticket no toca el flujo de carga de murales
ni ningún endpoint.

**Validación de input incompleta (F-SAST-14):**
✅ No aplica — sin cambios a validadores de formulario (`submit()`, `Validators.*`,
`passwordComplexityValidator`), confirmado sin tocar en ambos bloques.

**Manejo de errores que filtra internals (F-SAST-15):**
✅ `errorMessage` se sigue mostrando verbatim tal como ya estaba (decisión anti-enumeración de
FEAT-001a, no modificada por este ticket).

**Dependencias (F-SAST-13/16):**
✅ `npm audit --audit-level=moderate` → 0 vulnerabilidades. Sin dependencias nuevas agregadas
(confirmado: `git diff` sobre `frontend/package.json` sin cambios).

## Suppressions

Ninguna — no hay hallazgos Medium que requieran documentación de supresión.

─────────────────────────────────────────────────────────
**Total: 12 archivos escaneados, 0 vulnerabilidades (0 Critical, 0 High, 0 Medium), 0 Low/Info**
**Next:** avanzar a la transición CODE → VERIFY
