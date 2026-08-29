using Microsoft.EntityFrameworkCore;
using Paretto.Domain.Entities;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 3 (Dominio y persistencia de Auth).
///
/// Runs against a real SQL Server 2025 instance, as the spec requires literally ("La migración
/// inicial aplica sin error contra una instancia de SQL Server 2025... Insertar dos User con el
/// mismo Username... falla por la constraint única de DB"). A unique-index violation cannot be
/// simulated with an in-memory/mocked provider — it has to be enforced by the real storage engine.
///
/// The connection string is read from the `ConnectionStrings__DefaultConnection` environment
/// variable rather than from an appsettings/test-config file. That is a test/CI isolation decision
/// (keep credentials out of anything tracked in git), not something the spec authorizes or
/// requires — the spec only specifies what the tests must prove, not how they obtain a connection.
/// If the variable is not set, the tests fail loudly (they do not skip and do not fall back to a
/// different engine).
///
/// Data isolation (testing.instructions.md, Rule #0 — never operate on real/shared data): every
/// test creates its own `User`/`Session` rows with a GUID-suffixed `Username`/`Email`/`TokenHash`
/// so concurrent or repeated runs never collide, and deletes exactly what it created in a `finally`
/// block so no orphan rows are left behind in the shared database.
/// </summary>
public class AuthPersistenceTests
{
    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection to run persistence tests against a real SQL Server instance.");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            // FEAT-009: AppDbContext ahora mapea Mural.Location (Point de NetTopologySuite) —
            // sin .UseNetTopologySuite() el proveedor de SqlServer no puede construir el modelo,
            // aunque este archivo no toque nada de Mural (mismo síntoma que Program.cs/
            // AppDbContextFactory.cs, cualquier AppDbContext construido a mano lo necesita).
            .UseSqlServer(GetConnectionString(), sqlServerOptions => sqlServerOptions.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void InitialCreate_migration_applies_without_error_and_creates_usable_tables()
    {
        using var context = CreateContext();

        var exception = Record.Exception(() => context.Database.Migrate());
        Assert.Null(exception);

        // Canary: the tables must actually be usable (not just "no exception"), otherwise a
        // no-op Migrate() (e.g. zero migrations registered) would pass this test vacuously.
        // Uses a throwaway, uniquely-named lookup so it never depends on the table being empty
        // (Rule #0 — this is a real, shared database).
        var probeUsername = $"probe-{Guid.NewGuid():N}";
        Assert.Null(context.Users.SingleOrDefault(u => u.Username == probeUsername));
        var probeTokenHash = Guid.NewGuid().ToString("N");
        Assert.Null(context.Sessions.SingleOrDefault(s => s.TokenHash == probeTokenHash));
    }

    [Theory]
    [InlineData("duplicate-field:username")]
    [InlineData("duplicate-field:email")]
    public void Inserting_a_second_user_with_a_duplicate_username_or_email_fails_on_the_unique_db_constraint(string scenario)
    {
        using var context = CreateContext();
        context.Database.Migrate();

        var suffix = Guid.NewGuid().ToString("N");
        var sharedUsername = $"dilux-{suffix}";
        var sharedEmail = $"dilux-{suffix}@example.com";

        var firstUser = new User
        {
            Username = sharedUsername,
            Email = sharedEmail,
            PasswordHash = "hash-1"
        };
        context.Users.Add(firstUser);
        context.SaveChanges();

        try
        {
            var duplicateUsername = scenario == "duplicate-field:username";
            var secondUser = new User
            {
                Username = duplicateUsername ? sharedUsername : $"dilux-other-{suffix}",
                Email = duplicateUsername ? $"other-{suffix}@example.com" : sharedEmail,
                PasswordHash = "hash-2"
            };
            context.Users.Add(secondUser);

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());

            // The failed insert must not linger as a tracked pending change for the cleanup below.
            context.Entry(secondUser).State = EntityState.Detached;
        }
        finally
        {
            context.Users.Remove(firstUser);
            context.SaveChanges();
        }
    }
}
