using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (Domain: entidad Mural) de FEAT-001b, ampliado por Block 1 de FEAT-009 (migración de
/// `Latitude`/`Longitude` sueltos a `Location` `geography`/NetTopologySuite).
///
/// Mismo mecanismo que AuthPersistenceTests.cs: corre contra una instancia real de SQL Server 2025,
/// leyendo la cadena de conexión de la variable de entorno `ConnectionStrings__DefaultConnection`.
/// Si la variable no está seteada, el test falla ruidosamente (no se salta ni cae a otro motor).
///
/// Aislamiento de datos (testing.instructions.md, Regla #0): cada test crea su propio `User`
/// (dependencia FK de `Mural`) y su propio `Mural` con un `PhotoBlobName` sufijado con GUID, y borra
/// exactamente lo que creó en un `finally` para no dejar filas huérfanas en la base compartida.
///
/// Los dos tests de migración (FEAT-009) NO corren contra la base compartida `Paretto_Dev`: crean y
/// destruyen una base de datos efímera propia (`Paretto_Test_Migration_{guid}`) en la misma instancia
/// de SQL Server. Se decidió así (lectura razonable de "aplicar las migraciones hasta la anterior a
/// esta... aplicar esta migración" del spec, ya que no especifica el mecanismo de aislamiento) porque
/// migrar la base compartida hacia atrás/adelante alteraría el esquema real de `Murals` mientras otras
/// clases de test (p. ej. `AuthPersistenceTests`, que también usa SQL Server real) pueden estar
/// corriendo en paralelo — xUnit no serializa por default entre clases de test distintas. Una base
/// efímera aislada evita esa colisión sin necesidad de tocar ningún archivo fuera de este bloque.
/// </summary>
public class MuralPersistenceTests
{
    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection to run persistence tests against a real SQL Server instance.");
    }

    /// <summary>
    /// Reemplaza el segmento `Database=...;` de la cadena de conexión configurada por otro nombre de
    /// base, preservando servidor/credenciales — usado por los tests de migración para apuntar a
    /// `master` (crear/destruir la base efímera) o a la base efímera misma.
    /// </summary>
    private static string BuildConnectionString(string databaseName)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            GetConnectionString(),
            "Database=[^;]*",
            $"Database={databaseName}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static AppDbContext CreateContext(string? databaseNameOverride = null)
    {
        var connectionString = databaseNameOverride is null
            ? GetConnectionString()
            : BuildConnectionString(databaseNameOverride);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }

    // Nombre de base de datos generado en test: nunca proviene de input de usuario, por eso la
    // interpolación directa en DDL (CREATE/ALTER/DROP DATABASE, que SQL Server no permite
    // parametrizar) es aceptable acá — no es el patrón que R1 del threat model prohíbe (ese aplica a
    // la consulta espacial de producción con input real de usuario, Block 2).
    private static void CreateDatabase(string databaseName)
    {
        using var master = CreateContext("master");
#pragma warning disable EF1002 // DDL identifier (CREATE DATABASE), cannot be parameterized; see comment above.
        master.Database.ExecuteSqlRaw($"CREATE DATABASE [{databaseName}]");
#pragma warning restore EF1002
    }

    private static void DropDatabase(string databaseName)
    {
        using var master = CreateContext("master");
#pragma warning disable EF1002 // DDL identifier (DROP DATABASE), cannot be parameterized; see comment above.
        master.Database.ExecuteSqlRaw(
            $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
#pragma warning restore EF1002
    }

    [Fact]
    public void CreateLocation_assigns_latitude_to_Y_and_longitude_to_X_never_swapped()
    {
        var point = Mural.CreateLocation(-34.6037, -58.3816);

        Assert.Equal(-34.6037, point.Y);
        Assert.Equal(-58.3816, point.X);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public void Creating_a_mural_with_all_fields_persists_and_retrieves_the_same_values()
    {
        using var context = CreateContext();
        context.Database.Migrate();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Username = $"dilux-{suffix}",
            Email = $"dilux-{suffix}@example.com",
            PasswordHash = "hash-1"
        };
        context.Users.Add(user);
        context.SaveChanges();

        var createdAt = DateTime.UtcNow;
        var mural = new Mural
        {
            UserId = user.Id,
            PhotoBlobName = $"{suffix}.jpg",
            Location = Mural.CreateLocation(-34.6037, -58.3816),
            Status = MuralStatus.Rejected,
            CreatedAt = createdAt
        };

        try
        {
            context.Murals.Add(mural);
            context.SaveChanges();

            using var readContext = CreateContext();
            var persisted = readContext.Murals.Single(m => m.Id == mural.Id);

            Assert.Equal(user.Id, persisted.UserId);
            Assert.Equal($"{suffix}.jpg", persisted.PhotoBlobName);
            Assert.Equal(-34.6037, persisted.Latitude);
            Assert.Equal(-58.3816, persisted.Longitude);
            Assert.Equal(MuralStatus.Rejected, persisted.Status);
            Assert.Equal(createdAt, persisted.CreatedAt, TimeSpan.FromSeconds(1));
        }
        finally
        {
            context.Murals.Remove(mural);
            context.Users.Remove(user);
            context.SaveChanges();
        }
    }

    [Fact]
    public void Creating_a_mural_without_specifying_status_defaults_to_pending_and_created_at_is_populated_automatically()
    {
        using var context = CreateContext();
        context.Database.Migrate();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Username = $"dilux-{suffix}",
            Email = $"dilux-{suffix}@example.com",
            PasswordHash = "hash-1"
        };
        context.Users.Add(user);
        context.SaveChanges();

        var mural = new Mural
        {
            UserId = user.Id,
            PhotoBlobName = $"{suffix}.png",
            Location = Mural.CreateLocation(40.7128, -74.0060),
        };

        try
        {
            context.Murals.Add(mural);
            context.SaveChanges();

            using var readContext = CreateContext();
            var persisted = readContext.Murals.Single(m => m.Id == mural.Id);

            Assert.Equal(MuralStatus.Pending, persisted.Status);
            Assert.NotEqual(default, persisted.CreatedAt);
        }
        finally
        {
            context.Murals.Remove(mural);
            context.Users.Remove(user);
            context.SaveChanges();
        }
    }

    [Fact]
    public void Migration_MuralLocationGeography_backfills_Location_from_existing_Latitude_and_Longitude()
    {
        var databaseName = $"Paretto_Test_Migration_{Guid.NewGuid():N}";
        CreateDatabase(databaseName);
        try
        {
            var userId = Guid.NewGuid();
            var muralId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            using (var migrationContext = CreateContext(databaseName))
            {
                var migrator = migrationContext.GetInfrastructure().GetRequiredService<IMigrator>();

                // Aplica hasta la migración anterior a esta (AC-04: la tabla todavía tiene
                // Latitude/Longitude, no existe Location).
                migrator.Migrate("MuralTitle");

                migrationContext.Database.ExecuteSqlInterpolated($@"
                    INSERT INTO Users (Id, Username, Email, PasswordHash, Role, CreatedAt)
                    VALUES ({userId}, {$"migration-{userId:N}"}, {$"migration-{userId:N}@example.com"}, {"hash"}, {0}, {createdAt})");

                migrationContext.Database.ExecuteSqlInterpolated($@"
                    INSERT INTO Murals (Id, UserId, Title, PhotoBlobName, Latitude, Longitude, Status, CreatedAt)
                    VALUES ({muralId}, {userId}, {"Migration test"}, {"migration-test.jpg"}, {-34.6037}, {-58.3816}, {0}, {createdAt})");

                // Aplica esta migración (y cualquier otra pendiente) sobre la fila ya existente.
                migrator.Migrate();
            }

            using var readContext = CreateContext(databaseName);
            var persisted = readContext.Murals.Single(m => m.Id == muralId);

            Assert.Equal(-34.6037, persisted.Latitude);
            Assert.Equal(-58.3816, persisted.Longitude);
        }
        finally
        {
            DropDatabase(databaseName);
        }
    }

    [Fact]
    public void Migration_MuralLocationGeography_fails_explicitly_when_an_existing_row_has_out_of_range_latitude()
    {
        var databaseName = $"Paretto_Test_Migration_{Guid.NewGuid():N}";
        CreateDatabase(databaseName);
        try
        {
            var userId = Guid.NewGuid();
            var muralId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            using var migrationContext = CreateContext(databaseName);
            var migrator = migrationContext.GetInfrastructure().GetRequiredService<IMigrator>();

            migrator.Migrate("MuralTitle");

            migrationContext.Database.ExecuteSqlInterpolated($@"
                INSERT INTO Users (Id, Username, Email, PasswordHash, Role, CreatedAt)
                VALUES ({userId}, {$"migration-{userId:N}"}, {$"migration-{userId:N}@example.com"}, {"hash"}, {0}, {createdAt})");

            // 200 no es una latitud válida en WGS84 (rango -90..90) — geography::Point debe lanzar
            // durante el backfill (AC-05).
            migrationContext.Database.ExecuteSqlInterpolated($@"
                INSERT INTO Murals (Id, UserId, Title, PhotoBlobName, Latitude, Longitude, Status, CreatedAt)
                VALUES ({muralId}, {userId}, {"Invalid migration test"}, {"invalid-migration-test.jpg"}, {200.0}, {-58.3816}, {0}, {createdAt})");

            Assert.ThrowsAny<Exception>(() => migrator.Migrate());

            // La transacción por defecto de EF Core revirtió todo Up(): la migración no quedó
            // registrada como aplicada (R3 del threat model — sin estado intermedio inconsistente).
            var appliedMigrations = migrationContext.Database.GetAppliedMigrations();
            Assert.DoesNotContain(appliedMigrations, id => id.Contains("MuralLocationGeography"));
        }
        finally
        {
            DropDatabase(databaseName);
        }
    }

    [Fact]
    public void AppDbContextFactory_builds_a_model_that_supports_the_Point_column_used_by_the_migration()
    {
        var factory = new AppDbContextFactory();
        using var context = factory.CreateDbContext(Array.Empty<string>());

        // Si `.UseNetTopologySuite()` faltara en el factory de diseño, construir el modelo (que
        // incluye `Mural.Location`, tipo `Point`) lanzaría al intentar mapear un CLR type que el
        // proveedor de SqlServer no reconoce sin esa extensión (AC-06).
        var exception = Record.Exception(() => context.Model.FindEntityType(typeof(Mural)));

        Assert.Null(exception);
    }
}
