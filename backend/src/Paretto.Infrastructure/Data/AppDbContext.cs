using Microsoft.EntityFrameworkCore;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data.ValueConverters;

namespace Paretto.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<Mural> Murals => Set<Mural>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            // PBKDF2 output (Microsoft.AspNetCore.Identity.PasswordHasher<User>, format v3) is a
            // fixed-size byte array Base64-encoded to ~88 chars; 200 leaves comfortable margin
            // without leaving the column unbounded (nvarchar(max)).
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Role).IsRequired().HasDefaultValue(UserRole.Standard);

            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Session>(entity =>
        {
            // SHA-256 hex digest: always exactly 64 characters.
            entity.Property(s => s.TokenHash).IsRequired().HasMaxLength(64);
            entity.Property(s => s.ExpiresAt).IsRequired();

            entity.HasIndex(s => s.TokenHash).IsUnique();

            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Mural>(entity =>
        {
            // Blobs se nombran `{Guid}{extensión}`, muy por debajo del límite.
            entity.Property(m => m.PhotoBlobName).IsRequired().HasMaxLength(300);
            entity.Property(m => m.Title).IsRequired().HasMaxLength(50);
            // FEAT-009: `Latitude`/`Longitude` pasan a ser propiedades computadas de solo lectura
            // (`Location.Y`/`Location.X`) — EF Core las ignora, la única columna persistida es
            // `Location` (`geography`, ver la migración `MuralLocationGeography`).
            entity.Property(m => m.Location).HasColumnType("geography").IsRequired();
            entity.Ignore(m => m.Latitude);
            entity.Ignore(m => m.Longitude);
            entity.Property(m => m.Status).IsRequired().HasDefaultValue(MuralStatus.Pending);
            entity.Property(m => m.CreatedAt).IsRequired();

            // A diferencia de Session (que cascadea), Restrict: un mural es contenido generado por
            // el usuario y no debe desaparecer silenciosamente si en el futuro se implementara
            // borrado de cuentas — decisión conservadora, no hay requisito de borrado de cuenta en
            // este PRD ni en el de FEAT-001a.
            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // El índice B-tree compuesto `IX_Murals_Status_Latitude_Longitude` (FEAT-001d) se
            // eliminó junto con las columnas `Latitude`/`Longitude` — reemplazado por el índice
            // espacial `SPATIAL_IX_Murals_Location` sobre `Location`, creado con SQL crudo en la
            // migración `MuralLocationGeography` (el Fluent API de EF Core no tiene soporte nativo
            // para `CREATE SPATIAL INDEX`). Ver ADR-005 (revisado, FEAT-009).
        });
    }
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
