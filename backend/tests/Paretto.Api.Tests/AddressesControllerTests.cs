using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Geocoding;
using Paretto.Infrastructure.Security;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (FEAT-011) — GET /api/addresses/search, GET /api/addresses/reverse.
///
/// Mismo patrón que DiscoveryControllerTests/CreateMuralTests: integración HTTP completa vía
/// WebApplicationFactory, AppDbContext reemplazado por el proveedor InMemory de EF Core por test.
/// IAddressProviderClient se reemplaza por FakeAddressProviderClient (fake de mano, mismo patrón
/// que FakeBlobStorageService) para dictar determinísticamente Success/Unavailable sin golpear el
/// proveedor externo real.
/// </summary>
public class AddressesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public AddressesControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private WebApplicationFactory<Program> CreateFactory(string dbName, IAddressProviderClient? addressProviderClient = null)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var appDbContextDescriptors = services
                    .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext)))
                    .ToList();
                foreach (var descriptor in appDbContextDescriptors)
                {
                    services.Remove(descriptor);
                }
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));

                if (addressProviderClient is not null)
                {
                    services.RemoveAll<IAddressProviderClient>();
                    services.AddScoped(_ => addressProviderClient);
                }
            });
        });
    }

    private sealed class FakeAddressProviderClient : IAddressProviderClient
    {
        private readonly AddressProviderOutcome _outcome;
        private readonly IReadOnlyList<AddressSuggestionDto> _searchData;
        private readonly AddressSuggestionDto? _reverseData;
        private readonly AddressSuggestionDto? _resolveData;

        public FakeAddressProviderClient(
            AddressProviderOutcome outcome = AddressProviderOutcome.Success,
            IReadOnlyList<AddressSuggestionDto>? searchData = null,
            AddressSuggestionDto? reverseData = null,
            AddressSuggestionDto? resolveData = null)
        {
            _outcome = outcome;
            _searchData = searchData ?? [];
            _reverseData = reverseData;
            _resolveData = resolveData;
        }

        public Task<AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>> SearchAsync(string query, CancellationToken ct) =>
            Task.FromResult(new AddressProviderResult<IReadOnlyList<AddressSuggestionDto>>
            {
                Outcome = _outcome,
                Data = _searchData,
            });

        public Task<AddressProviderResult<AddressSuggestionDto?>> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct) =>
            Task.FromResult(new AddressProviderResult<AddressSuggestionDto?>
            {
                Outcome = _outcome,
                Data = _reverseData,
            });

        public Task<AddressProviderResult<AddressSuggestionDto?>> ResolveAsync(int streetId, int portalNumber, string locality, string type, CancellationToken ct) =>
            Task.FromResult(new AddressProviderResult<AddressSuggestionDto?>
            {
                Outcome = _outcome,
                Data = _resolveData,
            });
    }

    private static async Task<(string Username, string Password)> SeedUserAsync(WebApplicationFactory<Program> factory)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var username = $"dilux-{suffix}";
        const string password = "Sup3rSecret!";

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Standard,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (username, password);
    }

    private static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var loginRaw = await loginResponse.Content.ReadAsStringAsync();
        Assert.True(loginResponse.StatusCode == HttpStatusCode.OK, $"Login prerequisite failed: {loginResponse.StatusCode}: {loginRaw}");
        var token = JsonDocument.Parse(loginRaw).RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private static async Task<HttpClient> AuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        var (username, password) = await SeedUserAsync(factory);
        var client = factory.CreateClient();
        var token = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Search_with_a_valid_query_and_a_provider_with_results_returns_200_with_a_non_empty_list()
    {
        var suggestions = new List<AddressSuggestionDto>
        {
            new() { Address = "Bulevar Artigas 1234, Montevideo", Latitude = -34.9011, Longitude = -56.1645 },
        };
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(searchData: suggestions));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/search?q=Bulevar+Artigas");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
        var items = JsonDocument.Parse(raw).RootElement.GetProperty("suggestions");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("Bulevar Artigas 1234, Montevideo", items[0].GetProperty("address").GetString());
    }

    [Fact]
    public async Task Search_without_matches_returns_200_with_an_empty_list()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(searchData: []));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/search?q=xyzxyzxyz");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
        var items = JsonDocument.Parse(raw).RootElement.GetProperty("suggestions");
        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public async Task Search_with_the_provider_unavailable_returns_503_never_500()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(outcome: AddressProviderOutcome.Unavailable));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/search?q=Bulevar+Artigas");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Search_with_an_empty_q_returns_400()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/search?q=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_without_a_session_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/addresses/search?q=Bulevar+Artigas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reverse_with_valid_coordinates_and_a_provider_with_a_result_returns_200_with_the_suggestion()
    {
        var suggestion = new AddressSuggestionDto { Address = "Bulevar Artigas 1234, Montevideo", Latitude = -34.9011, Longitude = -56.1645 };
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(reverseData: suggestion));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/reverse?lat=-34.9011&lng=-56.1645");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
        var body = JsonDocument.Parse(raw).RootElement.GetProperty("suggestion");
        Assert.Equal("Bulevar Artigas 1234, Montevideo", body.GetProperty("address").GetString());
    }

    [Fact]
    public async Task Reverse_with_valid_coordinates_but_no_match_returns_200_with_a_null_suggestion_not_an_error()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(reverseData: null));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/reverse?lat=-34.9011&lng=-56.1645");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
        // Program.cs sets DefaultIgnoreCondition = WhenWritingNull globally (same convention as
        // every other response in this API) — a null Suggestion is therefore omitted from the JSON
        // altogether rather than serialized as an explicit `"suggestion": null` key, so "no
        // suggestion" is checked as "the key is absent", not as a literal JSON null token.
        var hasSuggestion = JsonDocument.Parse(raw).RootElement.TryGetProperty("suggestion", out var suggestion);
        Assert.False(hasSuggestion && suggestion.ValueKind != JsonValueKind.Null, $"Expected no suggestion, got: {raw}");
    }

    [Fact]
    public async Task Reverse_with_the_provider_unavailable_returns_503()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(outcome: AddressProviderOutcome.Unavailable));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/reverse?lat=-34.9011&lng=-56.1645");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task Reverse_with_lat_or_lng_out_of_range_is_rejected(double lat, double lng)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync($"/api/addresses/reverse?lat={lat}&lng={lng}");

        // NOTA (assumption documentada en el reporte del bloque): el spec (tabla de errores + lista
        // de tests requeridos) escribe "400" para este caso, pero el mismo InclusiveBetween de
        // FluentValidation ya usado por CreateMuralCommandValidator (ver
        // Coordinates_outside_the_valid_range_are_rejected_with_422 en CreateMuralTests.cs) resuelve,
        // vía el pipeline compartido (ValidationBehavior -> FluentValidation.ValidationException ->
        // ExceptionHandlingMiddleware), en 422 — no en 400. Mantener el mismo código que el resto del
        // proyecto para el mismo tipo de regla de validación, en vez de crear una excepción de un solo
        // endpoint, es la lectura más consistente con AGENTS.md ("Input validation lives in
        // FluentValidation") y con el propio texto del spec ("400 ... vía el pipeline de
        // FluentValidation existente" — ese pipeline ya devuelve 422 en todo el resto del proyecto).
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task The_21st_request_in_one_minute_from_the_same_IP_against_search_returns_429()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = await AuthenticatedClientAsync(factory);

        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/api/addresses/search?q=Bulevar+Artigas");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var rejectedResponse = await client.GetAsync("/api/addresses/search?q=Bulevar+Artigas");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    // FIX-005 — GET /api/addresses/resolve: resuelve coordenadas reales de un CALLEyPORTAL que
    // /search devolvió en 0,0.
    [Fact]
    public async Task Resolve_with_valid_params_and_a_provider_with_a_result_returns_200_with_real_coordinates()
    {
        var suggestion = new AddressSuggestionDto { Address = "Bulevar General Artigas 1234, Montevideo", Latitude = -34.9059, Longitude = -56.1639 };
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(resolveData: suggestion));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/resolve?streetId=8143&portal=1234&locality=MONTEVIDEO&type=CALLEyPORTAL");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
        var body = JsonDocument.Parse(raw).RootElement.GetProperty("suggestion");
        Assert.Equal(-34.9059, body.GetProperty("latitude").GetDouble());
        Assert.Equal(-56.1639, body.GetProperty("longitude").GetDouble());
    }

    [Fact]
    public async Task Resolve_with_no_match_returns_200_with_a_null_suggestion_not_an_error()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(resolveData: null));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/resolve?streetId=8143&portal=1234&locality=MONTEVIDEO&type=CALLEyPORTAL");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
        var hasSuggestion = JsonDocument.Parse(raw).RootElement.TryGetProperty("suggestion", out var suggestion);
        Assert.False(hasSuggestion && suggestion.ValueKind != JsonValueKind.Null, $"Expected no suggestion, got: {raw}");
    }

    [Fact]
    public async Task Resolve_with_the_provider_unavailable_returns_503()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient(outcome: AddressProviderOutcome.Unavailable));
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync("/api/addresses/resolve?streetId=8143&portal=1234&locality=MONTEVIDEO&type=CALLEyPORTAL");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_without_a_session_returns_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/addresses/resolve?streetId=8143&portal=1234&locality=MONTEVIDEO&type=CALLEyPORTAL");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 1234, "MONTEVIDEO", "CALLEyPORTAL")]
    [InlineData(8143, 0, "MONTEVIDEO", "CALLEyPORTAL")]
    public async Task Resolve_with_an_invalid_numeric_param_is_rejected_with_422(int streetId, int portal, string locality, string type)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync(
            $"/api/addresses/resolve?streetId={streetId}&portal={portal}&locality={locality}&type={type}");

        // Mismo criterio que Reverse_with_lat_or_lng_out_of_range_is_rejected: el pipeline
        // compartido de FluentValidation resuelve en 422, no en 400.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("", "CALLEyPORTAL")]
    [InlineData("MONTEVIDEO", "")]
    public async Task Resolve_with_an_empty_string_param_is_rejected_with_400(string locality, string type)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), new FakeAddressProviderClient());
        var client = await AuthenticatedClientAsync(factory);

        var response = await client.GetAsync(
            $"/api/addresses/resolve?streetId=8143&portal=1234&locality={locality}&type={type}");

        // Mismo criterio que Search_with_an_empty_q_returns_400: un string no-nullable vacío en
        // query params dispara la validación automática de [ApiController] (nullable reference
        // types habilitado en el .csproj) ANTES de llegar al pipeline de FluentValidation — nunca
        // llega al Validator, así que nunca puede ser 422.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
