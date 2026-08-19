namespace Paretto.Infrastructure.Moderation;

/// <summary>
/// Scans image content for NSFW material — see spec Block 3 (docs/daw/specs/spec-FEAT-001b.md).
/// Implementations must never propagate an exception or a timeout to the caller: any failure of
/// the underlying model is reported as <see cref="NsfwScanResult.Inconclusive"/>.
/// </summary>
public interface INsfwContentScanner
{
    Task<NsfwScanResult> ScanAsync(Stream imageContent, CancellationToken ct);
}
