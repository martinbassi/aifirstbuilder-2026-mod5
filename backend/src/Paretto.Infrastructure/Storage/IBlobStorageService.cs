namespace Paretto.Infrastructure.Storage;

public interface IBlobStorageService
{
    /// <summary>
    /// Uploads <paramref name="content"/> as <paramref name="blobName"/>. The caller ALWAYS decides
    /// the blob name (see spec Block 2 / threat model R4) — this service never derives it from an
    /// original client file name. Returns the same <paramref name="blobName"/> received, by symmetry
    /// with the rest of this interface.
    /// </summary>
    Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct);

    /// <summary>
    /// Generates a read-only SAS URL scoped to the single blob named <paramref name="blobName"/>
    /// (never an account- or container-level SAS), valid for <paramref name="validity"/>.
    /// </summary>
    string GenerateReadSasUrl(string blobName, TimeSpan validity);
}
