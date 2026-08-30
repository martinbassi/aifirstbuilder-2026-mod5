# Reporte de verificación — FEAT-011

Ticket: **FEAT-011** — Autocompletar dirección (calle y número) en el formulario de carga de mural
Rama: `feat/FEAT-011-address-autocomplete` · Tier: FEATURE
PRD: `docs/daw/prd/prd-FEAT-001b.md` (loop 2) · Spec: `docs/daw/specs/spec-FEAT-011.md`

## Ronda 1 — 2026-08-30

Ejecutado por el agente `daw-module-verifier` (cross-verificación independiente, no escribió el código).

### Suite y calidad (confirmado independientemente, no solo el reporte de CODE)
- Backend: `dotnet test Paretto.sln` → **135/135 PASSED**.
- Frontend: `npx ng test --watch=false` → **177/177 PASSED**, 12 errores no bloqueantes (`NG04002 '/discover'`, ícono `environment-o` sin registrar) reproducidos igual — confirmado preexistente, ajeno a FEAT-011.
- `dotnet build Paretto.sln` → 0 warnings, 0 errores.
- `npx tsc --build --noEmit tsconfig.json` → 0 errores.
- `npx ng lint` → "All files pass linting".
- Cobertura medida con herramienta real:
  - Backend Block 1 (coverlet, clases `Addresses`/`Geocoding`): ~96–100% líneas, ramas ~100% salvo dos artefactos del compilador en métodos `async` sin significado real.
  - Frontend `address.service.ts` (Block 2): 100% stmts/funcs/lines, 88.9% branch.
  - Frontend `create-mural-form.component.ts/.html` (Block 3): 96.7% stmts / 85.5% branch / 100% funcs en el `.ts`; 86.9% stmts / 70% branch en el `.html`.

### Trazabilidad PRD → Código → Tests (ACs de FEAT-011)

| AC | Resultado | Detalle |
|----|-----------|---------|
| AC-03 | ✅ PASS | `requestGeolocation()` → `ReverseGeocodeQuery.cs` → test verifica valores reales, no solo status |
| AC-04 | ✅ PASS | fallback manual con geolocalización denegada, verifica DOM |
| AC-05 | ✅ PASS | `onAddressSuggestionSelected()` setea lat/lng y llama `setCoordinatesInMap()` |
| AC-17 | ⚠️ WARN | debounce/search del pipeline bien verificado (299ms/300ms); el bloque `@for` de sugerencias en el `.html` (líneas 57-65) tiene 0% de cobertura — "mostrar las sugerencias" solo verificado a nivel de signal, no en pantalla |
| AC-18 | ❌ **FAIL** | falta indicación visible de "sin resultados". El código sólo vacía `addressSuggestions`; el template no tiene mensaje ni estado visual — `nz-autocomplete` no trae `notFoundContent` como `nz-select` y no se implementó nada equivalente. El test (`create-mural-form.component.spec.ts:549`) sólo verifica el signal, nunca el DOM. Gap de implementación, no solo de test |
| AC-19 | ✅ PASS | 503 del proveedor → `AddressProviderUnavailableException` → `addressProviderUnavailable` → tests de backend (status real) y frontend (DOM real) |
| AC-20 | ✅ PASS | `address.service.ts` como única puerta a `AddressesClient`, test que fallaría si se llamara `HttpClient`/`fetch` directo |
| AC-21 | ✅ PASS | `setCoordinatesInMap()` reutilizado desde GPS y desde selección, verificado con spies concretos |

### Spec — bloques
- ✅ Block 1 (backend, 13/13 tests requeridos). ⚠️ Desviación documentada: el spec dice 400 para lat/lng fuera de rango en `reverse`; el comportamiento real (y el test) es 422, consistente con el resto del pipeline de FluentValidation del proyecto. No afecta ninguna AC; el spec debería actualizarse.
- ✅ Block 2 (frontend, 7/7 tests requeridos). `AddressSuggestion` como alias del tipo generado, cumple AGENTS.md.
- ⚠️ Block 3 (frontend, 9/9 tests requeridos presentes y en verde) — completo salvo el gap de AC-18.

### TDD evidence
❌ **FAIL** — no se encontró evidencia de TDD para ninguno de los 3 bloques. Los 3 commits de FEAT-011 (`8de1158`, `6ccf33f`, `8ecdee4`) no declaran tests-primero ni conteo rojo→verde, y no hay reporte de implementer disponible para revisar. Hallazgo de **proceso** (falta documentar), no evidencia de que no se haya hecho TDD.

### Sad paths (F-VER-04)
- ✅ search: q vacío (400), sin sesión (401), proveedor caído (503), 21ª request/min (429)
- ✅ reverse: lat/lng fuera de rango x4 (422), proveedor caído (503)
- ✅ `IdeUruguayAddressProviderClient`: `HttpRequestException`, timeout (100ms inyectado)
- ✅ `address.service.ts`: 503 en ambos métodos
- ✅ `create-mural-form`: 503 en search y en reverseGeocode, reverseGeocode devolviendo null, GPS denegado
- ⚠️ Sin cubrir (menor): rama `query.trim().length === 0` del pipeline de autocomplete (línea 177) nunca ejercitada

### Calidad
- ✅ Lint 0 errores, imports limpios, sin código muerto, sin tests frágiles
- ✅ Cobertura ≥80% en los 3 bloques
- ✅ Threat model (0 riesgos sin mitigar) y SAST (0 hallazgos) confirmados por lectura de código

### Veredicto ronda 1

```
Total: 7 PASS, 2 FAIL, 4 WARN
Result: BLOCKED
```

**FAILs:**
1. **AC-18** — falta indicación visible de "sin resultados" en el autocomplete (gap de implementación).
2. **TDD evidence** — ausente para los 3 bloques (gap de proceso/documentación).

**WARNs (no bloqueantes):**
1. AC-17 — cobertura del `@for` de sugerencias solo a nivel de signal, no de DOM.
2. Spec de Block 1 desactualizado: dice 400, el comportamiento real y correcto es 422.
3. Rama `query.trim().length === 0` sin test.
4. Dos decisiones de diseño no especificadas por el spec/PRD (limpiar `addressProviderUnavailable` tras un `search()` exitoso; limpiar `manualLocationRequired` al seleccionar una sugerencia) — bien documentadas y probadas, pero a confirmar con el usuario como comportamiento deseado.

**Acción:** corrective loop VERIFY → CODE.

## Ronda 2 — 2026-08-30 (post loop correctivo)

Fix aplicado en el commit `82eae6c`. Ejecutado por el agente `daw-module-verifier` (cross-verificación independiente).

### AC-18 — indicación de "sin resultados"
✅ **PASS.** Nuevo signal privado `addressSearchResolved` en `create-mural-form.component.ts` distingue "todavía no busqué nada" de "busqué y no hubo resultados": se resetea a `false` en cada tecla (`onAddressQueryChange`) y se fija en `true` solo cuando el pipeline de `search()` resuelve. El camino de GPS (`requestGeolocation()`/`reverseGeocode()`) escribe `addressQuery` directamente sin pasar por el `Subject`, por lo que `addressSearchResolved` permanece `false` — el mensaje no puede aparecer prematuramente con una dirección precompletada por GPS ni durante el debounce. Template: `@if (addressNoResults())` con `data-testid="address-no-results"` (líneas 67-71 del `.html`).

Tests corridos independientemente (`npx ng test --watch=false --include='**/create-mural-form.component.spec.ts'`) → **24/24 PASSED**:
- El test de "sin coincidencias" ahora consulta el DOM real (`querySelector('[data-testid="address-no-results"]')` + texto), no solo el signal.
- Test nuevo de regresión: GPS exitoso + `reverseGeocode()` con match → `addressQuery` se precompleta y el mensaje es `null` en el DOM.

### TDD evidence
✅ **Pasa de FAIL a WARN aceptado.** La sección "Evidencia TDD" en `docs/daw/specs/spec-FEAT-011.md` (líneas 388–395) declara explícitamente que los 3 bloques no siguieron TDD estricto, por decisión confirmada por el usuario — no es un hueco sin explicar, es una decisión de proceso documentada con trazabilidad al hallazgo de la ronda 1. Nota para el futuro: la próxima feature debería retomar TDD estricto; esto no es una excepción a repetir.

### Regresión completa
- ✅ Backend: `dotnet test Paretto.sln` → 135/135 PASSED.
- ✅ Frontend: `npx ng test --watch=false` → 178/178 PASSED (177 + 1 nuevo test de regresión GPS), 12 errores `NG04002 '/discover'` idénticos a ronda 1, confirmados preexistentes.
- ✅ `dotnet build` → 0 warnings. `tsc --build --noEmit` → 0 errores. `ng lint` → sin errores.
- ✅ Los otros 6 ACs (AC-03/04/05/19/20/21) y ambos bloques de backend/NSwag no fueron tocados por el fix (`git show --stat 82eae6c` acota el diff a Block 3 + spec + SAST) — se mantienen PASS de ronda 1.
- ✅ SAST ronda 2 (`docs/daw/security/sast-FEAT-011.md`): diff acotado (4 archivos, 72 líneas), 0 hallazgos.

### WARNs de ronda 1 — seguimiento
- ✅ **Resuelto:** las 2 decisiones de diseño no especificadas, confirmadas por el usuario como comportamiento deseado y documentadas en el spec ("Loop correctivo", líneas 406-408).
- ✅ **Resuelto:** spec de Block 1 corregido (400→422 en `reverse` fuera de rango, líneas 174/190).
- ⚠️ AC-17 — cobertura del `@for` de sugerencias solo a nivel de signal, no de DOM. Sigue como WARN no bloqueante, sin cambios en esta ronda.
- ⚠️ Rama `query.trim().length === 0` del pipeline sin test dedicado. Sigue como WARN no bloqueante, sin cambios en esta ronda.

### Veredicto ronda 2

```
Total: 11 PASS, 0 FAIL, 3 WARN (2 no bloqueantes de siempre + 1 TDD aceptado por decisión explícita)
Result: PASSED
```

**Conclusión: FEAT-011 verificado. `gates.verify = true`. Listo para RELEASE.**
