using Microsoft.Extensions.Logging;
using Paretto.Infrastructure.Moderation;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 3 (Infrastructure: validación NSFW) de FEAT-001b — <see cref="NsfwSpyContentScanner"/>.
///
/// El clasificador subyacente se sustituye por <see cref="FakeNsfwClassifier"/> (fake de mano, sin
/// mocking framework — mismo patrón que el resto de esta suite, que no usa Moq en ningún lado) y el
/// <see cref="ILogger{TCategoryName}"/> por <see cref="RecordingLogger{T}"/>, que graba las
/// invocaciones a <c>Log</c> sin depender de una librería de mocking.
/// </summary>
public class NsfwSpyContentScannerTests
{
    private static Stream SomeImageStream() => new MemoryStream([1, 2, 3, 4]);

    [Fact]
    public async Task Underlying_classifier_reports_not_nsfw_scan_returns_clean()
    {
        var classifier = new FakeNsfwClassifier(isNsfw: false);
        var logger = new RecordingLogger<NsfwSpyContentScanner>();
        var scanner = new NsfwSpyContentScanner(classifier, logger);

        var result = await scanner.ScanAsync(SomeImageStream(), CancellationToken.None);

        Assert.Equal(NsfwScanResult.Clean, result);
    }

    [Fact]
    public async Task Underlying_classifier_reports_nsfw_scan_returns_nsfw()
    {
        var classifier = new FakeNsfwClassifier(isNsfw: true);
        var logger = new RecordingLogger<NsfwSpyContentScanner>();
        var scanner = new NsfwSpyContentScanner(classifier, logger);

        var result = await scanner.ScanAsync(SomeImageStream(), CancellationToken.None);

        Assert.Equal(NsfwScanResult.Nsfw, result);
    }

    [Fact]
    public async Task Underlying_classifier_throws_scan_returns_inconclusive_and_logs_a_warning_with_the_exception()
    {
        var thrown = new InvalidOperationException("model failed to load");
        var classifier = new FakeNsfwClassifier(exceptionToThrow: thrown);
        var logger = new RecordingLogger<NsfwSpyContentScanner>();
        var scanner = new NsfwSpyContentScanner(classifier, logger);

        var result = await scanner.ScanAsync(SomeImageStream(), CancellationToken.None);

        Assert.Equal(NsfwScanResult.Inconclusive, result);

        var warning = Assert.Single(logger.Entries, e => e.LogLevel == LogLevel.Warning);
        Assert.Same(thrown, warning.Exception);
    }

    [Fact]
    public async Task Underlying_classifier_does_not_complete_within_the_timeout_scan_returns_inconclusive_without_hanging()
    {
        // Fake that ignores the CancellationToken entirely and blocks forever — exercises the
        // "un archivo malformado no puede colgar el request indefinidamente" mitigation (threat
        // model R6) regardless of whether the underlying classifier cooperates with cancellation.
        var classifier = new FakeNsfwClassifier(hangsForever: true);
        var logger = new RecordingLogger<NsfwSpyContentScanner>();
        var scanner = new NsfwSpyContentScanner(classifier, logger, scanTimeout: TimeSpan.FromMilliseconds(100));

        var scanTask = scanner.ScanAsync(SomeImageStream(), CancellationToken.None);
        var completed = await Task.WhenAny(scanTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(scanTask, completed);
        Assert.Equal(NsfwScanResult.Inconclusive, await scanTask);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Warning);
    }

    private sealed class FakeNsfwClassifier : INsfwClassifier
    {
        private readonly bool _isNsfw;
        private readonly Exception? _exceptionToThrow;
        private readonly bool _hangsForever;

        public FakeNsfwClassifier(bool isNsfw = false, Exception? exceptionToThrow = null, bool hangsForever = false)
        {
            _isNsfw = isNsfw;
            _exceptionToThrow = exceptionToThrow;
            _hangsForever = hangsForever;
        }

        public bool IsNsfw(byte[] imageBytes, CancellationToken ct)
        {
            if (_exceptionToThrow is not null)
            {
                throw _exceptionToThrow;
            }

            if (_hangsForever)
            {
                // Deliberately ignores ct — the test proves NsfwSpyContentScanner unblocks its
                // caller via the timeout race even against an uncooperative classifier.
                Thread.Sleep(Timeout.Infinite);
            }

            return _isNsfw;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }
}
