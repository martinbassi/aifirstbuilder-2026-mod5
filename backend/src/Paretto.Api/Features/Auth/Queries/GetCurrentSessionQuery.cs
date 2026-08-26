using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Features.Auth.Queries;

/// <summary>
/// No parameters of its own — same pattern as `LogoutCommand`: the session to rehydrate is
/// entirely derived from the `ClaimsPrincipal` of the current HTTP request, never from anything the
/// client puts in the request itself.
/// </summary>
public class GetCurrentSessionQuery : IRequest<GetCurrentSessionResponse>
{
}

public class GetCurrentSessionResponse
{
    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

public class GetCurrentSessionQueryHandler : IRequestHandler<GetCurrentSessionQuery, GetCurrentSessionResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetCurrentSessionQueryHandler(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<GetCurrentSessionResponse> Handle(GetCurrentSessionQuery request, CancellationToken cancellationToken)
    {
        var (userId, role) = ReadCallerIdentity(_httpContextAccessor.HttpContext);

        // Role comes straight from the claim, never from a fresh DB query — SessionAuthenticationHandler
        // already resolved it fresh from Session.User.Role for this same request (with the
        // elevation-of-privilege mitigation already applied there), same precedent as
        // GetMuralByIdQuery.ReadCallerIdentity. This query only hits the DB for Username, which
        // travels in no claim today.
        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            // Defensive only: [Authorize] already requires a valid session, and the FK
            // Session.UserId -> User.Id guarantees this row exists. Same criterion as
            // CreateMuralCommandHandler.ReadUserId: not worth a typed domain exception for a case
            // that should never execute in practice.
            throw new InvalidOperationException("Authenticated session references a nonexistent user.");
        }

        return new GetCurrentSessionResponse { Username = user.Username, Role = role };
    }

    private static (Guid UserId, string Role) ReadCallerIdentity(HttpContext? httpContext)
    {
        // Defensive only: [Authorize] on the controller action already requires a valid session
        // carrying both claims, so this branch should be unreachable in practice. Same precedent as
        // CreateMuralCommandHandler.ReadUserId / GetMuralByIdQuery.ReadCallerIdentity.
        var user = httpContext?.User
            ?? throw new InvalidOperationException("Authenticated request is missing an HttpContext.");

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated request is missing a NameIdentifier claim.");
        var roleClaim = user.FindFirst(ClaimTypes.Role)
            ?? throw new InvalidOperationException("Authenticated request is missing a Role claim.");

        return (Guid.Parse(userIdClaim.Value), roleClaim.Value);
    }
}
