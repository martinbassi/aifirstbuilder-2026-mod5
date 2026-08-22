using Mapster;
using Paretto.Api.Features.Moderation.Commands;
using Paretto.Domain.Entities;

namespace Paretto.Api.Features.Moderation.Mappings;

/// <summary>
/// Mapster mapping configuration for the Moderation feature. Picked up automatically by
/// `TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly)` in Program.cs — no additional
/// wiring needed here, same pattern as `MuralMappingConfig`/`AuthMappingConfig`.
/// </summary>
public class ModerationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Status is mapped explicitly (enum -> string) instead of relying on Mapster's implicit
        // conversion, to keep the wire representation an intentional decision rather than an
        // incidental default — same precedent as `MuralMappingConfig`.
        config.NewConfig<Mural, ModerationActionResponse>()
            .Map(dest => dest.Status, src => src.Status.ToString());
    }
}
