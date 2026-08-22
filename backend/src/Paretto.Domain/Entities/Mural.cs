using Paretto.Domain.Enums;

namespace Paretto.Domain.Entities;

public class Mural
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Nombre del blob en Azure Storage (p. ej. `{Guid}{extensión}`), NUNCA una URL ni el nombre de
    /// archivo original del cliente (mitigación de path traversal/overwrite, threat model R4 de
    /// FEAT-001b — el blob name lo genera siempre quien sube el archivo, ver spec Block 4).
    /// </summary>
    public string PhotoBlobName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public MuralStatus Status { get; set; } = MuralStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
