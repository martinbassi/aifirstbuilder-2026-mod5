using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Paretto.Infrastructure.Data;

/// <summary>
/// Design-time factory used exclusively by the `dotnet ef migrations`/`dotnet ef database update`
/// CLI to scaffold and apply migrations without going through Paretto.Api's DI/Program.cs pipeline
/// (Block 3 owns AppDbContext and its migrations, not Program.cs — that is Block 1, already
/// closed). `migrations add` only needs a SqlServer-flavored connection string to build the model
/// and does not actually connect; `database update` does connect, so this reads the real string
/// from the `ConnectionStrings__DefaultConnection` environment variable when present (the CLI
/// convention for this — never hardcoded here), falling back to a credential-less placeholder that
/// is enough for `migrations add` but will fail a real `database update` without the env var set.
/// The runtime connection string for the app itself lives in user-secrets / appsettings, wired in
/// Program.cs (Block 1) — this factory never reads that configuration system, by design, since it
/// must work standalone from the CLI.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=Paretto;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
