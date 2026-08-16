using Microsoft.AspNetCore.Identity;
using Paretto.Domain.Entities;

namespace Paretto.Infrastructure.Security;

/// <summary>
/// Wraps <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/> (PBKDF2 with a random
/// salt per password, default library behavior) — see spec Block 4 and NFR-01 in
/// docs/daw/prd/prd-FEAT-001a.md.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _innerHasher = new();

    public string Hash(string password)
    {
        return _innerHasher.HashPassword(user: null!, password);
    }

    public bool Verify(string password, string hash)
    {
        var result = _innerHasher.VerifyHashedPassword(user: null!, hash, password);
        return result != PasswordVerificationResult.Failed;
    }
}
