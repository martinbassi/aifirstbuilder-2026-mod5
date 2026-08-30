# SAST — FEAT-012: Comando único para levantar frontend+backend visibles en la LAN

**Fecha:** 2026-08-30
**Alcance:** los 3 bloques del spec (backend: `Program.cs`, `LanModeTests.cs`; frontend:
`app.config.ts`, `app.config.spec.ts`, `index.development.html`; script:
`scripts/dev-lan.sh`).
**Resultado:** ✅ **PASSED** — 0 Critical, 0 High, 0 Medium sin mitigar. 1 riesgo HIGH ya evaluado
y aceptado formalmente en `docs/daw/security/threat-FEAT-012.md` (R1, PLAN) — no es un hallazgo
nuevo de SAST, es la materialización en código de lo ya threat-modelado.

## Secrets (F-SAST-01)
- ✅ Ninguno hardcodeado en ningún archivo tocado.

## Command injection (F-SAST-03, Critical si aplica)
- ✅ `scripts/dev-lan.sh`: todas las variables citadas correctamente (`"$LAN_IP"`,
  `"$BACKEND_PID"`, `"$FRONTEND_PID"`, `"$BACKEND_DIR"`, `"$FRONTEND_DIR"`), sin `eval`, sin
  interpolación insegura de comandos. `kill -TERM -- "-$BACKEND_PID"` usa `--` correctamente para
  que `kill` no interprete el PID negativo como flag (confirmado por arch-auditor).
- ✅ `Cors__AllowedOrigins__1="http://${LAN_IP}:4200"`: `LAN_IP` viene de `hostname -I` (salida
  del propio sistema operativo, no de input externo/de red) — no hay superficie de inyección vía
  esta variable.

## Riesgo materializado — R1 (HTTP plano en LAN)
- 🟠 **Ya evaluado y aceptado en threat-FEAT-012.md.** `LanMode=true` (Program.cs, gateado a este
  modo opt-in) expone el backend por HTTP sin cifrado dentro de la LAN del desarrollador. Sin
  mitigación técnica adicional posible más allá de lo ya decidido en PLAN — riesgo aceptado
  formalmente por el usuario, con las 3 condiciones de F-TM-04 documentadas (quién, justificación,
  condiciones de revisión).

## CORS / CSP (F-SAST-07 SSRF, F-SAST-12 fuga de sesión, XSS F-SAST-06)
- ✅ CORS: sin cambios de código — `Cors__AllowedOrigins__1` se agrega vía env var al array
  existente, coincidencia exacta de string (no wildcard), gateado por `IsDevelopment()` (sin
  cambios en ese gate).
- ✅ CSP: `connect-src` extendido con `http://*:5267` — wildcard de HOST, no de origen completo;
  acotado a un puerto específico y exclusivamente a `index.development.html` (nunca al `index.html`
  de producción, verificado por arch-auditor). Riesgo residual (R2, exfiltración solo si existiera
  un XSS previo) ya evaluado como Low-Medium en el threat model, mitigado por el scope del archivo.

## Debug/dev tooling en producción (F-SAST-09)
- ✅ `LanMode` solo tiene efecto si la variable de entorno homónima está presente — en producción
  nunca se exporta (el script `dev-lan.sh` es una herramienta de desarrollo, no se invoca en
  ningún pipeline de build/deploy). `GetValue<bool>` sin la clave devuelve `false` por defecto.

## Manejo de errores (F-SAST-15)
- ✅ `LanModeTests` no introduce ningún camino de error nuevo (decisión de configuración en
  arranque). El script maneja fallos de arranque (puerto ocupado, sin interfaz LAN) mostrando el
  error real a la terminal, sin exponer detalles sensibles ni tragárselos en silencio.

## Dependencias (F-SAST-13/16)
- ✅ `npm audit --omit=dev` (frontend): 0 vulnerabilidades.
- ✅ `dotnet list Paretto.sln package --vulnerable --include-transitive` (backend, los 4
  proyectos): 0 paquetes vulnerables. Sin dependencias nuevas en ningún bloque.

## Permisos de archivo
- ✅ `scripts/dev-lan.sh`: `100755` (ejecutable, sin exceso de permisos tipo 777) — confirmado en
  el commit (`git ls-files -s`).

## Suppressions
Ninguna nueva — R1 ya está formalmente documentado como riesgo aceptado en
`docs/daw/security/threat-FEAT-012.md` (no requiere el formato de supresión de Medium, F-TM-04 es
el mecanismo correspondiente para HIGH/CRITICAL sin mitigación).

---

**Total: 0 vulnerabilidades nuevas (0 Critical, 0 High, 0 Medium sin mitigar). 1 riesgo HIGH
aceptado formalmente en PLAN (R1), sin cambios respecto a lo ya evaluado.**
**Next:** `gates.sast = true` → cerrar CODE, avanzar a VERIFY.
