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
    }
}
