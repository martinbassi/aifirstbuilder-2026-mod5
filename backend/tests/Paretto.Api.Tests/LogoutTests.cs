using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
/// Block 7 (Logout) — POST /api/auth/logout.
///
/// Same testing strategy as LoginTests: full HTTP-pipeline integration tests via
/// WebApplicationFactory, with AppDbContext swapped for EF Core's InMemory provider per test.
///
/// Design decision (mine, spec leaves the exact test-endpoint shape open — "tu criterio"): this
/// file defines its own copy of the `[Authorize]`-probe `IStartupFilter` instead of reusing
/// LoginTests' private nested class (not accessible across files, and LoginTests belongs to the
/// already-closed Block 6 — not touched here). Minimal duplication of test scaffolding only, same
/// reasoning documented in LoginTests.cs for why a permanent diagnostic action was not added to
/// AuthController.
/// </summary>
public class LogoutTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public LogoutTests(WebApplicationFactory<Program> baseFactory)
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

                services.AddTransient<IStartupFilter, AuthorizedProbeEndpointStartupFilter>();
            });
        });
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

    private static async Task<string> LoginAsync(WebApplicationFactory<Program> factory, HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var loginRaw = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.StatusCode == HttpStatusCode.OK, $"Login prerequisite failed: {loginResponse.StatusCode}: {loginRaw}");
        var token = JsonDocument.Parse(loginRaw).RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    [Fact]
    public async Task Logout_deletes_the_corresponding_Sessions_row()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(factory, client, username, password);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.Sessions.CountAsync());
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.Sessions.CountAsync());
        }
    }

    [Fact]
    public async Task After_logout_a_subsequent_request_with_the_same_token_is_rejected_with_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(factory, client, username, password);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var probeBeforeLogout = await client.GetAsync("/test/authorized-probe");
        Assert.Equal(HttpStatusCode.OK, probeBeforeLogout.StatusCode);

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var probeAfterLogout = await client.GetAsync("/test/authorized-probe");
        Assert.Equal(HttpStatusCode.Unauthorized, probeAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_valid_token_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, logoutResponse.StatusCode);
    }

    private sealed class AuthorizedProbeEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);

            app.Map("/test/authorized-probe", branch =>
            {
                branch.Run(async context =>
                {
                    var authResult = await context.AuthenticateAsync();
                    context.Response.StatusCode = authResult.Succeeded
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status401Unauthorized;
                });
            });
        };
    }
}
