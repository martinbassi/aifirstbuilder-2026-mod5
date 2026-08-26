# SAST FIX-003: Revisar y corregir tests rotos por Title obligatorio en Mural + converters UTC

| Field | Value |
|-------|-------|
| Ticket | FIX-003 |
| Date | 2026-08-26 |
| Scope | Archivos modificados en CODE: backend/src/Paretto.Api/Program.cs,
  backend/tests/Paretto.Api.Tests/{CreateMuralTests,DiscoveryControllerTests,GetMuralByIdTests,LoginTests}.cs,
  frontend/src/app/features/murals/{data/mural.service.spec.ts,ui/create-mural-form.component.spec.ts} |

## Resultado

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                  │
├─────────────────────────────────────────────────────────────┤
│                                                                │
│  Secrets:                                                      │
│    ✅ F-SAST-01: sin secretos nuevos en el diff (grep sobre      │
│       líneas agregadas únicamente — las coincidencias de           │
│       "password" son fixtures de test preexistentes, sin tocar)     │
│                                                                        │
│  Injection:                                                            │
│    ✅ F-SAST-02: sin queries nuevas (Program.cs solo registra           │
│       JsonOptions; los tests no construyen SQL)                          │
│    ✅ F-SAST-03: sin exec/spawn/system                                    │
│    ✅ F-SAST-05: sin paths derivados de input de usuario                    │
│                                                                                │
│  XSS y funciones inseguras:                                                     │
│    ✅ F-SAST-06: sin innerHTML/dangerouslySetInnerHTML                            │
│    ✅ F-SAST-04/17: sin eval/exec/deserialización insegura                          │
│    ✅ F-SAST-08: sin criptografía débil (el diff no toca hashing/crypto)              │
│                                                                                          │
│  Resto de categorías obligatorias:                                                        │
│    ✅ F-SAST-07 (SSRF): N/A, sin llamadas HTTP salientes nuevas                             │
│    ✅ F-SAST-09 (debug mode): N/A, sin flags de entorno tocados                               │
│    ✅ F-SAST-10 (logging de datos sensibles): sin logging nuevo                                 │
│    ✅ F-SAST-11 (upload sin restricción): N/A, no se toca el límite de                            │
│       tamaño/tipo de CreateMuralCommandValidator (Program.cs solo cambia                           │
│       serialización de salida, no validación de entrada)                                             │
│    ✅ F-SAST-12 (CSRF): N/A, sin cambios de autenticación                                              │
│    ✅ F-SAST-14 (validación de input incompleta): el fix AGREGA cobertura                                │
│       de validación (título ausente/>50 chars), no la debilita                                            │
│    ✅ F-SAST-15 (error handling que filtra internals): los mensajes de                                      │
│       error de los tests nuevos usan las respuestas ya existentes de                                         │
│       ProblemDetails, sin exponer detalles internos nuevos                                                     │
│                                                                                                                    │
│  Dependencias:                                                                                                     │
│    ✅ F-SAST-13/16: dotnet list package --vulnerable --include-transitive │
│       → sin paquetes vulnerables en ninguno de los 4 proyectos. Sin        │
│       dependencias nuevas en este fix (no se tocó ningún .csproj/          │
│       package.json) → npm audit no aplica.                                  │
│                                                                                │
│  Suppressions: 0                                                               │
│                                                                                  │
│  ────────────────────────────────────────────────────────────────────────      │
│  Total: 12 clean, 0 vulnerabilidades (0 critical, 0 high)                         │
│  Report: docs/daw/security/sast-FIX-003.md                                          │
│  Next: gates.sast = true, avanzar al cierre de CODE                                    │
└─────────────────────────────────────────────────────────────┘
```
