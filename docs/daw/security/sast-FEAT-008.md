# SAST FEAT-008: Reemplazar input file por NzFileUpload con loader y preview en el formulario de creación de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-008 |
| Date | 2026-08-29 |
| Scope | Cierre de CODE — diff completo del ticket (commits `b34ad13`, `6eb67ce`) |

## Archivos escaneados

- `frontend/src/app/features/murals/ui/create-mural-form.component.ts`
- `frontend/src/app/features/murals/ui/create-mural-form.component.html`
- `frontend/src/app/features/murals/ui/create-mural-form.component.spec.ts`
- `frontend/src/app/app.config.ts`

## Resultado

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                  │
├─────────────────────────────────────────────────────────────┤
│                                                                │
│  Secrets:                                                      │
│    ✅ F-SAST-01: sin API keys/passwords/tokens/connection        │
│       strings en el diff. `.env` ya estaba en `.gitignore`         │
│       (sin cambios en esta área).                                    │
│                                                                          │
│  Injection:                                                              │
│    ✅ F-SAST-02/03/05: N/A — ningún archivo backend tocado, sin           │
│       queries, sin exec/spawn, sin paths de archivo server-side           │
│       construidos con input de usuario.                                     │
│                                                                                 │
│  XSS y funciones inseguras:                                                      │
│    ✅ F-SAST-06: sin `innerHTML`/`dangerouslySetInnerHTML`. El preview            │
│       usa el binding nativo de `nz-upload-list`                                     │
│       (`&lt;img [src]="thumbUrl"&gt;`), con un `blob:` URL generado                        │
│       localmente vía `URL.createObjectURL` — nunca HTML/string                          │
│       interpretado como marcado.                                                           │
│    ✅ F-SAST-04: sin `eval()`, sin deserialización insegura.                                  │
│    ✅ F-SAST-08: sin criptografía en este diff.                                                  │
│                                                                                                      │
│  Otras categorías obligatorias:                                                                       │
│    ✅ F-SAST-07 (SSRF): N/A, sin llamadas a URLs externas nuevas.                                        │
│    ✅ F-SAST-09 (debug mode): sin flags de debug ni cambios de entorno.                                    │
│    ✅ F-SAST-10 (logging de datos sensibles): sin `console.*` en código                                      │
│       de producción (confirmado también por `daw-validate-arch`).                                              │
│    ✅ F-SAST-11 (upload sin restricción): el límite de 10 MB y los tipos                                          │
│       permitidos (JPEG/PNG/WebP) se mantienen sin cambios respecto al                                              │
│       `&lt;input&gt;` nativo anterior — documentado explícitamente en el código                                        │
│       y en el threat model (`docs/daw/security/threat-FEAT-008.md`, R1)                                              │
│       como UX-only: la autoridad real sigue siendo el backend                                                          │
│       (`RequestFormLimits` + NSFW por firma de bytes), sin tocar en este                                                 │
│       ticket.                                                                                                              │
│    ✅ F-SAST-12 (CSRF): N/A, sin cambios al flujo de autenticación/envío.                                                    │
│    ✅ F-SAST-14 (validación de input incompleta): tipo y tamaño siguen                                                          │
│       validados antes de aceptar el archivo, con mensaje de error                                                                  │
│       explícito en ambos casos (AC-04/AC-05 del PRD).                                                                                 │
│    ✅ F-SAST-15 (error handling que filtra internals): mensajes de error                                                                  │
│       genéricos orientados al usuario, sin stack traces ni detalles                                                                          │
│       internos.                                                                                                                                  │
│                                                                                                                                                       │
│  Dependencias:                                                                                                                                        │
│    ✅ F-SAST-13/16: `npm audit --audit-level=high` → 0 vulnerabilidades.                                                                                 │
│       Sin dependencias nuevas agregadas (confirmado: sin diff en                                                                                           │
│       package.json/package-lock.json) — `NzUploadModule`/`NzIconModule`                                                                                        │
│       ya eran parte de `ng-zorro-antd@^21.3.3`.                                                                                                                    │
│                                                                                                                                                                        │
│  Suppressions: 0                                                                                                                                                        │
│                                                                                                                                                                            │
│  ─────────────────────────────────────────────────────────────                                                                                                             │
│  Total: 15 clean, 0 vulnerabilities (0 critical, 0 high, 0 medium)                                                                                                           │
│  Report: docs/daw/security/sast-FEAT-008.md                                                                                                                                    │
└─────────────────────────────────────────────────────────────┘
```
