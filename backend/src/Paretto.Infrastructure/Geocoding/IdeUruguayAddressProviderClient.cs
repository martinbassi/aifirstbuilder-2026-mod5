using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Paretto.Infrastructure.Geocoding;

/// <summary>
/// HTTP client for the external, free, key-less geocoding provider `direcciones.ide.uy` (see spec
/// Block 1 FEAT-011). Registered as a dedicated typed client (`AddHttpClient&lt;IAddressProviderClient,
/// IdeUruguayAddressProviderClient&gt;`, Program.cs) — it shares no `DelegatingHandler` with the rest
/// of the API, so no session cookie/token can ever leak to this third party (threat model R4).
///
/// Never propagates an exception to its callers (`SearchAddressesQueryHandler`/
/// `ReverseGeocodeQueryHandler`): `HttpRequestException`, a timeout, or a deserialization failure are
/// all caught, logged as a Warning (never an empty catch, AGENTS.md) and reported as
/// <see cref="AddressProviderOutcome.Unavailable"/> instead — same never-throwing contract as
/// `NsfwSpyContentScanner`, though the mechanism differs: this is a real HTTP call, bounded by
/// `HttpClient.Timeout` (native), not a CPU-bound operation raced manually with `Task.WhenAny`.
/// </summary>
public class IdeUruguayAddressProviderClient : IAddressProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly ILogger<IdeUruguayAddressProviderClient> _logger;

    /// <summary>
    /// QUICK-FIX-002: `[ActivatorUtilitiesConstructor]` le dice explícitamente a `ActivatorUtilities`
    /// cuál de los dos constructores usar al activar este typed client (`AddHttpClient&lt;TClient,
    /// TImplementation&gt;`, Program.cs) — sin él, la activación tira `InvalidOperationException`
    /// ("Multiple constructors...") porque no desambigua entre este constructor y el de 3 args de
    /// abajo (ambos empiezan con `HttpClient`). `AddScoped` (usado por `NsfwSpyContentScanner`, con
    /// la misma forma de 2 constructores) no tiene este problema: su algoritmo de selección sí
    /// descarta constructores cuyos parámetros extra no están registrados en el contenedor —
    /// `AddHttpClient`, en cambio, activa con el `HttpClient` pasado explícitamente y no aplica ese
    /// descarte.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public IdeUruguayAddressProviderClient(HttpClient httpClient, ILogger<IdeUruguayAddressProviderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Overload exposing the request timeout directly on <paramref name="httpClient"/>.Timeout — the
    /// 2-arg constructor above leaves whatever value was already configured externally by
    /// `AddHttpClient` in Program.cs (5s in production) untouched; this overload overrides it so
    /// tests exercising the timeout path do not need to wait 5 real seconds, same trick as
    /// `NsfwSpyContentScanner`'s injectable `scanTimeout`. Must run before the client issues its
    /// first request — `HttpClient.Timeout` throws `InvalidOperationException` if set afterwards.
    /// </summary>
    public IdeUruguayAddressProviderClient(HttpClient httpClient, ILogger<IdeUruguayAddressProviderClient> logger, TimeSpan requestTimeout)
        : this(httpClient, logger)
    {
        _httpClient.Timeout = requestTimeout;
    }

    public async Task<AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>> SearchAsync(string query, CancellationToken ct)
    {
        try
        {
            // Uri.EscapeDataString, never raw string concatenation — the host is fixed by
            // configuration (HttpClient.BaseAddress, Program.cs), only `q` is interpolated here
            // (threat model R5, SSRF discarded by design).
            var requestUri = $"api/v1/geocode/candidates?q={Uri.EscapeDataString(query)}";
            var payload = await _httpClient.GetFromJsonAsync<List<IdeGeocodeResultWire>>(requestUri, JsonOptions, ct);

            IReadOnlyList<AddressSuggestionDto> candidates = payload?.Select(ToSuggestion).ToList() ?? [];
            return new AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>
            {
                Outcome = AddressProviderOutcome.Success,
                Data = candidates,
            };
        }
        catch (Exception ex)
        {
            // Never propagated to the caller (SearchAddressesQueryHandler) and never silently
            // discarded — same criterion as NsfwSpyContentScanner: covers HttpRequestException,
            // TaskCanceledException (timeout) and any deserialization error uniformly.
            _logger.LogWarning(ex, "Address provider search failed or timed out; treating it as unavailable.");
            return new AddressProviderResult<IReadOnlyList<AddressSuggestionDto>> { Outcome = AddressProviderOutcome.Unavailable };
        }
    }

    public async Task<AddressProviderResult<AddressSuggestionDto?>> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct)
    {
        try
        {
            var requestUri =
                $"api/v1/geocode/reverse?latitud={Uri.EscapeDataString(latitude.ToString(CultureInfo.InvariantCulture))}" +
                $"&longitud={Uri.EscapeDataString(longitude.ToString(CultureInfo.InvariantCulture))}";
            var payload = await _httpClient.GetFromJsonAsync<List<IdeGeocodeResultWire>>(requestUri, JsonOptions, ct);

            // El proveedor responde con un array en la raíz también para reverse geocoding (no un
            // objeto único) — se asume ordenado por relevancia/cercanía, "la" dirección resuelta es
            // el primer elemento; ausencia de resultados es null dentro de Success, no Unavailable.
            var suggestion = payload is { Count: > 0 } ? ToSuggestion(payload[0]) : null;

            return new AddressProviderResult<AddressSuggestionDto?>
            {
                Outcome = AddressProviderOutcome.Success,
                Data = suggestion,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Address provider reverse geocoding failed or timed out; treating it as unavailable.");
            return new AddressProviderResult<AddressSuggestionDto?> { Outcome = AddressProviderOutcome.Unavailable };
        }
    }

    private static AddressSuggestionDto ToSuggestion(IdeGeocodeResultWire wire) => new()
    {
        Address = wire.Address,
        Latitude = wire.Lat,
        Longitude = wire.Lng,
    };

    /// <summary>
    /// Wire shape of the provider's real response — verified live against `direcciones.ide.uy` for
    /// both `/api/v1/geocode/candidates` and `/api/v1/geocode/reverse`: a JSON array at the root
    /// (never a wrapper object), each element carrying lowercase `address`/`lat`/`lng`. Kept private
    /// and mapped explicitly to the public <see cref="AddressSuggestionDto"/> so that contract stays
    /// clean and decoupled from the provider's raw shape — nothing outside this class depends on
    /// this type.
    /// </summary>
    private sealed class IdeGeocodeResultWire
    {
        public string Address { get; set; } = string.Empty;

        public double Lat { get; set; }

        public double Lng { get; set; }
    }
}
