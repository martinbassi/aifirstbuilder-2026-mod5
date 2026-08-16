using Microsoft.EntityFrameworkCore;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (Domain: entidad Mural) de FEAT-001b.
///
/// Mismo mecanismo que AuthPersistenceTests.cs: corre contra una instancia real de SQL Server 2025,
/// leyendo la cadena de conexión de la variable de entorno `ConnectionStrings__DefaultConnection`.
/// Si la variable no está seteada, el test falla ruidosamente (no se salta ni cae a otro motor).
///
/// Aislamiento de datos (testing.instructions.md, Regla #0): cada test crea su propio `User`
/// (dependencia FK de `Mural`) y su propio `Mural` con un `PhotoBlobName` sufijado con GUID, y borra
/// exactamente lo que creó en un `finally` para no dejar filas huérfanas en la base compartida.
/// </summary>
public class MuralPersistenceTests
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
            .UseSqlServer(GetConnectionString())
            .Options;

        return new AppDbContext(options);
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
            Latitude = -34.6037,
            Longitude = -58.3816,
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
            Latitude = 40.7128,
            Longitude = -74.0060
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
}
