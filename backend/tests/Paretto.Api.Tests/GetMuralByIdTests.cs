using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Security;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 5 (API: consultar un mural) de FEAT-001b — GET /api/murals/{id}.
///
/// Misma estrategia de testing que CreateMuralTests: integración HTTP completa vía
/// WebApplicationFactory, con AppDbContext reemplazado por el proveedor InMemory de EF Core por
/// test (nombre de base único). IBlobStorageService se reemplaza por un fake de mano para no
/// depender de Azurite y poder aserir determinísticamente sobre la URL generada.
/// </summary>
public class GetMuralByIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public GetMuralByIdTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private WebApplicationFactory<Program> CreateFactory(string dbName)
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
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));

                services.RemoveAll<IBlobStorageService>();
                services.AddScoped<IBlobStorageService>(_ => new FakeBlobStorageService());
            });
        });
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for GetMuralById tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    private static async Task<(Guid UserId, string Username, string Password)> SeedUserAsync(
        WebApplicationFactory<Program> factory,
        UserRole role = UserRole.Standard)
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
            Role = role,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (user.Id, username, password);
    }

    private static async Task<Guid> SeedMuralAsync(WebApplicationFactory<Program> factory, Guid ownerId, MuralStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mural = new Mural
        {
            UserId = ownerId,
            PhotoBlobName = $"{Guid.NewGuid()}.jpg",
            Latitude = -34.6037,
            Longitude = -58.3816,
            Status = status,
        };
        db.Murals.Add(mural);
        await db.SaveChangesAsync();

        return mural.Id;
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

    [Fact]
    public async Task Owner_querying_their_own_pending_mural_returns_200_with_photo_url()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, username, password) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/murals/{muralId}");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(muralId, body.GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("photoUrl").GetString()));
    }

    [Fact]
    public async Task Administrator_querying_another_users_pending_mural_returns_200()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/murals/{muralId}");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
    }

    [Fact]
    public async Task A_third_authenticated_user_querying_a_pending_mural_they_do_not_own_returns_404()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);
        var (_, otherUsername, otherPassword) = await SeedUserAsync(factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, otherUsername, otherPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/murals/{muralId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_third_authenticated_user_querying_a_rejected_mural_they_do_not_own_returns_404()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Rejected);
        var (_, otherUsername, otherPassword) = await SeedUserAsync(factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, otherUsername, otherPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/murals/{muralId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Nonexistent_id_returns_404_with_the_same_generic_message_as_the_denied_case()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);
        var (_, otherUsername, otherPassword) = await SeedUserAsync(factory);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, otherUsername, otherPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var deniedResponse = await client.GetAsync($"/api/murals/{muralId}");
        var deniedRaw = await deniedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, deniedResponse.StatusCode);

        var nonexistentResponse = await client.GetAsync($"/api/murals/{Guid.NewGuid()}");
        var nonexistentRaw = await nonexistentResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);

        var deniedTitle = JsonDocument.Parse(deniedRaw).RootElement.GetProperty("title").GetString();
        var nonexistentTitle = JsonDocument.Parse(nonexistentRaw).RootElement.GetProperty("title").GetString();
        Assert.Equal(deniedTitle, nonexistentTitle);
    }

    [Fact]
    public async Task Request_without_a_session_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);

        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/murals/{muralId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // FIX-003: JsonDateTimeUtcConverter estaba registrado en el JsonOptions equivocado
    // (ConfigureHttpJsonOptions en vez de AddControllers().AddJsonOptions) y nunca aplicaba a las
    // respuestas de MuralsController — ver docs/daw/specs/rca-FIX-003.md, causa raíz #2.
    [Fact]
    public async Task CreatedAt_is_serialized_with_the_full_utc_format()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, username, password) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/murals/{muralId}");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var createdAt = JsonDocument.Parse(raw).RootElement.GetProperty("createdAt").GetString();
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", createdAt);
    }
}
