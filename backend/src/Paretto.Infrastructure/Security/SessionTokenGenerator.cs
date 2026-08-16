using System.Security.Cryptography;
using System.Text;

namespace Paretto.Infrastructure.Security;

/// <summary>
/// Generates opaque session tokens (mitigation R2 in docs/daw/security/threat-FEAT-001a.md): the
/// raw token is returned to the caller once, only its SHA-256 hash is meant to be persisted (see
/// the <c>Sessions.TokenHash</c> column in Block 3).
/// </summary>
public class SessionTokenGenerator : ISessionTokenGenerator
{
    private const int TokenSizeInBytes = 32; // 256 bits

    public (string RawToken, string TokenHash) Generate()
    {
        var rawToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        var tokenHash = ComputeTokenHash(rawToken);

        return (rawToken, tokenHash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }
}
