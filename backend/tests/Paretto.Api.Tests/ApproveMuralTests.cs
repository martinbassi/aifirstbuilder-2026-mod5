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
/// Block 3 (Aprobar mural, admin) de FEAT-001c — POST /api/moderation/murals/{id}/approve.
///
/// Misma estrategia de testing que GetPendingMuralsTests: integración HTTP completa vía
/// WebApplicationFactory, con AppDbContext reemplazado por el proveedor InMemory de EF Core por
/// test (nombre de base único). IBlobStorageService se reemplaza por un fake de mano porque no es
/// necesario para este endpoint pero sigue siendo una dependencia registrada en el pipeline.
/// </summary>
public class ApproveMuralTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public ApproveMuralTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private WebApplicationFactory<Program> CreateFactory(string dbName, bool throwOnSaveChanges = false)
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

                services.RemoveAll<IBlobStorageService>();
                services.AddScoped<IBlobStorageService>(_ => new FakeBlobStorageService());
            });
        });
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for ApproveMural tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    /// <summary>
    /// Test double standing in for a DB-level failure at save time (same pattern as
    /// `CreateMuralTests`/`RegisterUserTests`'s own `ThrowingSaveChangesDbContext`), used to exercise
    /// `ApproveMuralCommandHandler`'s `catch (DbUpdateException)` -&gt; `ModerationPersistenceException`
    /// fallback. Only throws when the pending changes include a modified <see cref="Mural"/> — seeding
    /// the users (`SeedUserAsync`) and the mural (`SeedMuralAsync`), and logging in, must still succeed
    /// for real against the InMemory store.
    /// </summary>
    private sealed class ThrowingSaveChangesDbContext : AppDbContext
    {
        public ThrowingSaveChangesDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<Mural>().Any(e => e.State == EntityState.Modified))
            {
                throw new DbUpdateException("Simulated DB failure while approving the mural.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
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
        MuralStatus status)
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

    private static async Task<MuralStatus> ReadMuralStatusAsync(WebApplicationFactory<Program> factory, Guid muralId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mural = await db.Murals.SingleAsync(m => m.Id == muralId);
        return mural.Status;
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
    public async Task Administrator_approves_a_pending_mural_and_gets_200_with_published_status()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/moderation/murals/{muralId}/approve", null);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(muralId, body.GetProperty("id").GetGuid());
        Assert.Equal("Published", body.GetProperty("status").GetString());

        var statusInDb = await ReadMuralStatusAsync(factory, muralId);
        Assert.Equal(MuralStatus.Published, statusInDb);
    }

    [Fact]
    public async Task Standard_user_attempting_to_approve_gets_403_and_mural_stays_pending()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);
        var (_, username, password) = await SeedUserAsync(factory, UserRole.Standard);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/moderation/murals/{muralId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var statusInDb = await ReadMuralStatusAsync(factory, muralId);
        Assert.Equal(MuralStatus.Pending, statusInDb);
    }

    [Fact]
    public async Task Approving_a_nonexistent_mural_returns_404()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/moderation/murals/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(MuralStatus.Published)]
    [InlineData(MuralStatus.Rejected)]
    public async Task Approving_a_mural_that_is_already_published_or_rejected_returns_409(MuralStatus initialStatus)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, initialStatus);
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/moderation/murals/{muralId}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var statusInDb = await ReadMuralStatusAsync(factory, muralId);
        Assert.Equal(initialStatus, statusInDb);
    }

    [Fact]
    public async Task A_DbUpdateException_while_saving_returns_500_with_the_generic_message_and_mural_stays_pending()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), throwOnSaveChanges: true);
        var (ownerId, _, _) = await SeedUserAsync(factory);
        var muralId = await SeedMuralAsync(factory, ownerId, MuralStatus.Pending);
        var (_, adminUsername, adminPassword) = await SeedUserAsync(factory, UserRole.Administrator);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, adminUsername, adminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/moderation/murals/{muralId}/approve", null);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.InternalServerError, $"Expected 500, got {response.StatusCode}: {raw}");

        var title = JsonDocument.Parse(raw).RootElement.GetProperty("title").GetString();
        Assert.Equal(Paretto.Api.Features.Moderation.Commands.ModerationPersistenceException.GenericMessage, title);

        var statusInDb = await ReadMuralStatusAsync(factory, muralId);
        Assert.Equal(MuralStatus.Pending, statusInDb);
    }
}
