using Mapster;
using Paretto.Api.Features.Murals.Commands;
using Paretto.Api.Features.Murals.Queries;
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

        // PhotoUrl is not a property of Mural — it is a short-lived SAS URL computed by the Handler
        // (Block 5) after the automap, from `PhotoBlobName`. Explicitly ignored here so Mapster does
        // not complain about an unmapped destination member, and so the Handler setting it manually
        // afterwards reads as deliberate rather than accidental.
        config.NewConfig<Mural, MuralResponse>()
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Ignore(dest => dest.PhotoUrl);
    }
}
