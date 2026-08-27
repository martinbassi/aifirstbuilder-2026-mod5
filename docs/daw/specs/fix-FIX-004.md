# Fix-plan FIX-004: Fix clasificación NSFW de imágenes WebP (desfasaje de MagickFormat entre versiones de Magick.NET)

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-004.md |
| Date | 2026-08-26 |
| Spec loops | 0 |

## Problem

Al subir un mural con foto WebP, la validación automática NSFW nunca clasifica la imagen: siempre
lanza `NsfwSpyNS.ClassificationFailedException` internamente en `NsfwSpy.dll`. La excepción es
atrapada por `NsfwSpyContentScanner` (try/catch general, no se propaga) y el mural termina en estado
`Pending` igual que cualquier resultado inconcluso — sin error visible para el usuario final — pero
la imagen WebP nunca fue realmente evaluada por el modelo: el filtro automático de contenido NSFW
está roto para ese formato, uno de los tres explícitamente soportados (FR-01/NFR-01 de
`prd-FEAT-001b.md`).

## Root cause

`NsfwSpy` 3.5.0 reencoda internamente cualquier WebP con `MagickImage.ToByteArray((MagickFormat)179)`
antes de clasificarlo. Ese entero fue compilado contra `Magick.NET-Q16-AnyCPU` 11.1.2 (donde el
ordinal 179 del enum `MagickFormat` es `Png`), pero `Paretto.Infrastructure.csproj` pinnea esa
dependencia transitiva a 14.16.0 (mitigación de una vulnerabilidad High, ver comentario del csproj).
En la 14.16.0 el enum agregó ~12 miembros nuevos antes de esa posición, así que el mismo entero 179
ahora es `Phm` (Portable HalfFloat Map). El WebP se reencoda como un archivo `.phm`, no como el PNG
que NsfwSpy asumía; el modelo ML.NET no puede decodificar esos bytes, la predicción devuelve los 4
scores en cero, y `NsfwSpyResult` interpreta eso como fallo de carga. Detalle completo en
`docs/daw/specs/rca-FIX-004.md`.

## Solution — steps

1. `backend/src/Paretto.Infrastructure/Moderation/NsfwSpyClassifier.cs` — antes de llamar a
   `_nsfwSpy.ClassifyImage(imageBytes)`, detectar si `imageBytes` es un WebP (firma mágica
   `RIFF....WEBP`, mismos bytes que ya usa
   `CreateMuralCommandValidator.IsWebP`/`Paretto.Api/Features/Murals/Commands/CreateMuralCommand.cs`)
   y, si lo es, reencodarlo nosotros mismos a PNG con `ImageMagick.MagickImage` +
   `MagickFormat.Png` (por **nombre** del enum, nunca por entero) antes de pasarlo a
   `_nsfwSpy.ClassifyImage`. Así NsfwSpy nunca detecta "webp" en los bytes que recibe — ya vienen
   como PNG — y nunca entra a su branch interno roto. `ImageMagick` (Magick.NET-Q16-AnyCPU) ya es
   dependencia directa de `Paretto.Infrastructure.csproj`; no se agrega ninguna dependencia nueva.

2. **Decisión consciente sobre duplicación** (hallazgo del impact scan): la detección de firma WebP
   se duplica entre `CreateMuralCommandValidator.IsWebP` (proyecto `Paretto.Api`, capa de validación
   de entrada) y el nuevo chequeo en `NsfwSpyClassifier` (proyecto `Paretto.Infrastructure`, capa de
   moderación). No se extrae un helper compartido entre ambos proyectos para este FIX — es una
   comprobación de 4 líneas sobre bytes RIFF/WEBP, y mover código entre capas para una corrección
   acotada iría contra el principio de esta fase ("estabilidad antes que elegancia"). Se documenta
   aquí como duplicación aceptada; si en el futuro cambia qué variantes de WebP se soportan
   (VP8L/VP8X, etc.), ambos puntos deben actualizarse juntos.

3. `backend/src/Paretto.Infrastructure/Paretto.Infrastructure.csproj` (comentario del bloque 3,
   líneas ~48-62) — actualizar el comentario que documenta el pin de `Magick.NET-Q16-AnyCPU` a
   14.16.0: agregar que ese bump rompió el reencode interno de WebP de NsfwSpy (offset de
   `MagickFormat` distinto) y que la mitigación real es el reencode explícito por nombre de enum en
   `NsfwSpyClassifier` (paso 1), no la sola actualización de versión. El comentario anterior no
   preveía este efecto y quedaría desactualizado si no se corrige.

4. `backend/tests/Paretto.Api.Tests/NsfwSpyClassifierTests.cs` (nuevo) — ver sección Tests.

## Dependencies between steps

1 y 3 son independientes entre sí; 2 es una decisión de diseño que se aplica al escribir 1, no un
paso de código separado. 4 depende de que 1 esté implementado (el test de regresión falla contra el
código sin el fix, pasa después).

## Error handling

Sin cambios en el manejo de errores existente: `NsfwSpyClassifier.IsNsfw` sigue sin atrapar
excepciones — ese contrato lo mantiene `NsfwSpyContentScanner` (try/catch general, spec Block 3 de
FEAT-001b), sin tocar. Si el reencode con `MagickImage` fallara por algún motivo (WebP corrupto que
pasó la validación de firma pero no es decodificable), la excepción que lance `MagickImage` sube
igual que cualquier otra hasta `NsfwSpyContentScanner`, que la atrapa y devuelve `Inconclusive` —
mismo comportamiento de fallback ya cubierto por AC-08 de `prd-FEAT-001b.md`, sin caso nuevo que
manejar.

## Tests

- [ ] **Regression test** (`NsfwSpyClassifierTests.cs`, nuevo) — instancia `NsfwSpyClassifier` con el
  `NsfwSpy` **real** (`new NsfwSpyNS.NsfwSpy()`, no un fake) y una imagen WebP real y válida generada
  en el propio test con `MagickImage` (ej. un lienzo sólido de pocos píxeles, `.Write(stream,
  MagickFormat.WebP)`). Antes del fix: `IsNsfw` lanza `ClassificationFailedException`. Después del
  fix: `IsNsfw` devuelve `false` sin lanzar. Es el único punto de la suite que ejercita el `NsfwSpy`
  real (hoy toda la suite lo reemplaza por fakes — hallazgo del impact scan), así que es el único
  test capaz de detectar si esta regresión reaparece (p. ej. un futuro bump de Magick.NET).
- [ ] **Wiring test** (`NsfwSpyClassifierTests.cs`) — con un `INsfwSpy` fake que graba los bytes
  recibidos en `ClassifyImage`, verifica que para una entrada WebP los bytes que llegan al fake
  empiezan con la firma PNG (`0x89 0x50 0x4E 0x47`), no con la firma WebP original. Prueba el
  wiring del reencode de forma rápida y determinística, sin depender del modelo ML.NET real.
- [ ] **No-op para JPEG/PNG** (`NsfwSpyClassifierTests.cs`) — con el mismo fake, verifica que para
  una entrada JPEG o PNG los bytes pasan sin modificar al `INsfwSpy` (no hay reencode innecesario).

El único error documentado en "Error handling" (el reencode con `MagickImage` fallando y propagando
la excepción) ya tiene cobertura existente y no requiere un test nuevo: es exactamente el escenario
que ejercita
`NsfwSpyContentScannerTests.Underlying_classifier_throws_scan_returns_inconclusive_and_logs_a_warning_with_the_exception`
(`backend/tests/Paretto.Api.Tests/NsfwSpyContentScannerTests.cs:43`) — cualquier excepción que
`IsNsfw` deje escapar, venga de donde venga, ya se prueba que cae a `Inconclusive` con warning
logueado.

## Regression risk

**Bajo.** El cambio es aditivo y acotado a un solo método (`NsfwSpyClassifier.IsNsfw`): agrega un
chequeo de firma + un reencode condicional solo para WebP; JPEG y PNG siguen el mismo camino que
hoy, sin tocar. No cambia ninguna firma pública (`INsfwClassifier`, `INsfwContentScanner`), así que
no hay callers que actualizar. El único riesgo real es que el reencode a PNG introduzca una pérdida
de calidad/color que cambie el resultado de clasificación de un WebP borderline — aceptable, porque
hoy ese mismo WebP simplemente nunca se clasifica (siempre cae a Inconclusive).

## Rollback plan

- **Pasos:** trivial — revertir el commit de este fix. `NsfwSpyClassifier` vuelve a llamar a
  `_nsfwSpy.ClassifyImage(imageBytes)` directamente sin el reencode previo, y el comportamiento
  vuelve a ser el actual (WebP siempre cae a `Inconclusive`/`Pending`, sin excepción visible para el
  usuario). No hay migración de datos ni cambio de esquema involucrado.
- **Indicadores:** si tras el fix las clasificaciones de WebP tardan notablemente más (overhead del
  reencode con ImageMagick) o si aparecen falsos negativos/positivos nuevos y específicos de WebP en
  producción, revertir y reabrir la investigación.
