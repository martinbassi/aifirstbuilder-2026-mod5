using ImageMagick;
using NsfwSpyNS;

namespace Paretto.Infrastructure.Moderation;

/// <summary>
/// Default <see cref="INsfwClassifier"/> wrapping the real <see cref="NsfwSpyNS.NsfwSpy"/>
/// ML.NET-based classifier.
/// </summary>
public class NsfwSpyClassifier : INsfwClassifier
{
    private static readonly byte[] RiffTag = "RIFF"u8.ToArray();
    private static readonly byte[] WebPTag = "WEBP"u8.ToArray();

    private readonly INsfwSpy _nsfwSpy;

    public NsfwSpyClassifier()
        : this(new NsfwSpy())
    {
    }

    public NsfwSpyClassifier(INsfwSpy nsfwSpy)
    {
        _nsfwSpy = nsfwSpy;
    }

    public bool IsNsfw(byte[] imageBytes, CancellationToken ct)
    {
        // The underlying model has no cancellation support of its own — ct is intentionally unused
        // here, the timeout/cancellation contract is enforced by NsfwSpyContentScanner racing this
        // call against a timer, not by this adapter.
        var bytesToClassify = IsWebP(imageBytes) ? ReencodeWebPAsPng(imageBytes) : imageBytes;
        return _nsfwSpy.ClassifyImage(bytesToClassify).IsNsfw;
    }

    /// <summary>
    /// FIX-004: NsfwSpy.ClassifyImage reencodes WebP internally via
    /// <c>MagickImage.ToByteArray((MagickFormat)179)</c> — an integer literal compiled against
    /// Magick.NET-Q16-AnyCPU 11.1.2 (where ordinal 179 is <see cref="MagickFormat.Png"/>). This
    /// project pins Magick.NET-Q16-AnyCPU to 14.16.0 (see the security note in
    /// Paretto.Infrastructure.csproj), whose MagickFormat enum inserted ~12 new members before that
    /// position — ordinal 179 there is <c>Phm</c> (Portable HalfFloat Map), not Png. NsfwSpy ends up
    /// feeding the ML.NET model bytes that are not the PNG it assumes, and classification always
    /// fails (docs/daw/specs/rca-FIX-004.md).
    ///
    /// The fix reencodes WebP ourselves, by the enum's NAME (never an integer), before NsfwSpy ever
    /// sees the bytes — its own MimeGuesser check no longer detects "webp" once this runs, so its
    /// broken internal branch is never reached. JPEG/PNG bytes pass through unchanged.
    /// </summary>
    private static byte[] ReencodeWebPAsPng(byte[] imageBytes)
    {
        using var image = new MagickImage(imageBytes);
        return image.ToByteArray(MagickFormat.Png);
    }

    /// <summary>
    /// Magic-number check (RIFF....WEBP), mirroring
    /// <c>CreateMuralCommandValidator.IsWebP</c> in
    /// Paretto.Api/Features/Murals/Commands/CreateMuralCommand.cs. Deliberately duplicated rather
    /// than shared across the Api/Infrastructure boundary — see the "duplicación consciente" note in
    /// docs/daw/specs/fix-FIX-004.md.
    /// </summary>
    private static bool IsWebP(byte[] imageBytes) =>
        imageBytes.Length >= 12
        && imageBytes.AsSpan(0, 4).SequenceEqual(RiffTag)
        && imageBytes.AsSpan(8, 4).SequenceEqual(WebPTag);
}
