using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Paretto.Api.Features.Addresses.Queries;

namespace Paretto.Api.Api.Controllers;

/// <summary>
/// Proxy endpoints towards the external address provider `direcciones.ide.uy` (spec Block 1
/// FEAT-011). Only dispatches to `IMediator` — the 503 translation for an unavailable provider
/// lives entirely in the Handler via `AddressProviderUnavailableException`, never inspected by hand
/// here (arch-auditor finding referenced by the spec).
///
/// Both actions require a session (`[Authorize]`, FR-07) and share the `"addresses"` rate-limiting
/// policy (20 req/min per IP, same scheme as `"discovery"`) — the external provider is free and
/// key-less, so throttling our own outbound abuse towards it matters as much as throttling inbound
/// abuse of our own backend (threat model R1).
/// </summary>
[ApiController]
[Route("api/addresses")]
[Authorize]
[EnableRateLimiting("addresses")]
public class AddressesController : ControllerBase
{
    private readonly IMediator _mediator;

    public AddressesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Autocomplete search (FR-04/FR-19/AC-17). `Name = "SearchAddresses"` (native to
    /// `Microsoft.AspNetCore.Mvc`'s `HttpMethodAttribute`) is not optional: without it, NSwag
    /// (`operationGenerationMode: MultipleClientsFromFirstTagAndOperationId`, ADR-003) risks a
    /// non-semantic method name or a collision in the generated `AddressesClient` (Block 2) — same
    /// reasoning already documented in ADR-003 for `MuralsController`/`DiscoveryController`.
    /// </summary>
    [HttpGet("search", Name = "SearchAddresses")]
    [ProducesResponseType(typeof(SearchAddressesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SearchAddressesQuery { Q = q }, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Reverse geocoding (FR-04/AC-03). `Name = "ReverseGeocodeAddress"`, same reasoning as above.
    /// </summary>
    [HttpGet("reverse", Name = "ReverseGeocodeAddress")]
    [ProducesResponseType(typeof(ReverseGeocodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reverse([FromQuery] double lat, [FromQuery] double lng, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ReverseGeocodeQuery { Latitude = lat, Longitude = lng }, cancellationToken);
        return Ok(response);
    }
}
