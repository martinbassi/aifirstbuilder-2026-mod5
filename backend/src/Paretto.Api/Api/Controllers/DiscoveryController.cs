using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Paretto.Api.Features.Discovery.Queries;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Discovery endpoints. Block 3 (FEAT-001d) adds `NearbyMurals`, the public, unauthenticated
/// entry point for visitors browsing `Published` murals without a session (FR-07). The query
/// contract (`GetNearbyMuralsQuery`/`GetNearbyMuralsResponse`) is fixed by Block 2 — this
/// controller only exposes it over HTTP, it does not redefine it.
/// </summary>
[ApiController]
[Route("api/discovery")]
public class DiscoveryController : ControllerBase
{
    private readonly IMediator _mediator;

    public DiscoveryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists `Published` murals within a radius around a given location, sorted by distance.
    /// Anonymous ([AllowAnonymous]) — this is the entry point a visitor without a session uses to
    /// explore the map (FR-07). Its own rate-limiting policy ("discovery", 20 req/min per IP) is
    /// stricter than the global one (100 req/min) already applied to every endpoint — mitigation R3
    /// of the discovery threat model (docs/daw/security/threat-FEAT-001d.md).
    ///
    /// `Name = "GetNearbyMurals"` on [HttpGet] (native to Microsoft.AspNetCore.Mvc's
    /// HttpMethodAttribute) is not optional: without it, NSwag (operationGenerationMode:
    /// MultipleClientsFromFirstTagAndOperationId, ADR-003) produces a non-semantic method name in
    /// the generated client (Block 5) — same reasoning documented in ADR-003 for MuralsController.
    /// Swashbuckle.AspNetCore (the base package) promotes AttributeRouteInfo.Name to operationId in
    /// the generated OpenAPI document on its own, no annotations package needed.
    /// </summary>
    [HttpGet("nearby-murals", Name = "GetNearbyMurals")]
    [AllowAnonymous]
    [EnableRateLimiting("discovery")]
    [ProducesResponseType(typeof(GetNearbyMuralsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> NearbyMurals(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] double? radiusKm,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new GetNearbyMuralsQuery { Latitude = lat, Longitude = lng, RadiusKm = radiusKm },
            cancellationToken);
        return Ok(response);
    }
}
