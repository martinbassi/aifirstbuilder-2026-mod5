namespace Paretto.Infrastructure.Moderation;

/// <summary>
/// Own abstraction over the underlying NsfwSpy classifier (see spec Block 3) — injected by
/// constructor into <see cref="NsfwSpyContentScanner"/> so it is replaceable/mockable in tests
/// without instantiating <see cref="NsfwSpyNS.NsfwSpy"/> (which loads an ML.NET model from disk)
/// directly inside the scanner.
/// </summary>
public interface INsfwClassifier
{
    /// <summary>
    /// Synchronously classifies <paramref name="imageBytes"/>. <paramref name="ct"/> is offered for
    /// cooperative cancellation by implementations that can honor it (the real NsfwSpy model does
    /// not); <see cref="NsfwSpyContentScanner"/> does not rely on it alone to bound the call — it
    /// races the invocation against an explicit timeout instead (see that class).
    /// </summary>
    bool IsNsfw(byte[] imageBytes, CancellationToken ct);
}
