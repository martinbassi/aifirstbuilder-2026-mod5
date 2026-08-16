using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Features.Auth.Commands;

/// <summary>
/// No parameters of its own — the session to invalidate is entirely derived from the
/// `Authorization` header of the current HTTP request, not from anything the client puts in a
/// body (spec: "sin body").
/// </summary>
public class LogoutCommand : IRequest
{
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private const string BearerPrefix = "Bearer ";

    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LogoutCommandHandler(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // By the time this Handler runs, [Authorize] on the controller action has already required
        // a valid session — this raw-token extraction is only for computing the TokenHash to delete,
        // it does not re-validate the session (that already happened in the auth pipeline).
        var rawToken = ExtractRawToken(_httpContextAccessor.HttpContext);

        if (string.IsNullOrEmpty(rawToken))
        {
            // Defensive only: [Authorize] should make this unreachable in practice. Nothing to
            // delete without a token, so this is a silent no-op rather than a thrown exception.
            return;
        }

        var tokenHash = ComputeTokenHash(rawToken);

        var session = await _dbContext.Sessions
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (session is not null)
        {
            _dbContext.Sessions.Remove(session);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? ExtractRawToken(HttpContext? httpContext)
    {
        if (httpContext is null
            || !httpContext.Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValues))
        {
            return null;
        }

        var headerValue = authorizationHeaderValues.ToString();

        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rawToken = headerValue[BearerPrefix.Length..].Trim();
        return string.IsNullOrEmpty(rawToken) ? null : rawToken;
    }

    // Duplicated deliberately, not extracted into a method shared with
    // SessionAuthenticationHandler (Block 6, Paretto.Infrastructure.Auth): Block 7 is scoped to
    // "no toques Blocks 1-6 más allá de agregar la acción al AuthController existente", so
    // SessionAuthenticationHandler.cs is not touched. A single-line SHA-256-hex computation is not
    // worth introducing a new shared abstraction/file for outside that boundary.
    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hashBytes);
    }
}
