using NetTopologySuite.Geometries;
using Paretto.Domain.Enums;

namespace Paretto.Domain.Entities;

public class Mural
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del blob en Azure Storage (p. ej. `{Guid}{extensión}`), NUNCA una URL ni el nombre de
    /// archivo original del cliente (mitigación de path traversal/overwrite, threat model R4 de
    /// FEAT-001b — el blob name lo genera siempre quien sube el archivo, ver spec Block 4).
    /// </summary>
    public string PhotoBlobName { get; set; } = string.Empty;

    /// <summary>
    /// Ubicación del mural, mapeada a `geography` (SRID 4326) en SQL Server (FEAT-009, reemplaza las
    /// columnas sueltas `Latitude`/`Longitude`). Se construye SIEMPRE vía <see cref="CreateLocation"/>
    /// — nunca instanciando `Point` a mano en otro lugar del código C# (mitigación de threat model
    /// R2: un swap accidental de ejes X/Y rompería silenciosamente todos los cálculos de distancia).
    /// </summary>
    public Point Location { get; set; } = CreateLocation(0, 0);

    /// <summary>
    /// Propiedades computadas de solo lectura, ignoradas por EF Core (ver
    /// `AppDbContext.OnModelCreating`), para que `DiscoveryMappingConfig`/`MuralMappingConfig` sigan
    /// mapeando `Latitude`/`Longitude` por convención de nombre de Mapster sin ningún cambio en esos
    /// dos archivos (FEAT-009).
    /// </summary>
    public double Latitude => Location.Y;

    public double Longitude => Location.X;

    /// <summary>
    /// Único punto del código C# donde se decide el orden de ejes entre el `(latitud, longitud)` en
    /// que piensa el resto del sistema y el `(X=longitud, Y=latitud)` que usa `Point` de
    /// NetTopologySuite (hallazgo del arch-auditor en PLAN, mitigación de threat model R2 de
    /// FEAT-009) — todo lo demás en C# debe llamar a este factory, nunca construir un `Point` a mano.
    /// </summary>
    public static Point CreateLocation(double latitude, double longitude) =>
        new Point(longitude, latitude) { SRID = 4326 };

    public MuralStatus Status { get; set; } = MuralStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
