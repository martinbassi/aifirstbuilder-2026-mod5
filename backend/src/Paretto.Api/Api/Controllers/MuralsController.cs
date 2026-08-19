using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Paretto.Api.Features.Murals.Commands;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Mural endpoints. Block 4 (Crear mural) adds `Create`; Block 5 (Consultar mural) adds `GetById`
/// afterwards.
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
}
