using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paretto.Api.Features.Moderation.Commands;
using Paretto.Api.Features.Moderation.Queries;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Admin-only moderation endpoints. Block 2 (Listar murales pendientes) adds `Pending`; Block 3
/// (Aprobar) adds `approve`; Block 4 (Rechazar) will add its own action. `[Authorize(Roles =
/// "Administrator")]`
/// at the class level covers every action in this controller — declarative, no manual role check
/// anywhere: ASP.NET's own authorization pipeline produces 401 (no session) / 403 (session without
/// the role) before any action runs.
/// </summary>
[ApiController]
[Route("api/moderation/murals")]
[Authorize(Roles = "Administrator")]
public class ModerationController : ControllerBase
{
    private readonly IMediator _mediator;

    public ModerationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists the Pending murals waiting for moderation, oldest first, paginated (FR-01/AC-01/AC-02).
    /// `page`/`pageSize` default to `1`/`20`; out-of-range values (`page` &lt; 1 or `pageSize` outside
    /// `1..50`) are rejected by `GetPendingMuralsQueryValidator` through the existing FluentValidation
    /// pipeline, which — like every other validator in this codebase (`ValidationBehavior` +
    /// `ExceptionHandlingMiddleware`) — responds 422, not 400 (see `MuralsController.Create`'s same
    /// `[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]` precedent for
    /// `CreateMuralCommandValidator` failures).
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(GetPendingMuralsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPendingMuralsQuery { Page = page, PageSize = pageSize };
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Approves a Pending mural, moving it to Published (FR-02/AC-03/AC-04). `404` if the mural does
    /// not exist (`ModeratedMuralNotFoundException`); `409` if it exists but is not `Pending`
    /// (`MuralNotPendingException`) — already `Published` or `Rejected`.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ModerationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ApproveMuralCommand { MuralId = id }, cancellationToken);
        return Ok(response);
    }
}
