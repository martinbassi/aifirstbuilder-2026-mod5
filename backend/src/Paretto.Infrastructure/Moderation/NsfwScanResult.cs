namespace Paretto.Infrastructure.Moderation;

/// <summary>
/// Outcome of an NSFW content scan — see spec Block 3 (docs/daw/specs/spec-FEAT-001b.md).
/// Mapping to <c>MuralStatus</c> is decided by the caller (Block 4), not here: <see cref="Clean"/>
/// and <see cref="Inconclusive"/> both leave the mural <c>Pending</c> (FR-10 — a failed/unresponsive
/// scan never blocks or discards the mural), only <see cref="Nsfw"/> maps to <c>Rejected</c>.
/// </summary>
public enum NsfwScanResult
{
    Clean,
    Nsfw,
    Inconclusive,
}
