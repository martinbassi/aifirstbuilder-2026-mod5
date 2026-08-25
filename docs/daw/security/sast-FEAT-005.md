# SAST FEAT-005: Geolocalización funcional y refetch de murales por área en /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-005 |
| Date | 2026-08-25 |
| Scope | `frontend/src/app/features/discovery/ui/` (Blocks 1-4) |

## Resultado

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                  │
├─────────────────────────────────────────────────────────────┤
│  Secretos: ✅ F-SAST-01 — sin hardcoded secrets                │
│  Injection: ✅ F-SAST-02/03/05 — N/A, sin queries/exec/paths   │
│    de usuario (frontend puro, misma API sin cambios)           │
│  XSS: ✅ F-SAST-06 — el `html` de L.divIcon (VISITOR_ICON,      │
│    discovery-map.component.ts:44-49) es un string 100%          │
│    estático, sin interpolación de datos de usuario/API           │
│  Funciones inseguras/crypto: ✅ F-SAST-04/08 — sin eval/exec,   │
│    sin crypto                                                   │
│  SSRF/debug/logging/upload/CSRF: ✅ F-SAST-07/09/10/11/12 —     │
│    N/A, sin endpoints nuevos ni cambios de backend               │
│  Validación de input: ✅ F-SAST-14 — el centro que usa           │
│    "Buscar en esta área" viene de `map.getCenter()` (Leaflet,    │
│    siempre numérico válido), no de texto libre del usuario        │
│  Error handling: ✅ F-SAST-15 — mismo manejo de ApiError.message  │
│    ya existente, sin cambios que expongan detalles internos        │
│  Dependencias: ✅ F-SAST-13/16 — `npm audit --omit=dev` → 0        │
│    vulnerabilidades, sin dependencias nuevas agregadas              │
│  Suppressions: 0                                                     │
├─────────────────────────────────────────────────────────────┤
│  Total: 9 clean, 0 vulnerabilities                              │
│  Result: PASSED                                                  │
└─────────────────────────────────────────────────────────────┘
```

Consistente con el threat model de PLAN (`docs/daw/security/threat-FEAT-005.md`): 0 CRITICAL/HIGH,
2 LOW ya mitigados por diseño (botón deshabilitado durante la carga, guarda anti-loop). Sin
hallazgos nuevos surgidos durante la implementación.
