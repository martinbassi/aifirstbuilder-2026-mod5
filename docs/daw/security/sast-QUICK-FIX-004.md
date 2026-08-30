# SAST — QUICK-FIX-004: Regenerar el cliente NSwag automáticamente al correr dotnet run

**Fecha:** 2026-08-30
**Alcance:** `backend/src/Paretto.Api/Program.cs` (único archivo tocado, 5 LOC).
**Resultado:** ✅ **PASSED** — 0 Critical, 0 High, 0 Medium.

## Secrets (F-SAST-01)
- ✅ Ninguno.

## Command injection (F-SAST-03, Critical si aplica)
- ✅ `Process.Start("nswag", "run nswag.json")` — tanto el ejecutable como los argumentos son
  strings literales fijos en el código, sin ningún dato de request/usuario interpolado. No hay
  superficie de inyección de comandos: nada que un atacante pueda controlar llega a este llamado.

## Debug/dev tooling en producción (F-SAST-09)
- ✅ Todo el bloque está gateado por `app.Environment.IsDevelopment()` — mismo `if` que ya gatea
  Swagger/CORS en este archivo (patrón preexistente, no nuevo). Nunca se ejecuta fuera de
  Development; en producción, `Process.Start` ni siquiera se referencia en tiempo de ejecución.

## Denial of Service / recursos
- ✅ Fire-and-forget (no `WaitForExit`, no bloquea el hilo de `ApplicationStarted`) — no puede
  colgar ni demorar el arranque de la app ni las requests entrantes. Si `nswag` no está instalado o
  falla, la excepción queda contenida en el callback (la infraestructura de hosting la loguea, no
  tira abajo el proceso) — degrada silenciosamente al flujo manual existente.

## Dependencias (F-SAST-13/16)
- ✅ Sin dependencias nuevas — `nswag` ya es una herramienta CLI instalada y documentada como parte
  del flujo existente del proyecto (AGENTS.md, spec-FEAT-011.md).

## Suppressions
Ninguna.

---

**Total: 0 vulnerabilidades.**
**Next:** `gates.sast = true` → cerrar CODE, avanzar a RELEASE.
