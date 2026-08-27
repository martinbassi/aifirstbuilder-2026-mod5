using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Features.Auth.Queries;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (FEAT-007) — unit test for the defensive case documented in
/// GetCurrentSessionQueryHandler: a NameIdentifier claim carrying a well-formed Guid that does not
/// correspond to any row in Users. Unreachable in practice via [Authorize] (the FK
/// Session.UserId -&gt; User.Id guarantees the row exists), but reproducible by constructing the
/// handler directly against an empty InMemory AppDbContext and a hand-built ClaimsPrincipal — same
/// spirit as GetMuralByIdQueryHandler.ReadCallerIdentity / CreateMuralCommandHandler.ReadUserId's
/// own defensive branches.
/// </summary>
public class GetCurrentSessionQueryHandlerTests
{
    [Fact]
    public async Task Handle_throws_InvalidOperationException_when_the_claimed_user_does_not_exist_in_the_database()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new AppDbContext(options);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Standard.ToString()),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        var handler = new GetCurrentSessionQueryHandler(dbContext, httpContextAccessor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new GetCurrentSessionQuery(), CancellationToken.None));
    }
}
