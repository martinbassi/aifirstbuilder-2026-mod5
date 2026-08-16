using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paretto.Api.Features.Auth.Commands;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Auth endpoints. Block 5 (Registro) adds `Register`; Block 6 (Login) and Block 7 (Logout) add
/// their own actions to this same controller afterwards.
///
/// Round 2 correction: this action used to invoke `IValidator&lt;RegisterUserCommand&gt;` by hand
/// and catch `DuplicateAccountException` itself to build a `ProblemDetails`/`ValidationProblemDetails`
/// response. Both concerns now live outside the controller — `ValidationBehavior`
/// (Common/Behaviors) runs FluentValidation automatically as a MediatR pipeline behavior, and
/// `ExceptionHandlingMiddleware` (Common/Middleware) translates `FluentValidation.ValidationException`
/// (422) and any `AppException` such as `DuplicateAccountException` (400) into the HTTP response.
/// The controller only dispatches to `IMediator` and shapes the success response, per AGENTS.md.
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
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
