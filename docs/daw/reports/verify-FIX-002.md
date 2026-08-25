# Reporte de verificación — FIX-002: Assets rotos en discovery (marcadores Leaflet, CSP local, fallback center, foto sin max-width)

**Tier:** FIX
**RCA:** `docs/daw/specs/rca-FIX-002.md`
**Fix-plan:** `docs/daw/specs/fix-FIX-002.md`
**Threat model:** `docs/daw/security/threat-FIX-002.md`
**SAST:** `docs/daw/security/sast-FIX-002.md`
**Agente:** `daw-module-verifier` (cross-check, no escribió el código)

---

## Ronda 1 — 2026-08-25 — Resultado: **PASSED**

### Fix-plan steps (F-VER-02) — verificados en código, no asumidos

| Paso | Resultado |
|---|---|
| 1. Override íconos Leaflet + `FALLBACK_CENTER` Montevideo | ✅ `discovery-map.component.ts:19-25,36` |
| 2. `max-width:300px` en `discovery-list.component.html` | ✅ línea 16 |
| 3. `max-width:300px` en `pending-murals-list.component.html` | ✅ línea 9 |
| 4. `index.html` revertido a su CSP original | ✅ `git diff origin/main -- frontend/src/index.html` vacío |
| 5. `index.development.html` nuevo, con Azurite en `img-src` | ✅ línea 25 |
| 6. `angular.json` → `index` override en `development` (no `fileReplacements`, corregido en CODE) | ✅ `angular.json:78-82` |
| 7. 5 PNG de Leaflet trackeados en git | ✅ 5/5 |

### Regression tests por causa raíz (F-VER-01) — assertion leída, no solo el nombre

| Defecto RCA | Test | Veredicto |
|---|---|---|
| #1 Íconos Leaflet rotos | `discovery-map.component.spec.ts:90-98` | ✅ Reproduce el bug: sin el `mergeOptions`, `marker.src` no contendría `images/leaflet/marker-icon.png`. |
| #2 Fallback center (0,0) | `discovery-map.component.spec.ts:103-111` | ✅ Reproduce el bug: pre-fix `FALLBACK_CENTER = {0,0}`, el assert de Montevideo fallaría. |
| #3 CSP sin origen Azurite | N/A, documentado como no automatizable | ⚠️ Justificado — jsdom no aplica CSP real de navegador; verificado por diff en su lugar. |
| #4 Foto sin `max-width` (discovery-list) | `discovery-list.component.spec.ts:84` | ✅ Reproduce el bug, aunque como assertion embebida en un test existente (AC-04) en vez de un test independiente como pedía el fix-plan — **WARN de forma**, no de fondo. |
| #4 Foto sin `max-width` (moderación) | `pending-murals-list.component.spec.ts:68-78` | ✅ Test independiente, tal como pedía el fix-plan. |

### Cobertura (F-VER-03) — revisión manual, `@vitest/coverage-v8` no instalado

Confirmado no instalado (mismo precedente que `verify-FEAT-003.md`). Revisión línea por línea de
todo el código nuevo/modificado: 100% del código con lógica ejecutable está cubierto por al menos un
assert que fallaría sin el fix. Los cambios de CSP/build (`index.html`, `index.development.html`,
`angular.json`) no son código ejecutable por Vitest — verificados por diff en su lugar, mismo patrón
aceptado antes.

### Sad paths (F-VER-04)

No aplica: sin inputs de usuario ni endpoints nuevos. Los sad-path tests preexistentes de
`pending-murals-list.component.spec.ts` (`load-error`, `item-error-mural-1`) siguen intactos y en
verde — sin regresión en el manejo de errores existente.

### Calidad (F-VER-05, F-VER-06)

- ✅ `npx tsc --build --noEmit tsconfig.json`: 0 errores (corrido de forma independiente por el agente).
- ✅ `npx ng lint`: 0 errores (ídem).
- ✅ Suite completa: 92/92 tests (ídem, no basado en lo reportado en CODE).

### TDD evidence (diseño)

Los 4 regression tests fueron leídos contra el comportamiento pre-fix descrito en el RCA — las 4
assertions fallarían genuinamente si el fix se revirtiera. Sin necesidad de reproducir rojo→verde
ejecutando (el fix ya estaba parcialmente escrito por el usuario antes de CODE, documentado así en
el fix-plan desde su origen).

### WARNs no bloqueantes

1. **Comentario desactualizado** en `frontend/src/index.development.html:17-18` — sigue mencionando
   `fileReplacements` como mecanismo, cuando el real (corregido en CODE) es el override de `index`.
   El propio fix-plan y el threat model ya documentan la corrección; solo el comentario en el código
   quedó desalineado. Cosmético, no funcional.
2. **Assertion embebida vs. test independiente** — el regression test de `max-width` en
   `discovery-list.component.spec.ts` quedó como una línea agregada a un test existente (AC-04) en
   vez de un `it()` propio como especificaba el fix-plan. Reproduce el bug igualmente.
3. **Checkboxes del fix-plan sin marcar** — los 5 ítems de `## Tests` en `fix-FIX-002.md` siguen
   `- [ ]` pese a estar implementados y en verde. El fix-plan es inmutable en CODE/VERIFY (no se
   edita en estas fases), así que queda como discrepancia documental para señalar, no para corregir
   acá.

### Veredicto Ronda 1

**Total: 13 PASS, 0 FAIL, 3 WARN**
**Resultado: PASSED**

`gates.verify` = `true`.
