using Microsoft.EntityFrameworkCore;

namespace Paretto.Infrastructure.Data;

/// <summary>
/// Minimal placeholder so Block 1's Program.cs can wire AddDbContext&lt;AppDbContext&gt;() and
/// compile end to end. Block 3 (Dominio y persistencia de Auth) owns the real entity model
/// (DbSet&lt;User&gt;, DbSet&lt;Session&gt;) and the initial migration — this class only exists here
/// to satisfy the Block 1 completion criterion ("dotnet build compila sin errores").
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
