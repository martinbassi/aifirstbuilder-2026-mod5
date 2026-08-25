# SAST Report FEAT-004: Sidebar de navegación global colapsable + navbar de contexto

| Field | Value |
|-------|-------|
| Ticket | FEAT-004 |
| Date | 2026-08-25 |
| Scope | Todos los archivos nuevos/modificados del ticket (5 bloques + fixes de CODE) |

## Archivos analizados

`core/layout/state/layout.store.ts`, `core/layout/ui/{sidebar,navbar,app-shell}.component.{ts,html}`,
`app.routes.ts`, `app.config.ts` (y sus `.spec.ts`).

## Secrets (F-SAST-01)

✅ 0 encontrados — sin API keys, passwords, tokens ni connection strings hardcodeados.

## Injection (F-SAST-02/03/05)

✅ N/A — sin queries SQL/NoSQL, sin `exec`/`spawn`/`system`, sin paths construidos con input de
usuario. Este ticket es 100% frontend de navegación, sin acceso a datos ni al sistema de archivos.

## XSS (F-SAST-06)

✅ 0 encontrados — sin `innerHTML`, `bypassSecurityTrust*` ni `dangerouslySetInnerHTML` en ningún
archivo. Todo el texto (título de navbar, username, ítems de menú) se renderiza vía interpolación de
Angular (`{{ }}`), auto-escapada por defecto.

## Funciones inseguras / crypto débil (F-SAST-04/08/17)

✅ 0 encontrados — sin `eval()`, sin deserialización insegura, sin criptografía.

## Debug mode / logging sensible (F-SAST-09/10)

✅ 0 encontrados — sin `console.log`/`console.debug` en código de producción (confirmado por grep).
El manejo de error de `logout()` no loguea nada, solo limpia estado y redirige.

## Upload / CSRF (F-SAST-11/12)

✅ N/A — este ticket no agrega ningún formulario de upload ni endpoint que cambie estado server-side.

## Validación de input / manejo de errores (F-SAST-14/15)

✅ N/A — no hay input de usuario nuevo que validar (el único "input" es el click en ítems de menú y
en el botón de logout/toggle, sin datos libres). El manejo de error de logout (Block 2) no expone
detalles internos: limpia sesión y redirige, sin mostrar stack traces ni mensajes del backend.

## Dependencias (F-SAST-13/16)

✅ `npm audit --audit-level=moderate` → **0 vulnerabilidades**. Sin dependencias nuevas: los íconos
usados (`CompassOutline`, `CloudUploadOutline`, `SafetyCertificateOutline`, `LogoutOutline`,
`UserOutline`, `MenuFoldOutline`, `MenuUnfoldOutline`) ya vienen con `@ant-design/icons-angular`
(dependencia transitiva de `ng-zorro-antd`, ya en el stack); `toSignal` es parte de
`@angular/core/rxjs-interop`, core de Angular.

## localStorage/sessionStorage

✅ `LayoutStore` (Block 1) NO persiste nada — deliberadamente, por PRD (Out of Scope). No agrega
lectura/escritura nueva a `sessionStorage` más allá de la que `SessionStore` ya hacía (sin cambios).

## Redirects (open redirect)

✅ 0 encontrados — todos los `router.navigate`/`redirectTo` usan rutas literales del propio código
(`/login`, `/discover`, `/`), nunca una URL derivada de input del usuario o de un query param.

## Riesgos ya evaluados en threat modeling (PLAN)

Los 3 riesgos identificados en `docs/daw/security/threat-FEAT-004.md` (regresión de guards en el
refactor de rutas, spoofing client-side de `role`, logout con red caída) ya están mitigados o
aceptados en el diseño — este scan de CODE no encuentra nada adicional más allá de lo ya
documentado ahí.

## Resultado

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                  │
├─────────────────────────────────────────────────────────────┤
│                                                                │
│  Secrets:        ✅ F-SAST-01: 0 encontrados                    │
│  Injection:       ✅ N/A (sin acceso a datos/sistema)            │
│  XSS:             ✅ F-SAST-06: 0 encontrados                    │
│  Unsafe funcs:    ✅ 0 encontrados                                │
│  Debug/logging:   ✅ F-SAST-09/10: 0 encontrados                  │
│  Upload/CSRF:     ✅ N/A                                           │
│  Input/errors:    ✅ N/A / sin fuga de detalles internos           │
│  Dependencies:    ✅ npm audit: 0 vulnerabilidades                  │
│                                                                │
│  Suppressions: 0                                                │
│                                                                │
│  ────────────────────────────────────────────────────────────│
│  Total: 0 vulnerabilidades (0 critical, 0 high, 0 medium)       │
│  Report: docs/daw/security/sast-FEAT-004.md                     │
│  Next: gates.sast = true, avanzar al cierre de CODE               │
└─────────────────────────────────────────────────────────────┘
```
