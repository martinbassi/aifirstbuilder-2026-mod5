# Verify FIX-004: Fix clasificación NSFW de imágenes WebP

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Fix-plan | docs/daw/specs/fix-FIX-004.md |
| RCA | docs/daw/specs/rca-FIX-004.md |
| Fecha | 2026-08-26 |
| Rondas | 1 |

## Ronda 1 — daw-verify-module

### Fix-plan steps (F-VER-02 / F-VER-06)
- ✅ Paso 1 — reencode WebP→PNG por nombre de enum en `NsfwSpyClassifier.cs` (`ReencodeWebPAsPng` +
  `IsWebP`).
- ✅ Paso 2 — decisión de duplicación de `IsWebP` documentada (XML doc + fix-plan §2).
- ✅ Paso 3 — comentario del csproj actualizado (párrafo "FIX-004 correction").
- ✅ Paso 4 — `NsfwSpyClassifierTests.cs` nuevo, 4 tests.

### Tests prometidos por el fix-plan (F-VER-06)
- ✅ Regression test (NsfwSpy real) — pasa con el fix.
- ✅ Wiring test — verifica firma PNG en los bytes recibidos por el fake.
- ✅ No-op JPEG/PNG (theory, 2 casos) — verifica identidad de referencia de los bytes originales.

### Evidencia de TDD (verificada contra el código real, no solo declarada)
El verificador restauró temporalmente `NsfwSpyClassifier.cs` a su versión pre-fix (commit
`f03a76c`) y corrió la suite nueva: 2/4 tests fallan como predice el fix-plan (excepción real
`ClassificationFailedException` en el regression test; firma RIFF sin reencodar en el wiring test).
Restauró el código con el fix → 4/4 pasan. El archivo quedó igual al commit `f71ab6b` al terminar.

### Coherencia RCA ↔ fix-plan ↔ código
✅ Ataca la causa raíz, no la sortea superficialmente: reencoda el WebP a PNG **antes** de que
NsfwSpy detecte "webp" en los bytes, evitando que su branch interno roto se ejecute — usando
`MagickFormat.Png` por nombre, nunca un entero, que es exactamente el mecanismo de la falla
documentada en el RCA.

### Coverage real (`dotnet test --collect:"XPlat Code Coverage"`)
- ✅ Código nuevo/modificado (`IsNsfw` modificado, `ReencodeWebPAsPng` y `IsWebP` nuevos): **100%
  líneas, 100% branches**, 3/3 métodos nuevos cubiertos.
- ⚠️ Clase completa: 85% líneas, 100% branches — el único método en 0% es el constructor sin
  parámetros preexistente (sin tocar por este fix), fuera del alcance de F-VER-03.
- ✅ F-VER-03: cumple el mínimo de 80% en las tres métricas.
- ✅ Suite completa: 108/108 backend, sin regresiones.

### F-VER-04 (sad-path) — ⚠️ WARN no bloqueante
Hay sad-path parcial (input <12 bytes). El escenario específico "WebP con firma válida pero payload
corrupto que hace fallar el reencode" no está ejercitado directamente contra
`NsfwSpyClassifier.IsNsfw` — el fix-plan remite al test existente de `NsfwSpyContentScannerTests`
(excepción sintética inyectada vía fake), que prueba el contrato de fallback pero no el código nuevo
fallando en la práctica. Riesgo evaluado como Bajo/Bajo en el threat model; decisión documentada, no
omitida en silencio. No bloquea el veredicto.

### Quality
- ✅ Build limpio (0 warnings, 0 errores).
- ✅ Sin imports sin usar, sin código muerto.
- ✅ W-VER-03: sin tests frágiles (sin dependencia de orden, sin estado global, imágenes generadas
  dinámicamente en el propio test).

### Verificaciones adicionales
- Ningún archivo fuera de lo declarado fue tocado (confirmado contra el commit `f71ab6b`).
- Único caller de `NsfwSpyClassifier`: DI en `Program.cs:94`, sin cambios necesarios.

---

**Veredicto: PASSED — 0 FAILs, 1 WARN no bloqueante (F-VER-04 parcial), 13 checks en verde.**
