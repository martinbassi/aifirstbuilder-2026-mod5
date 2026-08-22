using Microsoft.Extensions.Logging;

namespace Paretto.Infrastructure.Moderation;

/// <summary>
/// Wraps <see cref="INsfwClassifier"/> with a bounded, never-throwing scan — see spec Block 3
/// (docs/daw/specs/spec-FEAT-001b.md) and threat model finding R6 (a malformed file must not hang
/// the request indefinitely).
/// </summary>
public class NsfwSpyContentScanner : INsfwContentScanner
{
    private static readonly TimeSpan DefaultScanTimeout = TimeSpan.FromSeconds(5);

    private readonly INsfwClassifier _classifier;
    private readonly ILogger<NsfwSpyContentScanner> _logger;
    private readonly TimeSpan _scanTimeout;

    public NsfwSpyContentScanner(INsfwClassifier classifier, ILogger<NsfwSpyContentScanner> logger)
        : this(classifier, logger, DefaultScanTimeout)
    {
    }

    /// <summary>
    /// Overload exposing the scan timeout — defaults to the 5s required by the spec via the
    /// parameterless constructor above; overridable so tests exercising the timeout path do not
    /// need to actually wait 5 real seconds.
    /// </summary>
    public NsfwSpyContentScanner(INsfwClassifier classifier, ILogger<NsfwSpyContentScanner> logger, TimeSpan scanTimeout)
    {
        _classifier = classifier;
        _logger = logger;
        _scanTimeout = scanTimeout;
    }

    public async Task<NsfwScanResult> ScanAsync(Stream imageContent, CancellationToken ct)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            using var buffer = new MemoryStream();
            await imageContent.CopyToAsync(buffer, linkedCts.Token);
            var imageBytes = buffer.ToArray();

            var classificationTask = Task.Run(() => _classifier.IsNsfw(imageBytes, linkedCts.Token), linkedCts.Token);
            var timeoutTask = Task.Delay(_scanTimeout, linkedCts.Token);

            var completedTask = await Task.WhenAny(classificationTask, timeoutTask);

            if (completedTask != classificationTask)
            {
                // Signal cancellation to any classifier implementation that cooperates with ct (real
                // NsfwSpy does not, but this still unblocks the race deterministically and lets
                // cooperative fakes/future implementations stop promptly) — the caller of ScanAsync
                // is unblocked regardless, the classificationTask is abandoned, never awaited.
                linkedCts.Cancel();
                throw new TimeoutException($"NSFW classification did not complete within {_scanTimeout}.");
            }

            var isNsfw = await classificationTask;
            return isNsfw ? NsfwScanResult.Nsfw : NsfwScanResult.Clean;
        }
        catch (Exception ex)
        {
            // Never propagated to the caller (Block 4's Handler) and never silently discarded — the
            // Warning entry below is the observable trace that distinguishes this from the empty
            // catch AGENTS.md forbids.
            _logger.LogWarning(ex, "NSFW scan failed or timed out; treating the image as inconclusive.");
            return NsfwScanResult.Inconclusive;
        }
    }
}
