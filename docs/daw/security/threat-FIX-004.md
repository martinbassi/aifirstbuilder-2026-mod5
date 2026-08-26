# Threat model FIX-004: Fix clasificación NSFW de imágenes WebP

| Field | Value |
|-------|-------|
| Ticket | FIX-004 |
| Fix-plan | docs/daw/specs/fix-FIX-004.md |
| Date | 2026-08-26 |

## Componente analizado

`backend/src/Paretto.Infrastructure/Moderation/NsfwSpyClassifier.cs` (`IsNsfw`) — agrega un chequeo
de firma WebP y, si aplica, un reencode explícito a PNG con `ImageMagick.MagickImage` antes de
delegar en `_nsfwSpy.ClassifyImage`.

## Trust boundary

Bytes de la foto de un mural (subidos por un usuario autenticado, ya validados por firma mágica y
tamaño ≤10MB en `CreateMuralCommandValidator`, spec FEAT-001b Block 4) cruzan hacia una librería de
parseo de imágenes nativa de terceros (ImageMagick, vía Magick.NET). Este boundary **ya existía**
antes de este fix: `NsfwSpy.dll` invoca exactamente el mismo constructor (`new
MagickImage(imageData)`) sobre los mismos bytes de forma interna, condicionado a que la extensión
detectada sea `webp`. El fix mueve esa misma llamada a nuestro propio código, no la introduce de
cero — no es una superficie de ataque nueva, es la misma superficie ya presente hecha explícita y
bajo nuestro control (más auditable, no menos seguro).

## STRIDE

| Categoría | Análisis |
|---|---|
| **Spoofing** | N/A — este componente no autentica nada; la identidad del caller ya la estableció `[Authorize]` en el controller, capas arriba. |
| **Tampering** | Un WebP adversarial diseñado para evadir la clasificación NSFW tras el reencode a PNG no gana nada hoy: tanto `Clean` como `Inconclusive` dejan el mural en `Pending` (AC-08), nunca lo auto-publican (RF-013 excluye `Pending` de resultados/mapa). El fix en todo caso **mejora** la cobertura real de detección (antes, WebP nunca se clasificaba de verdad). |
| **Repudiation** | Sin cambios: el logging de fallos (Warning con la excepción) ya existe en `NsfwSpyContentScanner` y sigue aplicando igual si el reencode fallara. |
| **Information Disclosure** | Sin cambios: ni antes ni después se loguean bytes de imagen ni resultados de clasificación con contenido sensible. |
| **Denial of Service** | Decodificar una imagen WebP con dimensiones grandes (decompression-bomb: archivo pequeño, bitmap decodificado enorme) puede consumir CPU/memoria nativa de forma notable. **Ya mitigado**: `NsfwSpyContentScanner.ScanAsync` corre `IsNsfw` en una carrera contra un timeout de 5s (mitigación R6 de FEAT-001b, sin cambios) — un reencode colgado o lento sigue devolviendo `Inconclusive` sin bloquear el request. El `using var image = new MagickImage(...)` del fix libera el handle nativo de ImageMagick apenas termina, evitando fugas de memoria nativa acumuladas entre requests. |
| **Elevation of Privilege** | N/A — no hay boundary de privilegios en este componente. |

## Riesgos

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| Reencode de un WebP con dimensiones grandes consume CPU/memoria de forma notable (decompression bomb) | D | Baja | Bajo | Ya cubierto por el timeout de 5s existente (R6, FEAT-001b) — sin mitigación nueva necesaria. `using` libera el handle nativo de `MagickImage` inmediatamente. |
| Un WebP corrupto que pasó la validación de firma (RIFF/WEBP) pero no es decodificable por ImageMagick hace fallar el reencode | I/D | Baja | Bajo | La excepción sube igual que cualquier otra hasta el catch-all de `NsfwSpyContentScanner`, que ya la atrapa y devuelve `Inconclusive` (mismo camino que hoy, cubierto por AC-08 y por el test existente `NsfwSpyContentScannerTests.Underlying_classifier_throws_...`). |

Sin riesgos CRITICAL ni HIGH. No hay datos clasificables como PII/credenciales nuevos en este fix
(las fotos de murales ya fueron clasificadas en el threat model de FEAT-001b); no aplica F-TM-07.

## Mitigaciones a incorporar al fix-plan

Ninguna nueva — ambos riesgos identificados ya están cubiertos por mitigaciones existentes
(timeout de 5s de FEAT-001b, catch-all de `NsfwSpyContentScanner`) que este fix no modifica.

---

Riesgos: C:0 H:0 M:0 L:2
Resultado: **PASSED**
