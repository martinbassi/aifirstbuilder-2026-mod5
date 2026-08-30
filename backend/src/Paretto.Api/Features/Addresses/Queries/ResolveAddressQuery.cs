using FluentValidation;
using MediatR;
using Paretto.Infrastructure.Geocoding;

namespace Paretto.Api.Features.Addresses.Queries;

/// <summary>
/// FIX-005: resolves the real coordinates of a `CALLEyPORTAL` (street+number) result that
/// `SearchAddressesQuery` returned with `Latitude`/`Longitude` at 0 — `/candidates` never resolves
/// those, `/find` does (see docs/daw/specs/rca-FIX-005.md). Frontend calls this only when it detects
/// the selected suggestion needs it, passing back the raw provider fields that suggestion already
/// carried.
/// </summary>
public class ResolveAddressQuery : IRequest<ResolveAddressResponse>
{
    public int StreetId { get; set; }

    public int PortalNumber { get; set; }

    public string Locality { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;
}

public class ResolveAddressResponse
{
    /// <summary>
    /// `null` when the provider cannot resolve this result either — not an error (see
    /// `ResolveAddressQueryHandler`), same criterion as `ReverseGeocodeResponse.Suggestion`.
    /// </summary>
    public AddressSuggestionDto? Suggestion { get; set; }
}

public class ResolveAddressQueryValidator : AbstractValidator<ResolveAddressQuery>
{
    public ResolveAddressQueryValidator()
    {
        RuleFor(x => x.StreetId).GreaterThan(0);
        RuleFor(x => x.PortalNumber).GreaterThan(0);
        RuleFor(x => x.Locality).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
    }
}

public class ResolveAddressQueryHandler : IRequestHandler<ResolveAddressQuery, ResolveAddressResponse>
{
    private readonly IAddressProviderClient _addressProviderClient;

    public ResolveAddressQueryHandler(IAddressProviderClient addressProviderClient)
    {
        _addressProviderClient = addressProviderClient;
    }

    public async Task<ResolveAddressResponse> Handle(ResolveAddressQuery request, CancellationToken cancellationToken)
    {
        var result = await _addressProviderClient.ResolveAsync(
            request.StreetId, request.PortalNumber, request.Locality, request.Type, cancellationToken);

        if (result.Outcome == AddressProviderOutcome.Unavailable)
        {
            throw new AddressProviderUnavailableException();
        }

        return new ResolveAddressResponse
        {
            Suggestion = result.Data,
        };
    }
}
