using Mapster;
using Paretto.Api.Features.Murals.Commands;
using Paretto.Domain.Entities;

namespace Paretto.Api.Features.Murals.Mappings;

/// <summary>
/// Mapster mapping configuration for the Murals feature. Picked up automatically by
/// `TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly)` in Program.cs — no additional
/// wiring needed here, same pattern as `AuthMappingConfig`.
/// </summary>
public class MuralMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Status is mapped explicitly (enum -> string) instead of relying on Mapster's implicit
        // conversion, to keep the wire representation an intentional decision rather than an
        // incidental default.
        config.NewConfig<Mural, CreateMuralResponse>()
            .Map(dest => dest.Status, src => src.Status.ToString());
    }
}
