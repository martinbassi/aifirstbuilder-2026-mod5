using FluentValidation;
using MediatR;
using Paretto.Infrastructure.Geocoding;

namespace Paretto.Api.Features.Addresses.Queries;

/// <summary>
/// Reverse geocoding against the external address provider — precomputes a human-readable address
/// from GPS coordinates (FR-04/AC-03, spec Block 1 FEAT-011).
/// </summary>
public class ReverseGeocodeQuery : IRequest<ReverseGeocodeResponse>
{
    public double Latitude { get; set; }

    public double Longitude { get; set; }
}

public class ReverseGeocodeResponse
{
    /// <summary>
    /// `null` when the provider has no match for the given coordinates — not an error (see
    /// `ReverseGeocodeQueryHandler`).
    /// </summary>
    public AddressSuggestionDto? Suggestion { get; set; }
}

public class ReverseGeocodeQueryValidator : AbstractValidator<ReverseGeocodeQuery>
{
    public ReverseGeocodeQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
    }
}

public class ReverseGeocodeQueryHandler : IRequestHandler<ReverseGeocodeQuery, ReverseGeocodeResponse>
{
    private readonly IAddressProviderClient _addressProviderClient;

    public ReverseGeocodeQueryHandler(IAddressProviderClient addressProviderClient)
    {
        _addressProviderClient = addressProviderClient;
    }

    public async Task<ReverseGeocodeResponse> Handle(ReverseGeocodeQuery request, CancellationToken cancellationToken)
    {
        var result = await _addressProviderClient.ReverseGeocodeAsync(request.Latitude, request.Longitude, cancellationToken);

        if (result.Outcome == AddressProviderOutcome.Unavailable)
        {
            throw new AddressProviderUnavailableException();
        }

        return new ReverseGeocodeResponse
        {
            Suggestion = result.Data,
        };
    }
}
