namespace Paretto.Infrastructure.Geocoding;

/// <summary>
/// Abstraction over the external address geocoding provider (`direcciones.ide.uy`, see spec Block 1
/// FEAT-011 and docs/daw/security/threat-FEAT-011.md). Implementations must never propagate an
/// exception from the underlying HTTP call — any failure (network error, timeout, malformed
/// response) is reported as <see cref="AddressProviderOutcome.Unavailable"/>, same never-throwing
/// contract already established by <c>INsfwContentScanner</c>.
/// </summary>
public interface IAddressProviderClient
{
    Task<AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>> SearchAsync(string query, CancellationToken ct);

    Task<AddressProviderResult<AddressSuggestionDto?>> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct);
}

public enum AddressProviderOutcome
{
    Success,
    Unavailable,
}

/// <summary>
/// Outcome of a call to <see cref="IAddressProviderClient"/>. <see cref="Data"/> is only meaningful
/// when <see cref="Outcome"/> is <see cref="AddressProviderOutcome.Success"/> — a Handler must check
/// <see cref="Outcome"/> before reading it (see `SearchAddressesQueryHandler`/
/// `ReverseGeocodeQueryHandler`).
/// </summary>
public class AddressProviderResult<T>
{
    public required AddressProviderOutcome Outcome { get; init; }

    public T? Data { get; init; }
}

/// <summary>
/// A single geocoding result — reused both as the internal Infrastructure type and, directly, as
/// the item type of the API responses (`SearchAddressesResponse.Suggestions`,
/// `ReverseGeocodeResponse.Suggestion`), per spec Block 1 ("sin Mapster: son DTOs planos sin lógica
/// de dominio").
/// </summary>
public class AddressSuggestionDto
{
    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
