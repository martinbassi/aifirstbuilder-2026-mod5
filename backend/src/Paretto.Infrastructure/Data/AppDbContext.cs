using Microsoft.EntityFrameworkCore;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;

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
            entity.Property(m => m.Latitude).IsRequired();
            entity.Property(m => m.Longitude).IsRequired();
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

            // Índice B-tree compuesto (no espacial: SQL Server no ofrece uno para columnas `float`
            // sueltas) que permite que el filtro Status == Published junto con el rango de
            // Latitude/Longitude del bounding box (FEAT-001d, GeoDistanceCalculator.BoundingBox) use
            // seek en vez de scan completo de la tabla. Ver ADR-005.
            entity.HasIndex(m => new { m.Status, m.Latitude, m.Longitude })
                .HasDatabaseName("IX_Murals_Status_Latitude_Longitude");
        });
    }
}
