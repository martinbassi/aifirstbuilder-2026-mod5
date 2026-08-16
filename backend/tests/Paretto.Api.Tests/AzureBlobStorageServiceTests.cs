using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 2 (Infrastructure: almacenamiento de fotos) — <see cref="IBlobStorageService"/> /
/// <see cref="AzureBlobStorageService"/>. Runs against Azurite (local Azure Storage emulator,
/// "UseDevelopmentStorage=true") — see docs/daw/specs/spec-FEAT-001b.md Block 2.
/// </summary>
public class AzureBlobStorageServiceTests
{
    private const string AzuriteConnectionString = "UseDevelopmentStorage=true";

    private static readonly string ContainerName = $"mural-photos-test-{Guid.NewGuid():N}";

    private static IBlobStorageService CreateService(string connectionString, string containerName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureStorage:ConnectionString"] = connectionString,
                ["AzureStorage:ContainerName"] = containerName,
            })
            .Build();

        return new AzureBlobStorageService(configuration);
    }

    [Fact]
    public async Task UploadAsync_creates_the_container_with_no_public_access()
    {
        var service = CreateService(AzuriteConnectionString, ContainerName);
        await using var content = new MemoryStream([1, 2, 3]);

        await service.UploadAsync(content, $"{Guid.NewGuid()}.jpg", "image/jpeg", CancellationToken.None);

        // NFR-03: the container must never be created with anonymous public access — verified
        // independently of AzureBlobStorageService, straight against the emulator. Service version
        // pinned to what this Azurite install (3.35.0) supports — same reason as
        // AzureBlobStorageService.BlobClientOptionsForCompatibility.
        var serviceClient = new BlobServiceClient(
            AzuriteConnectionString,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05));
        var containerClient = serviceClient.GetBlobContainerClient(ContainerName);
        var accessPolicy = await containerClient.GetAccessPolicyAsync();

        Assert.Equal(PublicAccessType.None, accessPolicy.Value.BlobPublicAccess);
    }

    [Fact]
    public void GenerateReadSasUrl_produces_a_read_only_blob_scoped_sas_expiring_in_about_5_minutes()
    {
        var service = CreateService(AzuriteConnectionString, ContainerName);
        var blobName = $"{Guid.NewGuid()}.jpg";

        var url = service.GenerateReadSasUrl(blobName, TimeSpan.FromMinutes(5));

        var uri = new Uri(url);
        var query = QueryHelpers.ParseQuery(uri.Query);

        Assert.Contains(blobName, uri.AbsolutePath);
        // sp=r → read-only permission, never write/delete/list; a blob-scoped SAS (sr=b), never
        // account- or container-scoped.
        Assert.Equal("r", query["sp"].ToString());
        Assert.Equal("b", query["sr"].ToString());

        var expiry = DateTimeOffset.Parse(query["se"].ToString());
        var expectedExpiry = DateTimeOffset.UtcNow.AddMinutes(5);
        Assert.True(Math.Abs((expiry - expectedExpiry).TotalSeconds) < 30);
    }

    [Fact]
    public async Task UploadAsync_propagates_the_sdk_exception_instead_of_swallowing_it()
    {
        // Malformed connection string — exercises both cases documented in the spec's "Error
        // handling": an invalid connection string fails synchronously while parsing it, and an
        // unreachable Azurite/Storage account fails on the network call — either way the exception
        // must reach the caller, never be swallowed here (Block 4 translates it downstream).
        var service = CreateService("not-a-real-connection-string", ContainerName);
        await using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.UploadAsync(content, $"{Guid.NewGuid()}.jpg", "image/jpeg", CancellationToken.None));
    }
}
