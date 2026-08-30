using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Paretto.Infrastructure.Geocoding;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (FEAT-011) — <see cref="IdeUruguayAddressProviderClient"/>. Mismo patrón que
/// <see cref="NsfwSpyContentScannerTests"/>: sin mocking framework, un
/// <see cref="HttpMessageHandler"/> fake de mano y un <see cref="ILogger{T}"/> que graba sus
/// invocaciones. Nunca golpea el proveedor externo real.
/// </summary>
public class IdeUruguayAddressProviderClientTests
{
    private static HttpClient BuildHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://addresses.example.test") };

    [Fact]
    public async Task HttpRequestException_from_the_handler_returns_unavailable_and_never_propagates()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("simulated network failure"));
        var httpClient = BuildHttpClient(handler);
        var logger = new RecordingLogger<IdeUruguayAddressProviderClient>();
        var client = new IdeUruguayAddressProviderClient(httpClient, logger);

        var result = await client.SearchAsync("18 de Julio", CancellationToken.None);

        Assert.Equal(AddressProviderOutcome.Unavailable, result.Outcome);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public async Task A_response_slower_than_the_configured_timeout_returns_unavailable_without_hanging()
    {
        // Handler que nunca completa (ignora la cancelación de forma deliberada en su primer await
        // artificial) — exercises el race contra el timeout inyectable, mismo truco que
        // NsfwSpyContentScannerTests con scanTimeout: no hace falta esperar 5s reales.
        var handler = new FakeHttpMessageHandler(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = BuildHttpClient(handler);
        var logger = new RecordingLogger<IdeUruguayAddressProviderClient>();
        var client = new IdeUruguayAddressProviderClient(httpClient, logger, requestTimeout: TimeSpan.FromMilliseconds(100));

        var searchTask = client.SearchAsync("18 de Julio", CancellationToken.None);
        var completed = await Task.WhenAny(searchTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(searchTask, completed);
        var result = await searchTask;
        Assert.Equal(AddressProviderOutcome.Unavailable, result.Outcome);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public async Task A_valid_200_response_returns_success_with_the_deserialized_data()
    {
        // Shape real verificado contra direcciones.ide.uy: un array JSON en la raíz, campos
        // "address"/"lat"/"lng" en minúsculas — NUNCA un objeto envoltorio { "candidates": [...] }
        // ni "latitude"/"longitude".
        const string json = """
            [ { "address": "Bulevar Artigas 1234, Montevideo", "lat": -34.9011, "lng": -56.1645 } ]
            """;
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(json)));
        var httpClient = BuildHttpClient(handler);
        var logger = new RecordingLogger<IdeUruguayAddressProviderClient>();
        var client = new IdeUruguayAddressProviderClient(httpClient, logger);

        var result = await client.SearchAsync("Bulevar Artigas", CancellationToken.None);

        Assert.Equal(AddressProviderOutcome.Success, result.Outcome);
        var suggestion = Assert.Single(result.Data!);
        Assert.Equal("Bulevar Artigas 1234, Montevideo", suggestion.Address);
        Assert.Equal(-34.9011, suggestion.Latitude);
        Assert.Equal(-56.1645, suggestion.Longitude);
    }

    [Fact]
    public async Task ReverseGeocode_with_a_valid_200_response_returns_the_first_element_of_the_array()
    {
        // El proveedor real también responde con un array en la raíz para /geocode/reverse (no un
        // objeto único) — "la" dirección resuelta es el primer elemento cuando hay varios.
        const string json = """
            [
                { "address": "Bulevar Artigas 1234, Montevideo", "lat": -34.9011, "lng": -56.1645 },
                { "address": "Bulevar Artigas 1200, Montevideo", "lat": -34.9012, "lng": -56.1646 }
            ]
            """;
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(json)));
        var httpClient = BuildHttpClient(handler);
        var logger = new RecordingLogger<IdeUruguayAddressProviderClient>();
        var client = new IdeUruguayAddressProviderClient(httpClient, logger);

        var result = await client.ReverseGeocodeAsync(-34.9011, -56.1645, CancellationToken.None);

        Assert.Equal(AddressProviderOutcome.Success, result.Outcome);
        Assert.NotNull(result.Data);
        Assert.Equal("Bulevar Artigas 1234, Montevideo", result.Data!.Address);
        Assert.Equal(-34.9011, result.Data!.Latitude);
        Assert.Equal(-56.1645, result.Data!.Longitude);
    }

    [Fact]
    public async Task ReverseGeocode_with_an_empty_array_returns_success_with_null_data()
    {
        const string json = "[]";
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(json)));
        var httpClient = BuildHttpClient(handler);
        var logger = new RecordingLogger<IdeUruguayAddressProviderClient>();
        var client = new IdeUruguayAddressProviderClient(httpClient, logger);

        var result = await client.ReverseGeocodeAsync(-34.9011, -56.1645, CancellationToken.None);

        Assert.Equal(AddressProviderOutcome.Success, result.Outcome);
        Assert.Null(result.Data);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        public FakeHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _respond(cancellationToken);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }
}
