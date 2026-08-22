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
    // 10 MB photo limit already validated by `CreateMuralCommandValidator` + margin for the rest of
    // the multipart/form-data (threat model R2).
    private const long MaxRequestBodyBytes = 11 * 1024 * 1024;

    private readonly IMediator _mediator;

    public MuralsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new mural from a multipart/form-data upload. Requires a valid session ([Authorize])
    /// — the author is always the caller of that session, never a value taken from the request body
    /// (see `CreateMuralCommand`).
    ///
    /// `[RequestFormLimits(MultipartBodyLengthLimit = ...)]` caps the whole multipart body at ~11 MB
    /// (10 MB photo limit already enforced by `CreateMuralCommandValidator` + margin for the rest of
    /// the multipart/form-data) so the request is rejected while the managed multipart reader parses
    /// it, BEFORE it ever reaches the Handler/FluentValidation (threat model R2 — DoS por upload sin
    /// límite, `docs/daw/security/threat-FEAT-001b.md`, which explicitly allows either
    /// `[RequestSizeLimit]` or `RequestFormLimits`). `[RequestSizeLimit]` was tried first, but it
    /// only takes effect on Kestrel's own transport-level body tracking — under
    /// `WebApplicationFactory`'s in-memory `TestServer`, used by this ticket's test suite, it is a
    /// no-op (confirmed empirically: even a byte-sized limit let arbitrarily large requests through),
    /// so it could not be covered by an automated test. `RequestFormLimits` is enforced by
    /// `Microsoft.AspNetCore.Http.Features`' managed multipart reader, host-independent, and IS
    /// exercised correctly under `TestServer`. ASP.NET Core surfaces the violation as `400 Bad
    /// Request` (`[ApiController]`'s automatic `ModelState` validation on the "Failed to read the
    /// request form" error), not `413` — verified against this implementation's actual behavior, not
    /// assumed.
    /// </summary>
    [HttpPost]
    [Authorize]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(CreateMuralResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
