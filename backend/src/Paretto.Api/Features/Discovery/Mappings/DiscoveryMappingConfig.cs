using Mapster;
using Paretto.Api.Features.Discovery.Queries;
using Paretto.Domain.Entities;

namespace Paretto.Api.Features.Discovery.Mappings;

/// <summary>
/// Mapster mapping configuration for the Discovery feature. Picked up automatically by
/// `TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly)` in Program.cs — no additional
/// wiring needed here, same pattern as `MuralMappingConfig`/`ModerationMappingConfig`.
/// </summary>
public class DiscoveryMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // PhotoUrl and DistanceKm are not properties of Mural — PhotoUrl is a short-lived SAS URL
        // and DistanceKm a computed value, both set by the Handler after the automap (same pattern
        // as `PhotoUrl` in `MuralMappingConfig`). Explicitly ignored here so Mapster does not
        // complain about an unmapped destination member.
        config.NewConfig<Mural, NearbyMuralItemResponse>()
            .Ignore(dest => dest.PhotoUrl)
            .Ignore(dest => dest.DistanceKm);
    }
}
