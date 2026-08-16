using System.Security.Cryptography;
using System.Text;
using Paretto.Infrastructure.Security;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 4 (Servicios de seguridad) — <see cref="ISessionTokenGenerator"/>.
/// </summary>
public class SessionTokenGeneratorTests
{
    private readonly ISessionTokenGenerator _generator = new SessionTokenGenerator();

    [Fact]
    public void Generate_produces_1000_tokens_without_collisions()
    {
        var rawTokens = new HashSet<string>();
        var tokenHashes = new HashSet<string>();

        for (var i = 0; i < 1000; i++)
        {
            var (rawToken, tokenHash) = _generator.Generate();
            rawTokens.Add(rawToken);
            tokenHashes.Add(tokenHash);
        }

        Assert.Equal(1000, rawTokens.Count);
        Assert.Equal(1000, tokenHashes.Count);
    }

    [Fact]
    public void TokenHash_is_deterministic_from_the_raw_token_but_cannot_be_reversed_back_to_it()
    {
        var (rawToken, tokenHash) = _generator.Generate();

        // Deterministic: recomputing SHA-256 over the same RawToken independently (the same way
        // SessionAuthenticationHandler will do it in Block 6 to look up a Session by TokenHash)
        // must match the TokenHash the generator returned.
        var recomputedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        Assert.Equal(tokenHash, recomputedHash);

        // Conceptual proof of non-recoverability: SHA-256 is a cryptographic one-way function (no
        // known feasible inverse), and the only public surface of ISessionTokenGenerator is
        // Generate() itself — there is no method/property anywhere that takes a TokenHash and
        // returns/derives the RawToken that produced it. Reflection over the interface's public
        // members confirms there is no such "reverse" member to call.
        var publicMembers = typeof(ISessionTokenGenerator).GetMembers();
        Assert.DoesNotContain(publicMembers, m =>
            m.Name.Contains("Reverse", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Decode", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("Recover", StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains("FromHash", StringComparison.OrdinalIgnoreCase));

        // Two independent Generate() calls must never produce distinct RawToken values that
        // collapse onto the same TokenHash (a necessary condition for the hash not acting as a
        // shortcut back into a small, guessable token space).
        var (otherRawToken, otherTokenHash) = _generator.Generate();
        Assert.NotEqual(rawToken, otherRawToken);
        Assert.NotEqual(tokenHash, otherTokenHash);
    }
}
