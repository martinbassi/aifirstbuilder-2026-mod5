namespace Paretto.Infrastructure.Geocoding;

/// <summary>
/// Abstraction over the external address geocoding provider (`direcciones.ide.uy`, see spec Block 1
/// FEAT-011, spec FIX-005 and docs/daw/security/threat-FEAT-011.md +
/// docs/daw/security/threat-FIX-005.md). Implementations must never propagate an exception from the
/// underlying HTTP call — any failure (network error, timeout, malformed response) is reported as
/// <see cref="AddressProviderOutcome.Unavailable"/>, same never-throwing contract already
/// established by <c>INsfwContentScanner</c>.
/// </summary>
public interface IAddressProviderClient
{
    Task<AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>> SearchAsync(string query, CancellationToken ct);

    Task<AddressProviderResult<AddressSuggestionDto?>> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct);

    /// <summary>
    /// FIX-005: resolves the real coordinates of a specific `CALLEyPORTAL` (street+number) result
    /// that `SearchAsync` returned with `Latitude`/`Longitude` at 0 — `/candidates` never resolves
    /// coordinates for that result type, but `/find` does, given the street/portal/locality/type it
    /// already reported. `Data` is `null` (inside `Success`) when the provider cannot resolve it
    /// either — same "no match is not an error" criterion as `ReverseGeocodeAsync`.
    /// </summary>
    Task<AddressProviderResult<AddressSuggestionDto?>> ResolveAsync(int streetId, int portalNumber, string locality, string type, CancellationToken ct);
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
/// `ReverseGeocodeResponse.Suggestion`, `ResolveAddressResponse.Suggestion`), per spec Block 1
/// ("sin Mapster: son DTOs planos sin lógica de dominio"). <see cref="StreetId"/>/
/// <see cref="Locality"/>/<see cref="PortalNumber"/>/<see cref="Type"/> (spec FIX-005) are the raw
/// provider fields a `CALLEyPORTAL` result needs to resolve its real coordinates later via
/// <see cref="IAddressProviderClient.ResolveAsync"/> — exposed publicly too (decision confirmed in
/// PLAN, non-sensitive geocoding metadata) rather than splitting a separate internal type.
/// </summary>
public class AddressSuggestionDto
{
    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int StreetId { get; set; }

    public string Locality { get; set; } = string.Empty;

    public int PortalNumber { get; set; }

    public string Type { get; set; } = string.Empty;
}
