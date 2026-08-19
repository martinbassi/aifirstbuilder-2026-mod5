using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paretto.Api.Features.Murals.Commands;
using Paretto.Api.Features.Murals.Queries;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Mural endpoints. Block 4 (Crear mural) adds `Create`; Block 5 (Consultar mural) adds `GetById`.
/// </summary>
[ApiController]
[Route("api/murals")]
public class MuralsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MuralsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new mural from a multipart/form-data upload. Requires a valid session ([Authorize])
    /// — the author is always the caller of that session, never a value taken from the request body
    /// (see `CreateMuralCommand`).
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CreateMuralResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromForm] CreateMuralCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Fetches a single mural, including a short-lived read-only SAS URL for its photo (FR-15).
    /// Requires a valid session ([Authorize]) — any authenticated user may attempt this, but the
    /// Handler applies fine-grained authorization: a Pending/Rejected mural is only visible to its
    /// owner or an Administrator, otherwise it responds identically to a nonexistent mural (404,
    /// same generic message) to avoid enumeration (see `MuralAccessDeniedException`).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(MuralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetMuralByIdQuery { Id = id }, cancellationToken);
        return Ok(response);
    }
}
