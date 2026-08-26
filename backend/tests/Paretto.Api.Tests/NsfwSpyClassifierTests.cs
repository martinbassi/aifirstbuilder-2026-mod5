using System.Net;
using ImageMagick;
using NsfwSpyNS;
using Paretto.Infrastructure.Moderation;

namespace Paretto.Api.Tests;

/// <summary>
/// FIX-004 (docs/daw/specs/fix-FIX-004.md) — <see cref="NsfwSpyClassifier"/>. Root cause: NsfwSpy
/// reencodes WebP internally via a raw <c>MagickFormat</c> integer literal that resolves to a
/// different format once Magick.NET-Q16-AnyCPU is pinned past 11.1.2 (see
/// docs/daw/specs/rca-FIX-004.md). <see cref="NsfwSpyClassifier"/> now reencodes WebP to PNG itself,
/// by the enum's name, before NsfwSpy's own broken branch ever runs.
/// </summary>
public class NsfwSpyClassifierTests
{
    private static readonly byte[] MinimalJpegBytes = [0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] MinimalPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    /// A real, decodable WebP image — not a fixture with just the right magic bytes, because the
    /// regression test below classifies it with the REAL NsfwSpy model, which needs bytes it can
    /// actually decode.
    /// </summary>
    private static byte[] RealWebPImageBytes()
    {
        using var image = new MagickImage(MagickColors.Red, 4, 4);
        using var stream = new MemoryStream();
        image.Write(stream, MagickFormat.WebP);
        return stream.ToArray();
    }

    [Fact]
    public void Real_NsfwSpy_classifies_a_real_webp_image_without_throwing()
    {
        // Regression test: this is the only place in the suite that exercises the REAL NsfwSpy
        // (every other test fakes INsfwClassifier/INsfwSpy) — before the fix, this throws
        // NsfwSpyNS.ClassificationFailedException for every WebP, because NsfwSpy's internal
        // reencode resolves (MagickFormat)179 against the pinned Magick.NET 14.16.0 as Phm, not
        // Png (see rca-FIX-004.md).
        var classifier = new NsfwSpyClassifier(new NsfwSpy());

        var exception = Record.Exception(() => classifier.IsNsfw(RealWebPImageBytes(), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public void Webp_input_is_reencoded_to_png_before_reaching_the_underlying_classifier()
    {
        var recordingSpy = new RecordingNsfwSpy();
        var classifier = new NsfwSpyClassifier(recordingSpy);

        classifier.IsNsfw(RealWebPImageBytes(), CancellationToken.None);

        Assert.NotNull(recordingSpy.LastImageData);
        // PNG signature: 0x89 0x50 0x4E 0x47 — proves the WebP was reencoded, not passed through.
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], recordingSpy.LastImageData!.Take(4));
    }

    [Theory]
    [MemberData(nameof(NonWebPInputs))]
    public void Jpeg_and_png_input_reaches_the_underlying_classifier_unmodified(byte[] originalBytes)
    {
        var recordingSpy = new RecordingNsfwSpy();
        var classifier = new NsfwSpyClassifier(recordingSpy);

        classifier.IsNsfw(originalBytes, CancellationToken.None);

        Assert.Same(originalBytes, recordingSpy.LastImageData);
    }

    public static IEnumerable<object[]> NonWebPInputs()
    {
        yield return [MinimalJpegBytes];
        yield return [MinimalPngBytes];
    }

    /// <summary>
    /// Fakes <see cref="INsfwSpy"/> (third-party interface, cannot be changed) purely to record the
    /// bytes <see cref="NsfwSpyClassifier"/> hands it — no mocking framework, same hand-written-fake
    /// pattern as the rest of this suite (<see cref="NsfwSpyContentScannerTests"/>). Every member but
    /// <see cref="ClassifyImage(byte[])"/> is unused by <see cref="NsfwSpyClassifier"/> and throws.
    /// </summary>
    private sealed class RecordingNsfwSpy : INsfwSpy
    {
        public byte[]? LastImageData { get; private set; }

        public NsfwSpyResult ClassifyImage(byte[] imageData)
        {
            LastImageData = imageData;
            return new NsfwSpyResult();
        }

        public NsfwSpyFramesResult ClassifyGif(byte[] gifImage, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public NsfwSpyFramesResult ClassifyGif(string filePath, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public NsfwSpyFramesResult ClassifyGif(Uri uri, WebClient? webClient = null, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public Task<NsfwSpyFramesResult> ClassifyGifAsync(string filePath, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public Task<NsfwSpyFramesResult> ClassifyGifAsync(Uri uri, WebClient? webClient = null, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public NsfwSpyResult ClassifyImage(string filePath) => throw new NotImplementedException();

        public NsfwSpyResult ClassifyImage(Uri uri, WebClient? webClient = null) =>
            throw new NotImplementedException();

        public Task<NsfwSpyResult> ClassifyImageAsync(string filePath) => throw new NotImplementedException();

        public Task<NsfwSpyResult> ClassifyImageAsync(Uri uri, WebClient? webClient = null) =>
            throw new NotImplementedException();

        public List<NsfwSpyValue> ClassifyImages(IEnumerable<string> filesPaths, Action<string, NsfwSpyResult>? actionAfterEachClassify = null) =>
            throw new NotImplementedException();

        public NsfwSpyFramesResult ClassifyVideo(byte[] video, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public NsfwSpyFramesResult ClassifyVideo(string filePath, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public NsfwSpyFramesResult ClassifyVideo(Uri uri, WebClient? webClient = null, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public Task<NsfwSpyFramesResult> ClassifyVideoAsync(string filePath, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();

        public Task<NsfwSpyFramesResult> ClassifyVideoAsync(Uri uri, WebClient? webClient = null, VideoOptions? videoOptions = null) =>
            throw new NotImplementedException();
    }
}
