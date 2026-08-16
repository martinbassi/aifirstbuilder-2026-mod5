namespace Paretto.Domain.Entities;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 en hex del token opaco de sesión. El token en claro NUNCA se persiste
    /// (mitigación R2 del threat model, docs/daw/security/threat-FEAT-001a.md).
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
