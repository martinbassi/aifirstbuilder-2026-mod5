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

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (FEAT-007) — GET /api/auth/session.
///
/// Same testing strategy as LoginTests/LogoutTests: full HTTP-pipeline integration tests via
/// WebApplicationFactory, with AppDbContext swapped for EF Core's InMemory provider per test. A
/// real token is obtained via the actual /api/auth/login endpoint (no test-only probe needed here,
/// since the endpoint under test IS the [Authorize]-decorated action itself).
/// </summary>
public class GetCurrentSessionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public GetCurrentSessionTests(WebApplicationFactory<Program> baseFactory)
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
            });
        });
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
    public async Task Valid_session_for_an_Administrator_returns_200_with_username_and_role()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory, UserRole.Administrator);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/session");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal(nameof(UserRole.Administrator), body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Valid_session_for_a_Standard_user_returns_200_with_username_and_role()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory, UserRole.Standard);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/session");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(username, body.GetProperty("username").GetString());
        Assert.Equal(nameof(UserRole.Standard), body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Request_without_an_Authorization_header_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_that_does_not_correspond_to_any_session_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "nonexistent-token-value");
        var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
