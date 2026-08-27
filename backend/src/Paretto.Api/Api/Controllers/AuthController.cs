using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paretto.Api.Features.Auth.Commands;
using Paretto.Api.Features.Auth.Queries;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Auth endpoints. Block 5 (Registro) adds `Register`; Block 6 (Login) and Block 7 (Logout) add
/// their own actions to this same controller afterwards. FEAT-007 Block 1 adds `Session`.
///
/// Round 2 correction (Block 5): this action used to invoke `IValidator&lt;RegisterUserCommand&gt;`
/// by hand and catch `DuplicateAccountException` itself to build a
/// `ProblemDetails`/`ValidationProblemDetails` response. Both concerns now live outside the
/// controller — `ValidationBehavior` (Common/Behaviors) runs FluentValidation automatically as a
/// MediatR pipeline behavior, and `ExceptionHandlingMiddleware` (Common/Middleware) translates
/// `FluentValidation.ValidationException` (422) and any `AppException` such as
/// `DuplicateAccountException` (400) into the HTTP response. The controller only dispatches to
/// `IMediator` and shapes the success response, per AGENTS.md.
///
/// Round 2 correction (Block 8): actions now carry `[ProducesResponseType]` so Swashbuckle
/// documents the response bodies in `swagger.json` and NSwag can generate typed methods
/// (`Observable&lt;RegisterUserResponse&gt;`/`Observable&lt;LoginResponse&gt;`) instead of
/// `Observable&lt;void&gt;`. Pure OpenAPI metadata — no runtime behavior change.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new account. Anonymous — this is the endpoint that creates the account in the
    /// first place, there is no session yet to require.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Authenticates with username/password and issues a session token. Anonymous — this is the
    /// endpoint that creates the session in the first place, there is no session yet to require.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Invalidates the caller's current session immediately (deletes its `Sessions` row) — does not
    /// depend on the token expiring on its own. Requires a valid session ([Authorize]): the auth
    /// pipeline (SessionAuthenticationHandler, Block 6) already rejects with 401 before this action
    /// runs if there is none, so no extra logic is needed here for that case.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Rehydrates the caller's current session (username/role) so the frontend can repopulate its
    /// in-memory user state after a page reload (FEAT-007), without requiring a new login. Requires
    /// a valid session ([Authorize]): the auth pipeline (SessionAuthenticationHandler) already
    /// rejects with 401 before this action runs if there is none, so no extra logic is needed here
    /// for that case.
    /// </summary>
    [HttpGet("session")]
    [Authorize]
    [ProducesResponseType(typeof(GetCurrentSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetCurrentSessionQuery(), cancellationToken);
        return Ok(response);
    }
}
