using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Api.Features.Auth.Commands;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 5 (Registro) — POST /api/auth/register.
///
/// Testing decision (mine, not spec-mandated — the spec does not prescribe an execution strategy
/// for this block): these are full HTTP-pipeline integration tests via WebApplicationFactory
/// (Controller -> validation -> MediatR -> Handler -> Mapster -> AppDbContext), exactly like Block
/// 3/4's approach, but with AppDbContext's SQL Server provider swapped for EF Core's InMemory
/// provider per test (unique database name), instead of a real SQL Server instance.
///
/// Why not follow Block 3's real-SQL-Server pattern literally: this sandbox has no reachable SQL
/// Server instance and no Docker available to stand one up (verified: `sqlcmd` fails with no
/// credentials, `docker` is not installed). Unlike Block 3 — which specifically had to prove a
/// *database-level* unique constraint — Block 5's uniqueness check happens in application code
/// (an `AnyAsync` lookup before insert, see RegisterUserCommandHandler), so InMemory exercises the
/// real logic under test without hitting the problem Block 3 had (InMemory does not enforce unique
/// indexes, but this Handler doesn't rely on that enforcement for its primary duplicate-detection
/// path).
///
/// New test-only dependency: Microsoft.EntityFrameworkCore.InMemory, added to
/// Paretto.Api.Tests.csproj. Justification: first-party EF Core package, test-project-only,
/// enables exercising the real HTTP pipeline without a live database dependency in this sandbox.
///
/// Round 2 addition — the 7th test (DbUpdateException/race-condition fallback, spec's 7th bullet
/// under Block 5's "Required tests"), previously flagged as a known gap: reliably forcing the exact
/// race between two concurrent requests deterministically, in a single-threaded test, against
/// EF Core's InMemory provider is fragile (interleaving two DbContext instances against the same
/// named store to hit the unique-index check at exactly the right moment is not something you can
/// assert on repeatably). Instead this uses the fake/test-double alternative the block explicitly
/// allows: `ThrowingSaveChangesDbContext` (below), a subclass of `AppDbContext` that overrides
/// `SaveChangesAsync` to throw `DbUpdateException`, standing in for the real unique-constraint
/// violation a DB-level race would produce. Everything else in the request stays real — the
/// Handler's own `AnyAsync` duplicate check still runs (and returns false, since the fake store is
/// empty), so the only thing being substituted is the trigger of `DbUpdateException` at save time,
/// which is exactly the fallback path under test (`RegisterUserCommandHandler`'s
/// `catch (DbUpdateException)` block).
/// </summary>
public class RegisterUserTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public RegisterUserTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private WebApplicationFactory<Program> CreateFactory(string dbName)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // AddDbContext also registers internal EF Core configuration services (beyond
                // DbContextOptions<AppDbContext> itself); removing only that one leaves the
                // SqlServer provider's configuration registered alongside InMemory's, and EF Core
                // refuses to start with two providers registered for the same context. Remove every
                // service descriptor generically closed over AppDbContext before re-adding it with
                // the InMemory provider.
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

    /// <summary>
    /// Same DI-swap as <see cref="CreateFactory"/>, but resolves <see cref="AppDbContext"/> to
    /// <see cref="ThrowingSaveChangesDbContext"/> instead of a plain InMemory-backed instance — see
    /// the class summary above for why.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryWithSaveChangesFailure(string dbName)
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

                services.AddScoped<AppDbContext>(_ =>
                {
                    var options = new DbContextOptionsBuilder<AppDbContext>()
                        .UseInMemoryDatabase(dbName)
                        .Options;
                    return new ThrowingSaveChangesDbContext(options);
                });
            });
        });
    }

    /// <summary>
    /// Test double standing in for the DB-level unique-constraint violation a race between two
    /// concurrent `/register` requests with the same Username/Email would produce (see the class
    /// summary above). Everything besides `SaveChangesAsync` behaves like a real
    /// InMemory-backed <see cref="AppDbContext"/> — in particular, `AnyAsync` still runs for real
    /// against a real (empty) store.
    /// </summary>
    private sealed class ThrowingSaveChangesDbContext : AppDbContext
    {
        public ThrowingSaveChangesDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("Simulated unique constraint violation (concurrent request race).");
        }
    }

    [Fact]
    public async Task Register_with_valid_data_creates_the_account_with_standard_role()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"dilux-{suffix}",
            password = "Sup3rSecret!",
            email = $"dilux-{suffix}@example.com"
        });

        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Expected 201, got {response.StatusCode}: {raw}");

        var body = JsonDocument.Parse(raw).RootElement;
        Assert.True(body.TryGetProperty("id", out var idProperty));
        Assert.NotEqual(Guid.Empty, idProperty.GetGuid());
        Assert.Equal($"dilux-{suffix}", body.GetProperty("username").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Username == $"dilux-{suffix}");
        Assert.Equal(UserRole.Standard, user.Role);
        Assert.NotEqual("Sup3rSecret!", user.PasswordHash);
    }

    [Fact]
    public async Task Duplicate_email_and_duplicate_username_return_the_exact_same_error_message()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var sharedUsername = $"dilux-{suffix}";
        var sharedEmail = $"dilux-{suffix}@example.com";

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = sharedUsername,
            password = "Sup3rSecret!",
            email = sharedEmail
        });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateUsernameResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = sharedUsername,
            password = "Sup3rSecret!",
            email = $"other-{suffix}@example.com"
        });

        var duplicateEmailResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"other-{suffix}",
            password = "Sup3rSecret!",
            email = sharedEmail
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateUsernameResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateEmailResponse.StatusCode);

        var usernameMessage = JsonDocument.Parse(await duplicateUsernameResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("title").GetString();
        var emailMessage = JsonDocument.Parse(await duplicateEmailResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("title").GetString();

        Assert.NotNull(usernameMessage);
        Assert.Equal(usernameMessage, emailMessage);
    }

    [Fact]
    public async Task Register_with_a_password_shorter_than_8_characters_is_rejected()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"dilux-{suffix}",
            password = "Ab1",
            email = $"dilux-{suffix}@example.com"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_a_password_longer_than_128_characters_is_rejected()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var tooLongPassword = "Ab1" + new string('a', 129);

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"dilux-{suffix}",
            password = tooLongPassword,
            email = $"dilux-{suffix}@example.com"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("onlylettersnodigits")]
    [InlineData("12345678901234")]
    public async Task Register_with_a_password_missing_letters_or_missing_numbers_is_rejected(string invalidPassword)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"dilux-{suffix}",
            password = invalidPassword,
            email = $"dilux-{suffix}@example.com"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_role_field_in_the_request_json_does_not_grant_that_role()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var username = $"dilux-{suffix}";

        var rawJson = $$"""
        {
            "username": "{{username}}",
            "password": "Sup3rSecret!",
            "email": "dilux-{{suffix}}@example.com",
            "role": "Administrator"
        }
        """;

        var response = await client.PostAsync(
            "/api/auth/register",
            new StringContent(rawJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Username == username);
        Assert.Equal(UserRole.Standard, user.Role);
    }

    [Fact]
    public async Task A_unique_constraint_violation_at_save_time_is_translated_to_the_same_generic_duplicate_error()
    {
        var factory = CreateFactoryWithSaveChangesFailure(Guid.NewGuid().ToString());
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"racer-{suffix}",
            password = "Sup3rSecret!",
            email = $"racer-{suffix}@example.com"
        });

        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Expected 400, got {response.StatusCode}: {raw}");

        var title = JsonDocument.Parse(raw).RootElement.GetProperty("title").GetString();
        Assert.Equal(DuplicateAccountException.GenericMessage, title);
    }
}
