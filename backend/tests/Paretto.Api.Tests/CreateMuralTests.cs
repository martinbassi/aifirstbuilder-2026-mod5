using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Moderation;
using Paretto.Infrastructure.Security;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 4 (API: crear mural) de FEAT-001b — POST /api/murals.
///
/// Misma estrategia de testing que RegisterUserTests/LoginTests/LogoutTests: integración HTTP
/// completa vía WebApplicationFactory, con AppDbContext reemplazado por el proveedor InMemory de EF
/// Core por test (nombre de base único). IBlobStorageService e INsfwContentScanner se reemplazan por
/// fakes de mano (mismo patrón sin mocking framework que NsfwSpyContentScannerTests) para poder
/// dictar determinísticamente el resultado del scan NSFW y simular fallas de Storage sin depender de
/// Azurite.
/// </summary>
public class CreateMuralTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public CreateMuralTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private WebApplicationFactory<Program> CreateFactory(
        string dbName,
        INsfwContentScanner? nsfwContentScanner = null,
        IBlobStorageService? blobStorageService = null,
        bool throwOnSaveChanges = false)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var appDbContextDescriptors = services
                    .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext)))
                    .ToList();
                foreach (var descriptor in appDbContextDescriptors)
                {
                    services.Remove(descriptor);
                }
                services.RemoveAll<AppDbContext>();

                if (throwOnSaveChanges)
                {
                    services.AddScoped<AppDbContext>(_ =>
                    {
                        var options = new DbContextOptionsBuilder<AppDbContext>()
                            .UseInMemoryDatabase(dbName)
                            .Options;
                        return new ThrowingSaveChangesDbContext(options);
                    });
                }
                else
                {
                    services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
                }

                if (nsfwContentScanner is not null)
                {
                    services.RemoveAll<INsfwContentScanner>();
                    services.AddScoped(_ => nsfwContentScanner);
                }

                if (blobStorageService is not null)
                {
                    services.RemoveAll<IBlobStorageService>();
                    services.AddScoped(_ => blobStorageService);
                }
            });
        });
    }

    /// <summary>
    /// Only throws when the pending changes include a new <see cref="Mural"/> — seeding the user
    /// (`SeedUserAsync`) and logging in (`LoginCommandHandler` persisting the `Session`) must still
    /// succeed for real against the InMemory store, or those prerequisites would fail before the
    /// actual request under test ever runs.
    /// </summary>
    private sealed class ThrowingSaveChangesDbContext : AppDbContext
    {
        public ThrowingSaveChangesDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<Mural>().Any(e => e.State == EntityState.Added))
            {
                throw new DbUpdateException("Simulated DB failure while saving the mural.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FakeNsfwContentScanner : INsfwContentScanner
    {
        private readonly NsfwScanResult _result;

        public FakeNsfwContentScanner(NsfwScanResult result)
        {
            _result = result;
        }

        public Task<NsfwScanResult> ScanAsync(Stream imageContent, CancellationToken ct) => Task.FromResult(_result);
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public List<string> UploadedBlobNames { get; } = [];

        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct)
        {
            UploadedBlobNames.Add(blobName);
            return Task.FromResult(blobName);
        }

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}";
    }

    private sealed class ThrowingBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct) =>
            throw new InvalidOperationException("Simulated Storage failure.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => throw new NotSupportedException();
    }

    private static async Task<(Guid UserId, string Username, string Password)> SeedUserAsync(WebApplicationFactory<Program> factory)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var username = $"dilux-{suffix}";
        const string password = "Sup3rSecret!";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Standard,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, username, password);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var loginRaw = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.StatusCode == HttpStatusCode.OK, $"Login prerequisite failed: {loginResponse.StatusCode}: {loginRaw}");
        var token = JsonDocument.Parse(loginRaw).RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    // JPEG magic bytes: FF D8 FF, padded to a plausible small body.
    private static byte[] ValidJpegBytes() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x02, 0x03];

    private static byte[] OversizedJpegBytes()
    {
        var bytes = new byte[10 * 1024 * 1024 + 1];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static byte[] NotAnImageBytes() => "this is definitely not an image file"u8.ToArray();

    private static MultipartFormDataContent BuildMultipartContent(
        byte[] photoBytes,
        string contentType,
        double latitude,
        double longitude,
        string fileName = "mural.jpg")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(photoBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "Photo", fileName);
        content.Add(new StringContent(latitude.ToString(CultureInfo.InvariantCulture)), "Latitude");
        content.Add(new StringContent(longitude.ToString(CultureInfo.InvariantCulture)), "Longitude");
        return content;
    }

    [Fact]
    public async Task Valid_photo_and_coordinates_with_a_clean_scan_return_201_and_persist_the_mural_as_pending()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", -34.6037, -58.3816);
        var response = await client.PostAsync("/api/murals", content);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(nameof(MuralStatus.Pending), body.GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mural = db.Murals.Single(m => m.Id == id);
        Assert.Equal(MuralStatus.Pending, mural.Status);
    }

    [Fact]
    public async Task Photo_larger_than_10MB_is_rejected_with_422()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(OversizedJpegBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Non_image_file_renamed_as_jpg_is_rejected_with_422_because_the_byte_signature_is_invalid()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(NotAnImageBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_a_session_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var client = factory.CreateClient();

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Nsfw_scan_result_persists_the_mural_as_rejected_with_201()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Nsfw));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(nameof(MuralStatus.Rejected), body.GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mural = db.Murals.Single(m => m.Id == id);
        Assert.Equal(MuralStatus.Rejected, mural.Status);
    }

    [Fact]
    public async Task Inconclusive_scan_result_persists_the_mural_as_pending_with_201()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Inconclusive));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        var id = body.GetProperty("id").GetGuid();
        Assert.Equal(nameof(MuralStatus.Pending), body.GetProperty("status").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mural = db.Murals.Single(m => m.Id == id);
        Assert.Equal(MuralStatus.Pending, mural.Status);
    }

    [Fact]
    public async Task A_DbUpdateException_while_saving_returns_500_and_persists_no_mural()
    {
        var factory = CreateFactory(
            Guid.NewGuid().ToString(),
            nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean),
            throwOnSaveChanges: true);
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Murals.CountAsync());
    }

    [Fact]
    public async Task A_failing_blob_upload_returns_500_and_persists_no_mural()
    {
        var factory = CreateFactory(
            Guid.NewGuid().ToString(),
            nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean),
            blobStorageService: new ThrowingBlobStorageService());
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Murals.CountAsync());
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task Coordinates_outside_the_valid_range_are_rejected_with_422(double latitude, double longitude)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", latitude, longitude);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
