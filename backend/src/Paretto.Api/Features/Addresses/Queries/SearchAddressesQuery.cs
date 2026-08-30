using FluentValidation;
using MediatR;
using Paretto.Infrastructure.Geocoding;

namespace Paretto.Api.Features.Addresses.Queries;

/// <summary>
/// Autocomplete search against the external address provider (FR-04/FR-19, spec Block 1 FEAT-011).
/// </summary>
public class SearchAddressesQuery : IRequest<SearchAddressesResponse>
{
    public string Q { get; set; } = string.Empty;
}

public class SearchAddressesResponse
{
    /// <summary>
    /// May be an empty list — no matches is not an error (AC-18), only
    /// `AddressProviderOutcome.Unavailable` is (see `SearchAddressesQueryHandler`).
    /// </summary>
    public List<AddressSuggestionDto> Suggestions { get; set; } = [];
}

public class SearchAddressesQueryValidator : AbstractValidator<SearchAddressesQuery>
{
    public SearchAddressesQueryValidator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(200);
    }
}

public class SearchAddressesQueryHandler : IRequestHandler<SearchAddressesQuery, SearchAddressesResponse>
{
    private readonly IAddressProviderClient _addressProviderClient;

    public SearchAddressesQueryHandler(IAddressProviderClient addressProviderClient)
    {
        _addressProviderClient = addressProviderClient;
    }

    public async Task<SearchAddressesResponse> Handle(SearchAddressesQuery request, CancellationToken cancellationToken)
    {
        var result = await _addressProviderClient.SearchAsync(request.Q, cancellationToken);

        if (result.Outcome == AddressProviderOutcome.Unavailable)
        {
            throw new AddressProviderUnavailableException();
        }

        return new SearchAddressesResponse
        {
            Suggestions = result.Data?.ToList() ?? [],
        };
    }
}
