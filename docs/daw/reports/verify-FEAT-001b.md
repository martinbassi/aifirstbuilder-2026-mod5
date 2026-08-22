# Verificación — FEAT-001b: Crear mural

| Field | Value |
|-------|-------|
| Fecha | 2026-08-22 |
| Ticket | FEAT-001b |
| Tier | FEATURE |
| PRD | `docs/daw/prd/prd-FEAT-001b.md` |
| Spec | `docs/daw/specs/spec-FEAT-001b.md` (8 bloques) |
| Veredicto | **PASSED** (ronda 2, tras corrective loop) |

## Round 1 — BLOCKED

### 1. Trazabilidad PRD → Código → Tests (F-VER-01)

13/14 AC en PASS, 1 WARN, 0 FAIL:

| AC | Veredicto | Detalle |
|---|---|---|
| AC-01 | ⚠️ WARN | El único formato de imagen ejercitado en toda la suite es JPEG. FR-01/AC-01 exigen JPEG **o PNG o WebP**; el código los soporta (`IsPng`, `IsWebP`, `ExtensionFor`) pero ningún test sube un PNG/WebP real — `IsPng`/`IsWebP` solo tienen cubierta su rama `false`. |
| AC-02 a AC-14 | ✅ PASS | Ver detalle completo en el reporte del agente `daw-module-verifier` (transcripción interna de esta corrida). |

### 2. Tareas del spec, bloque por bloque (F-VER-02, F-VER-06)

7/8 bloques PASS, 1 FAIL:

| Bloque | Veredicto |
|---|---|
| 1 — Domain: `Mural` | ✅ PASS |
| 2 — Storage | ✅ PASS |
| 3 — NSFW | ✅ PASS |
| **4 — API crear mural** | ❌ **FAIL** |
| 5 — API consultar mural | ✅ PASS |
| 6 — Cliente API + servicio | ✅ PASS |
| 7 — Formulario | ✅ PASS |
| 8 — Routing protegido | ✅ PASS |

**Detalle del FAIL (Bloque 4):** el threat model (`docs/daw/security/threat-FEAT-001b.md`, riesgo
**R2, HIGH**, DoS) exige un `[RequestSizeLimit]` (~11 MB) en `MuralsController.Create` o
`RequestFormLimits` equivalente. El spec Block 4 lo deja condicionado explícitamente. No existe en
el código — el único límite activo hoy es el default de Kestrel (~28-30 MB), más laxo que lo
acordado.

### 3. Cobertura de tests (F-VER-03)

```
Lines:    247/263 = 93.9%   ✅ (≥80%)
Branches:  39/58  = 67.2%   ❌ (<80%)
Functions: 48/52  = 92.3%   ✅ (≥80%)
```

❌ **FAIL** — branch coverage por debajo del mínimo. Ramas sin ejercitar: `IsPng`/`IsWebP` (rama
verdadera), `ExtensionFor` (solo cubre `"image/jpeg"`, 2/8 ramas), ramas defensivas de
`ReadUserId`/`ReadCallerIdentity` (`?? throw`, inalcanzables en la práctica por `[Authorize]`,
mismo patrón ya aceptado en el repo), fallback de configuración de `AzureBlobStorageService`.

Frontend: no medible — `@vitest/coverage-v8` no está instalado/configurado en el repo. Reportado
explícitamente en vez de inventar un número.

### 4. Sad paths (F-VER-04)

✅ PASS — cubiertos en ambos endpoints y en el formulario.

### 5. Calidad

- ✅ F-VER-05: lint/type checker backend y frontend sin errores.
- ✅ W-VER-01: sin código muerto/imports sin usar.
- ⚠️ W-VER-02: `CreateMuralCommandHandler` 88.0% líneas (dentro de la banda 80-90%, recomendado
  subir a ≥90%).
- ✅ W-VER-03: sin tests frágiles.

### 6. Suite completa

Backend: 53/53. Frontend: 33/33.

### Resumen Round 1

```
FAILs: 2 | WARNs: 3 | PASSes: 25
Veredicto: BLOCKED
```

**Bloquean:**
1. **F-VER-03** — branch coverage 67.2% < 80%, ligado 1:1 a AC-01/WARN (PNG/WebP nunca ejercitados
   de punta a punta).
2. **F-VER-02** — Bloque 4: mitigación R2 del threat model (`[RequestSizeLimit]`/
   `RequestFormLimits` ~11MB) no implementada.

**Acción:** corrective loop VERIFY → CODE. Ninguno de los dos hallazgos requiere cambiar el diseño
aprobado en el spec — son huecos de implementación.

## Corrective loop (CODE) — resumen

Resuelto en 2 rondas de implementación (ver `.daw-state.json.history` para el detalle temporal):

- **Ronda 1:** F-VER-02 → `[RequestFormLimits(MultipartBodyLengthLimit = 11MB)]` en
  `MuralsController.Create` (se usó `RequestFormLimits` en vez de `[RequestSizeLimit]`, la
  alternativa que el propio threat model permite explícitamente, porque `[RequestSizeLimit]`
  resultó ser un no-op bajo `WebApplicationFactory`/`TestServer`, confirmado empíricamente).
  F-VER-03 mejoró de 67.2% a 75.9% (agregó tests PNG/WebP end-to-end) pero no cruzó el 80%.
- **Ronda 2:** cerró F-VER-03 agregando tests para el brazo default de `ExtensionFor` (Content-Type
  no reconocido con firma de bytes válida) y la rama de longitud insuficiente de
  `IsJpeg`/`IsPng`/`IsWebP` (archivo más corto que cualquier firma). Branch coverage final: 82.7%.
  También corrigió un WARN de naming de la auditoría de arquitectura de la ronda 1.

Cada ronda pasó por revisión en dos etapas (`daw-module-verifier` + `daw-arch-auditor`) antes de
darse por resuelta. Commit: `99bd800` (ambas rondas, un solo fix lógico). Re-scan de SAST tras el
fix: PASSED (ver addendum en `docs/daw/security/sast-FEAT-001b.md`).

## Round 2 — PASSED (re-verificación completa desde cero)

Se repitió el protocolo completo de `daw-verify-module` sin dar nada por sentado de las corridas
anteriores:

- **F-VER-01** (AC del PRD): 14/14 AC en PASS — el WARN de AC-01 (formatos JPEG/PNG/WebP no
  ejercitados) quedó resuelto por los tests de la ronda 1/2 del corrective loop.
- **F-VER-02/F-VER-06** (tareas del spec): 8/8 bloques PASS. Las 8 mitigaciones del threat model
  re-verificadas independientemente en el código (R1 a R8), no solo declaradas.
- **F-VER-03** (cobertura): Líneas 95.8%, **Branches 82.8%**, Funciones 92.3% — los tres por encima
  del mínimo del 80%.
- **F-VER-04** (sad paths): PASS.
- **F-VER-05** (lint/type checker): PASS, backend y frontend.
- Suite completa: 58/58 backend + 33/33 frontend, sin regresiones.

```
FAILs: 0 | WARNs: 1 (informativo: evidencia TDD bloque-por-bloque de la ronda original de CODE no
re-adjuntada a esta verificación puntual; el gate `tests` de esa ronda ya está aprobado y no cambió
en el corrective loop) | PASSes: 39
Veredicto: PASSED
```

`gates.verify = true`.
