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
/// Block 6 (Login + esquema de autenticación por sesión) — POST /api/auth/login and
/// SessionAuthenticationHandler.
///
/// Same testing strategy as RegisterUserTests: full HTTP-pipeline integration tests via
/// WebApplicationFactory, with AppDbContext swapped for EF Core's InMemory provider per test.
///
/// Design decision (mine, spec leaves the exact shape of the "[Authorize] test endpoint" open —
/// "usá tu criterio, documentalo"): instead of adding a permanent diagnostic action to
/// AuthController (which would be a change outside Block 6's documented API contract — the only
/// modification to AuthController the spec lists is the `Login` action), a test-only endpoint is
/// injected via IStartupFilter, same pattern ExceptionHandlingMiddlewareTests (Block 1) already
/// uses. The probe endpoint calls `HttpContext.AuthenticateAsync()` (no explicit scheme — resolves
/// to the default scheme configured by `AddAuthentication(SessionAuthenticationHandler.SchemeName)`
/// in Program.cs) and checks `AuthenticateResult.Succeeded`, returning 200/401 accordingly. This
/// exercises the exact same authentication path a real `[Authorize]`-decorated action would run
/// (SessionAuthenticationHandler.HandleAuthenticateAsync), for the default policy `[Authorize]`
/// applies (only requires an authenticated principal, no extra requirements) — without depending on
/// ASP.NET Core's endpoint-routing-inside-a-branch machinery, which `app.Map(...)` sub-pipelines do
/// not wire up automatically.
/// </summary>
public class LoginTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public LoginTests(WebApplicationFactory<Program> baseFactory)
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

    [Fact]
    public async Task Login_with_valid_credentials_returns_a_token_and_expiresAt_about_7_days_out()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();

        var before = DateTime.UtcNow;
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        var token = body.GetProperty("token").GetString();
        var expiresAt = body.GetProperty("expiresAt").GetDateTime();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.InRange(expiresAt, before.AddDays(7).AddMinutes(-1), before.AddDays(7).AddMinutes(1));
    }

    // FIX-003: el cambio de AddJsonOptions en Program.cs (JsonDateTimeUtcConverter movido al
    // JsonOptions correcto) es global — afecta cualquier controller MVC, no solo Murals. Gap
    // detectado por el impact scan de PLAN: sin este test, LoginCommand.ExpiresAt quedaba sin
    // cobertura del nuevo formato de fecha.
    [Fact]
    public async Task ExpiresAt_is_serialized_with_the_full_utc_format()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var expiresAt = JsonDocument.Parse(raw).RootElement.GetProperty("expiresAt").GetString();
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", expiresAt);
    }

    [Fact]
    public async Task Login_with_a_Standard_user_returns_role_Standard_in_the_response()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory, UserRole.Standard);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var role = JsonDocument.Parse(raw).RootElement.GetProperty("role").GetString();
        Assert.Equal(nameof(UserRole.Standard), role);
    }

    [Fact]
    public async Task Login_with_an_Administrator_user_returns_role_Administrator_in_the_response()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory, UserRole.Administrator);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var role = JsonDocument.Parse(raw).RootElement.GetProperty("role").GetString();
        Assert.Equal(nameof(UserRole.Administrator), role);
    }

    [Fact]
    public async Task Nonexistent_user_and_wrong_password_return_the_exact_same_error()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, _) = await SeedUserAsync(factory);
        var client = factory.CreateClient();

        var nonexistentResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = $"ghost-{Guid.NewGuid():N}",
            password = "WhateverPassword1"
        });

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password = "TotallyWrong1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, nonexistentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);

        var nonexistentMessage = JsonDocument.Parse(await nonexistentResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("title").GetString();
        var wrongPasswordMessage = JsonDocument.Parse(await wrongPasswordResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("title").GetString();

        Assert.NotNull(nonexistentMessage);
        Assert.Equal(nonexistentMessage, wrongPasswordMessage);
    }

    [Fact]
    public async Task Authorized_probe_endpoint_accepts_a_valid_unexpired_token()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (_, username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var loginRaw = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.StatusCode == HttpStatusCode.OK, $"Login prerequisite failed: {loginResponse.StatusCode}: {loginRaw}");
        var token = JsonDocument.Parse(loginRaw).RootElement.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var probeResponse = await client.GetAsync("/test/authorized-probe");

        Assert.Equal(HttpStatusCode.OK, probeResponse.StatusCode);
    }

    [Fact]
    public async Task Authorized_probe_endpoint_rejects_a_token_that_does_not_exist_in_Sessions()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "nonexistent-token-value");
        var probeResponse = await client.GetAsync("/test/authorized-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, probeResponse.StatusCode);
    }

    [Fact]
    public async Task Authorized_probe_endpoint_rejects_a_token_whose_session_has_expired()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var (userId, _, _) = await SeedUserAsync(factory);
        var client = factory.CreateClient();

        string? rawToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokenGenerator = scope.ServiceProvider.GetRequiredService<ISessionTokenGenerator>();
            var generated = tokenGenerator.Generate();
            rawToken = generated.RawToken;

            db.Sessions.Add(new Session
            {
                TokenHash = generated.TokenHash,
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rawToken);
        var probeResponse = await client.GetAsync("/test/authorized-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, probeResponse.StatusCode);
    }

    [Fact]
    public async Task Authorized_probe_endpoint_rejects_a_request_without_an_Authorization_header()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var probeResponse = await client.GetAsync("/test/authorized-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, probeResponse.StatusCode);
    }

    private sealed class AuthorizedProbeEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Register the real Program.cs pipeline first (so authentication/authorization
            // middleware is already in place), then append a test-only probe downstream of it.
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
