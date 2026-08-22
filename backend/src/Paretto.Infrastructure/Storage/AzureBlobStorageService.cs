using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;

namespace Paretto.Infrastructure.Storage;

/// <summary>
/// Wraps <see cref="BlobServiceClient"/> for the mural photos container — see spec Block 2 and
/// NFR-03/threat model R4/R5 in docs/daw/prd/prd-FEAT-001b.md /
/// docs/daw/security/threat-FEAT-001b.md.
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly string _connectionString;
    private readonly string _containerName;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        _connectionString = configuration["AzureStorage:ConnectionString"] ?? string.Empty;
        _containerName = configuration["AzureStorage:ContainerName"] ?? "mural-photos";
    }

    public async Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct)
    {
        var containerClient = GetContainerClient();

        // NFR-03 / threat model R: the container is asserted private on every upload, explicit and
        // never implicit in "just upload the file" — CreateIfNotExistsAsync is a no-op once the
        // container already exists, so this does not re-create it on every call, only ensures it.
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            content,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: ct);

        return blobName;
    }

    public string GenerateReadSasUrl(string blobName, TimeSpan validity)
    {
        var blobClient = GetContainerClient().GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerName,
            BlobName = blobName,
            Resource = "b", // blob-scoped SAS — never account- or container-scoped
            ExpiresOn = DateTimeOffset.UtcNow.Add(validity),
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }

    private BlobContainerClient GetContainerClient()
    {
        // Built here (not cached in the constructor) so a malformed connection string or an
        // unreachable Storage account surfaces from within UploadAsync/GenerateReadSasUrl, never
        // swallowed — see spec Block 2 "Error handling".
        var serviceClient = new BlobServiceClient(_connectionString, BlobClientOptionsForCompatibility);
        return serviceClient.GetBlobContainerClient(_containerName);
    }

    // Pins the request's storage API version to the latest one the local Azurite emulator used for
    // development supports (3.35.0 → 2025-11-05); the SDK's own default (2026-06-06 in 12.29.1) is
    // newer than Azurite understands and gets rejected outright. Real Azure Storage accounts are
    // backward-compatible with older API versions, so this does not change behavior against
    // production Storage — only pins it to a version verified to work end-to-end today.
    private static readonly BlobClientOptions BlobClientOptionsForCompatibility =
        new(BlobClientOptions.ServiceVersion.V2025_11_05);
}
