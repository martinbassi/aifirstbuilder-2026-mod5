using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paretto.Infrastructure.Data;

namespace Paretto.Infrastructure.Auth;

/// <summary>
/// Server-side session authentication scheme (opaque token, not JWT — PLAN decision, spec Block 6
/// and docs/daw/security/threat-FEAT-001a.md). Reads `Authorization: Bearer {token}`, hashes the
/// raw token with SHA-256 and looks up a `Session` row by `TokenHash`. If the row does not exist,
/// or `Session.ExpiresAt` has already passed, authentication fails — the expiry check runs on
/// every request, not just once at token issuance (NFR-03).
///
/// The resulting `ClaimsPrincipal` carries `ClaimTypes.NameIdentifier` (`Session.UserId`) and
/// `ClaimTypes.Role` (`Session.User.Role`), both read from the database row every time — never
/// from a claim embedded in the incoming token/request itself. This is the elevation-of-privilege
/// mitigation from the threat model: nothing the caller sends can influence its own role.
/// </summary>
public class SessionAuthenticationHandler : AuthenticationHandler<SessionAuthenticationSchemeOptions>
{
    public const string SchemeName = "SessionAuth";

    private const string BearerPrefix = "Bearer ";

    private readonly AppDbContext _dbContext;

    public SessionAuthenticationHandler(
        IOptionsMonitor<SessionAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext dbContext) : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValues))
        {
            return AuthenticateResult.Fail("Missing Authorization header.");
        }

        var headerValue = authorizationHeaderValues.ToString();

        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Malformed Authorization header.");
        }

        var rawToken = headerValue[BearerPrefix.Length..].Trim();

        if (string.IsNullOrEmpty(rawToken))
        {
            return AuthenticateResult.Fail("Missing bearer token.");
        }

        var tokenHash = ComputeTokenHash(rawToken);

        // A token that does not match any TokenHash (malformed or simply unknown) is treated
        // exactly like a nonexistent one — there is no separate code path for "malformed".
        var session = await _dbContext.Sessions
            .Include(s => s.User)
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash);

        if (session is null || session.User is null || session.ExpiresAt < DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("Invalid or expired session.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Role, session.User.Role.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }
}
