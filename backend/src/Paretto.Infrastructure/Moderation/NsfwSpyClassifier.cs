using NsfwSpyNS;

namespace Paretto.Infrastructure.Moderation;

/// <summary>
/// Default <see cref="INsfwClassifier"/> wrapping the real <see cref="NsfwSpyNS.NsfwSpy"/>
/// ML.NET-based classifier.
/// </summary>
public class NsfwSpyClassifier : INsfwClassifier
{
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
        return _nsfwSpy.ClassifyImage(imageBytes).IsNsfw;
    }
}
