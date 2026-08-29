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
/// Block 2 (Listar murales pendientes, admin) de FEAT-001c — GET /api/moderation/murals/pending.
///
/// Misma estrategia de testing que GetMuralByIdTests: integración HTTP completa vía
/// WebApplicationFactory, con AppDbContext reemplazado por el proveedor InMemory de EF Core por
/// test (nombre de base único). IBlobStorageService se reemplaza por un fake de mano para no
/// depender de Azurite y poder aserir determinísticamente sobre la URL generada.
/// </summary>
public class GetPendingMuralsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public GetPendingMuralsTests(WebApplicationFactory<Program> baseFactory)
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
            throw new NotSupportedException("Not needed for GetPendingMurals tests.");

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

    private static async Task<Guid> SeedMuralAsync(
        WebApplicationFactory<Program> factory,
        Guid ownerId,
        MuralStatus status,
        DateTime createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mural = new Mural
        {
            UserId = ownerId,
            PhotoBlobName = $"{Guid.NewGuid()}.jpg",
            Location = Mural.CreateLocation(-34.6037, -58.3816),
            Status = status,
            CreatedAt = createdAt,
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
    public async Task Administrator_gets_200_with_pending_murals_ordered_by_created_at_ascending_and_photo_url()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var now = DateTime.UtcNow;
        var olderMuralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending, now.AddHours(-2));
        var newerMuralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending, now.AddHours(-1));
        // Rejected mural must never show up in this admin-only pending queue.
        await SeedMuralAsync(factory, ownerId, MuralStatus.Rejected, now.AddHours(-3));
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/moderation/murals/pending?page=1&pageSize=20");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        var murals = body.GetProperty("murals").EnumerateArray().ToList();
        Assert.Equal(2, murals.Count);
        Assert.Equal(olderMuralId, murals[0].GetProperty("id").GetGuid());
        Assert.Equal(newerMuralId, murals[1].GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(murals[0].GetProperty("photoUrl").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(murals[1].GetProperty("photoUrl").GetString()));
    }

    [Fact]
    public async Task Omitting_page_and_page_size_applies_defaults_and_returns_total_count()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var now = DateTime.UtcNow;
        await SeedMuralAsync(factory, ownerId, MuralStatus.Pending, now.AddHours(-1));
        await SeedMuralAsync(factory, ownerId, MuralStatus.Pending, now);
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/moderation/murals/pending");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Page_two_with_more_pending_murals_than_page_size_returns_the_remainder_without_overlap()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var now = DateTime.UtcNow;
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await SeedMuralAsync(factory, ownerId, MuralStatus.Pending, now.AddMinutes(i)));
        }
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var page1Response = await client.GetAsync("/api/moderation/murals/pending?page=1&pageSize=2");
        var page1Raw = await page1Response.Content.ReadAsStringAsync();
        Assert.True(page1Response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {page1Response.StatusCode}: {page1Raw}");
        var page1Murals = JsonDocument.Parse(page1Raw).RootElement.GetProperty("murals").EnumerateArray()
            .Select(m => m.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(new[] { ids[0], ids[1] }, page1Murals);

        var page2Response = await client.GetAsync("/api/moderation/murals/pending?page=2&pageSize=2");
        var page2Raw = await page2Response.Content.ReadAsStringAsync();
        Assert.True(page2Response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {page2Response.StatusCode}: {page2Raw}");
        var page2Murals = JsonDocument.Parse(page2Raw).RootElement.GetProperty("murals").EnumerateArray()
            .Select(m => m.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(new[] { ids[2] }, page2Murals);
    }

    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("page=1&pageSize=51")]
    public async Task Out_of_range_page_or_page_size_returns_422(string queryString)
    {
        // The spec's "Required tests" section calls this a 400, but the FluentValidation pipeline
        // already established by `ValidationBehavior`/`ExceptionHandlingMiddleware` (untouched by
        // this block) translates every validation failure to 422 UnprocessableEntity — same
        // behavior already documented on `MuralsController.Create`'s own
        // `[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]` for
        // `CreateMuralCommandValidator` failures. Asserting the actual, observed status here rather
        // than the spec's stated one — see the implementer report for this block.
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/moderation/murals/pending?{queryString}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Standard_user_gets_403()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory, UserRole.Standard);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/moderation/murals/pending");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_a_session_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/moderation/murals/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
