# RCA FIX-004: Fix clasificación NSFW de imágenes WebP (desfasaje de MagickFormat entre versiones de Magick.NET)

| Campo | Valor |
|-------|-------|
| Ticket | FIX-004 |
| Fecha | 2026-08-26 |
| Reportado por | Usuario, al subir un mural con foto WebP en local |

## Síntoma

Al subir un mural con una foto en formato WebP, el backend lanza en runtime
`NsfwSpyNS.ClassificationFailedException` ("Classification of the file failed. Make sure the file is
a valid image format (jpg, png, gif etc) and has been loaded correctly.") dentro de
`NsfwSpy.dll`. La excepción es atrapada por `NsfwSpyContentScanner` (try/catch general, spec Block 3
de FEAT-001b) y el mural termina igualmente en estado `Pending`, así que no hay error visible para el
usuario final — pero la imagen WebP nunca llega a clasificarse de verdad: siempre cae al camino de
fallo.

## Root cause

`NsfwSpy` 3.5.0 (`NsfwSpyNS.NsfwSpy.ClassifyImage(byte[])`, decompilado con `ilspycmd`) tiene un
branch específico para WebP:

```csharp
FileType val = MimeGuesser.GuessFileType(imageData);
if (val.Extension == "webp")
{
    MagickImage val2 = new MagickImage(imageData);
    imageData = val2.ToByteArray((MagickFormat)179);   // reencodea antes de clasificar
}
ModelInput modelInput = new ModelInput(imageData);
// ... predicción con el modelo ML.NET
```

El entero `179` está grabado en el IL de `NsfwSpy.dll`, compilado contra `Magick.NET-Q16-AnyCPU`
**11.1.2** — la versión que NsfwSpy trae por dependencia transitiva. En esa versión, la posición 179
del enum `MagickFormat` (sin valores explícitos, numerado por orden de declaración) es `Png`.

`Paretto.Infrastructure.csproj` pinnea explícitamente `Magick.NET-Q16-AnyCPU` a **14.16.0**,
sobreescribiendo la 11.1.2 transitiva, para resolver una vulnerabilidad High detectada por
`dotnet list package --vulnerable` (documentado en el propio csproj). El comentario que justifica el
pin asume que es seguro porque *"NsfwSpy only calls the stable `MagickImage(byte[])` constructor and
`ToByteArray(MagickFormat)`, unaffected by the major-version bump"* — la firma del método sí es
estable, pero no el **valor** que NsfwSpy le pasa: entre la 11.1.2 y la 14.16.0 el enum
`MagickFormat` agregó ~12 miembros nuevos antes de esa posición (268 → 280 miembros totales), corriendo
todos los valores posteriores. Verificado decompilando ambas versiones: en 14.16.0, la posición 179 ya
no es `Png` sino `Phm` (Portable HalfFloat Map, un formato HDR sin relación con el pipeline de
clasificación).

Cadena de eventos:

1. Se sube un mural con foto WebP → `CreateMuralCommandHandler` llama a
   `NsfwSpyContentScanner.ScanAsync`, que delega en `NsfwSpyClassifier.IsNsfw` →
   `_nsfwSpy.ClassifyImage(imageBytes)`.
2. `NsfwSpy` detecta la extensión `webp` y reencoda con `MagickImage.ToByteArray((MagickFormat)179)`.
3. Ese entero, resuelto contra el `MagickFormat` de la 14.16.0 pinneada, es `Phm`, no `Png`: el WebP
   se reescribe como un archivo `.phm`, no como el PNG que NsfwSpy asumía.
4. El pipeline de ML.NET (`ModelInput`/`CreatePredictionEngine`) no puede decodificar esos bytes; la
   predicción devuelve los 4 scores (`Hentai`/`Neutral`/`Pornography`/`Sexy`) en cero.
5. El constructor de `NsfwSpyResult` interpreta la suma-cero como fallo de carga y lanza
   `ClassificationFailedException`.
6. `NsfwSpyContentScanner.ScanAsync` atrapa la excepción (nunca la propaga, por diseño de Block 3) y
   devuelve `NsfwScanResult.Inconclusive`.
7. `CreateMuralCommandHandler` mapea `Inconclusive` (igual que `Clean`) a `MuralStatus.Pending` —
   comportamiento correcto según AC-08 de `prd-FEAT-001b.md`, pero la imagen WebP nunca fue
   realmente evaluada por el modelo: cae sistemáticamente al camino de fallo, no al de éxito.

## Componente afectado

`Paretto.Infrastructure/Moderation/NsfwSpyClassifier.cs`,
`Paretto.Infrastructure/Moderation/NsfwSpyContentScanner.cs`,
`Paretto.Infrastructure/Paretto.Infrastructure.csproj` (pin de `Magick.NET-Q16-AnyCPU`).

## Por qué no se detectó en FEAT-001b ni en VERIFY

El corrective loop de FEAT-001b (VERIFY, 2026-08-22) sí ejercitó WebP para subir el branch coverage
de 67.2% a 82.7%, pero contra la resolución de dependencias vigente en ese momento — el pin a
`Magick.NET-Q16-AnyCPU` 14.16.0 se hizo en el mismo ticket, como respuesta a un hallazgo de SAST, y el
re-scan de seguridad posterior verificó ausencia de vulnerabilidades conocidas, no compatibilidad de
ordinales de enum entre versiones de una dependencia transitiva de una librería de terceros. Es un
tipo de ruptura binaria que ningún test unitario ni SAST detecta: los tests de moderación mockean
`INsfwClassifier`/`INsfwSpy` (no invocan el `NsfwSpy.dll` real), así que nunca ejercitan el branch de
reencode de `ClassifyImage`. Solo se manifiesta corriendo la app real con NsfwSpy real y una imagen
WebP real.

## PRD relacionado

`docs/daw/prd/prd-FEAT-001b.md` — revisado completo. FR-01/NFR-01 ya listan WebP como formato
soportado; FR-08/FR-09 exigen validación NSFW automática; AC-08 ya contempla el fallback a
`Pending` cuando la validación falla o no responde, y es exactamente el camino que se ejecuta hoy.
Sin gap de PRD: es un defecto de implementación (la validación automática nunca clasifica de verdad
un WebP), no un requisito no cubierto.

## Confirmación

Confirmado por el usuario el 2026-08-26.
