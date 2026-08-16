using Mapster;
using Paretto.Api.Features.Auth.Commands;
using Paretto.Domain.Entities;

namespace Paretto.Api.Features.Auth.Mappings;

/// <summary>
/// Mapster mapping configuration for the Auth feature. Picked up automatically by
/// `TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly)` in Program.cs (Block 1) — no
/// additional wiring needed here.
/// </summary>
public class AuthMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Never map PasswordHash — the response must never carry the password hash.
        config.NewConfig<User, RegisterUserResponse>();
    }
}
