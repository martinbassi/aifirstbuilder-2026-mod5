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

    // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A, padded to a plausible small body.
    private static byte[] ValidPngBytes() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    // WebP: "RIFF" + 4-byte size (arbitrary for this test) + "WEBP", per `CreateMuralCommandValidator.IsWebP`.
    private static byte[] ValidWebPBytes() =>
        [.. "RIFF"u8.ToArray(), 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8.ToArray(), 0x00, 0x01, 0x02, 0x03];

    private static byte[] OversizedJpegBytes()
    {
        var bytes = new byte[10 * 1024 * 1024 + 1];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    // Exceeds the 11 MB `[RequestFormLimits]` set on `MuralsController.Create` (threat model R2) —
    // distinct from `OversizedJpegBytes`, which only exceeds FluentValidation's 10 MB photo-size
    // rule and is meant to prove the request-level cap is what rejects it, not the validator.
    private static byte[] RequestFormLimitExceedingBytes()
    {
        var bytes = new byte[11 * 1024 * 1024 + 1024 * 1024];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static byte[] NotAnImageBytes() => "this is definitely not an image file"u8.ToArray();

    // FIX-003: el commit 9cecf21 agregó Title como campo obligatorio en CreateMuralCommand
    // (NotEmpty + MaximumLength(50)) pero este helper nunca lo enviaba, rompiendo los tests que
    // esperan 201. Default válido para no forzar a cada call site a pasarlo explícitamente; los
    // dos tests de borde de abajo sí lo pasan para ejercitar el rechazo.
    private static MultipartFormDataContent BuildMultipartContent(
        byte[] photoBytes,
        string contentType,
        double latitude,
        double longitude,
        string fileName = "mural.jpg",
        string title = "Mural de prueba")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(photoBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "Photo", fileName);
        content.Add(new StringContent(latitude.ToString(CultureInfo.InvariantCulture)), "Latitude");
        content.Add(new StringContent(longitude.ToString(CultureInfo.InvariantCulture)), "Longitude");
        content.Add(new StringContent(title), "Title");
        return content;
    }

    /// <summary>
    /// Covers the three formats the validator/handler actually support (`CreateMuralCommandValidator.IsJpeg/IsPng/IsWebP`,
    /// `CreateMuralCommandHandler.ExtensionFor`) — before this Theory the suite only ever exercised
    /// JPEG, leaving `IsPng`/`IsWebP`'s `true` branch and the `image/png`/`image/webp` arms of
    /// `ExtensionFor`'s switch uncovered (F-VER-03).
    /// </summary>
    [Theory]
    [InlineData("image/jpeg", "mural.jpg", ".jpg")]
    [InlineData("image/png", "mural.png", ".png")]
    [InlineData("image/webp", "mural.webp", ".webp")]
    public async Task Valid_photo_and_coordinates_with_a_clean_scan_return_201_and_persist_the_mural_as_pending(
        string contentType, string fileName, string expectedExtension)
    {
        var photoBytes = contentType switch
        {
            "image/jpeg" => ValidJpegBytes(),
            "image/png" => ValidPngBytes(),
            "image/webp" => ValidWebPBytes(),
            _ => throw new ArgumentOutOfRangeException(nameof(contentType)),
        };

        var blobStorageService = new FakeBlobStorageService();
        var factory = CreateFactory(
            Guid.NewGuid().ToString(),
            nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean),
            blobStorageService: blobStorageService);
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(photoBytes, contentType, -34.6037, -58.3816, fileName);
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

        // Also pins `ExtensionFor` picking the extension that matches the uploaded Content-Type.
        var uploadedBlobName = Assert.Single(blobStorageService.UploadedBlobNames);
        Assert.EndsWith(expectedExtension, uploadedBlobName);
    }

    /// <summary>
    /// Threat model R2 (DoS por upload sin límite): `[RequestFormLimits(MultipartBodyLengthLimit = ...)]`
    /// on `MuralsController.Create` must reject an oversized request while the multipart body is
    /// being parsed, BEFORE it reaches FluentValidation — distinct from
    /// `Photo_larger_than_10MB_is_rejected_with_422`, which proves the validator's own 10 MB
    /// photo-only rule. The framework-level cap (~11 MB, over the whole multipart body) surfaces as
    /// `400 Bad Request` (`[ApiController]`'s automatic `ModelState` validation on "Failed to read
    /// the request form"), confirmed against this implementation's actual response rather than
    /// assumed — see `MuralsController.Create`'s XML doc for why `[RequestSizeLimit]` was rejected
    /// (a no-op under `TestServer`, so an equivalent test for it would silently test nothing).
    /// </summary>
    [Fact]
    public async Task Request_body_larger_than_the_request_size_limit_is_rejected_with_400_before_reaching_validation()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(RequestFormLimitExceedingBytes(), "image/jpeg", 0, 0);
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    /// <summary>
    /// F-VER-03: covers `CreateMuralCommandHandler.ExtensionFor`'s `_ => string.Empty` default arm.
    /// Byte-signature validation (`HasValidImageSignatureAsync`) never looks at the declared
    /// `Content-Type`, only at the file's magic numbers, so a valid JPEG body sent with an
    /// unrecognized `Content-Type` still passes validation (201) — but `ExtensionFor` then has no
    /// arm matching that `Content-Type` and falls through to its default, so the uploaded blob name
    /// ends up with no extension at all.
    /// </summary>
    [Fact]
    public async Task Valid_photo_with_an_unrecognized_content_type_is_still_accepted_and_the_uploaded_blob_has_no_extension()
    {
        var blobStorageService = new FakeBlobStorageService();
        var factory = CreateFactory(
            Guid.NewGuid().ToString(),
            nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean),
            blobStorageService: blobStorageService);
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "application/octet-stream", 0, 0);
        var response = await client.PostAsync("/api/murals", content);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {response.StatusCode}: {raw}");

        var uploadedBlobName = Assert.Single(blobStorageService.UploadedBlobNames);
        Assert.DoesNotContain('.', uploadedBlobName);
    }

    /// <summary>
    /// F-VER-03: covers the "header shorter than the signature" length-check branch in
    /// `CreateMuralCommandValidator.IsJpeg/IsPng/IsWebP` — every other test's photo is at least 12
    /// bytes long, so `totalRead >= Signature.Length` was always true. A file smaller than any of
    /// the three signatures makes `HasValidImageSignatureAsync` read fewer bytes than the header
    /// buffer (the source stream runs out first), exercising the `length >= Signature.Length` guard
    /// with a `false` outcome.
    /// </summary>
    [Fact]
    public async Task Photo_shorter_than_any_image_signature_is_rejected_with_422()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent([0xFF, 0xD8], "image/jpeg", 0, 0);
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

    // FIX-003: cubre FR-17/AC-15 de prd-FEAT-001b.md (Title obligatorio, agregado por el commit
    // 9cecf21 sin cobertura de test). Rechazo con 400, no 422: Title es un `string` no-anulable
    // (nullable reference types habilitado) y [ApiController] le aplica un [Required] implícito
    // durante el model binding — ModelState queda inválido y el 400 automático ocurre ANTES de que
    // el request llegue al Handler/FluentValidation (CreateMuralCommandValidator.NotEmpty() nunca
    // se ejecuta para este caso; sí lo hace en el caso de longitud del test siguiente, porque un
    // string de 51 caracteres no está vacío y pasa el [Required] implícito).
    [Fact]
    public async Task Missing_title_is_rejected_with_400()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0, title: "");
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // FIX-003: cubre FR-17/AC-15 de prd-FEAT-001b.md (límite de 50 caracteres).
    [Fact]
    public async Task Title_longer_than_50_characters_is_rejected_with_422()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), nsfwContentScanner: new FakeNsfwContentScanner(NsfwScanResult.Clean));
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = BuildMultipartContent(ValidJpegBytes(), "image/jpeg", 0, 0, title: new string('a', 51));
        var response = await client.PostAsync("/api/murals", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
