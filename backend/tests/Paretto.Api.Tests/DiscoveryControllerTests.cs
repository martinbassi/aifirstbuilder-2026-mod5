using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 3 (DiscoveryController + rate limiting específico) de FEAT-001d — GET
/// /api/discovery/nearby-murals.
///
/// A diferencia de GetNearbyMuralsTests (Block 2), que invoca el Handler directo porque el
/// Controller todavía no existía, estos tests son HTTP reales vía WebApplicationFactory + HttpClient
/// (mismo patrón que GetMuralByIdTests/GetPendingMuralsTests), porque acá lo que se prueba es
/// justamente la exposición HTTP (ausencia de auth, rate limiting).
/// </summary>
public class DiscoveryControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public DiscoveryControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    // Obelisco, Buenos Aires — mismo punto de referencia usado en GetNearbyMuralsTests (Block 2).
    private const double OriginLat = -34.6037;
    private const double OriginLon = -58.3816;

    private WebApplicationFactory<Program> CreateFactory(string dbName)
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

                services.RemoveAll<IBlobStorageService>();
                services.AddScoped<IBlobStorageService>(_ => new FakeBlobStorageService());
            });
        });
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for DiscoveryController tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    private static async Task SeedPublishedMuralAsync(WebApplicationFactory<Program> factory, double latitude, double longitude)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mural = new Mural
        {
            UserId = Guid.NewGuid(),
            PhotoBlobName = $"{Guid.NewGuid()}.jpg",
            Location = Mural.CreateLocation(latitude, longitude),
            Status = MuralStatus.Published,
        };
        db.Murals.Add(mural);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Request_without_an_auth_header_returns_200_not_401()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        await SeedPublishedMuralAsync(factory, OriginLat, OriginLon);

        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/discovery/nearby-murals?lat={OriginLat}&lng={OriginLon}&radiusKm=5");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");
    }

    [Fact]
    public async Task The_21st_request_in_one_minute_from_the_same_IP_returns_429()
    {
        // TestServer does not populate `HttpContext.Connection.RemoteIpAddress` by default, so every
        // request made through the same WebApplicationFactory instance falls into the same
        // "discovery" rate-limiting partition (`?? "unknown"` in the policy) regardless of how many
        // HttpClient instances issue them — equivalent, for this policy, to "the same IP". A fresh
        // `CreateFactory` per test keeps the limiter's state isolated between tests (it lives in that
        // factory's own DI container as configured by `AddRateLimiter`, not shared globally).
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync($"/api/discovery/nearby-murals?lat={OriginLat}&lng={OriginLon}&radiusKm=5");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var rejectedResponse = await client.GetAsync($"/api/discovery/nearby-murals?lat={OriginLat}&lng={OriginLon}&radiusKm=5");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    // FEAT-009 (Block 3): valida end-to-end (vía el endpoint HTTP real, no Mural.CreateLocation en
    // aislamiento) que DiscoveryMappingConfig sigue mapeando Latitude/Longitude en el eje correcto
    // sin haber tocado ese archivo. Usa coordenadas claramente distinguibles (|lat| != |lng|) para
    // que un swap de ejes accidental en cualquier punto del pipeline (Mural.Location -> Mapster ->
    // JSON) se detecte de forma inequívoca — mitigación final de threat model R2.
    [Fact]
    public async Task Nearby_mural_response_exposes_latitude_and_longitude_in_the_correct_field_not_swapped()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        await SeedPublishedMuralAsync(factory, OriginLat, OriginLon);

        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/discovery/nearby-murals?lat={OriginLat}&lng={OriginLon}&radiusKm=5");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var items = JsonDocument.Parse(raw).RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        Assert.Equal(OriginLat, items[0].GetProperty("latitude").GetDouble());
        Assert.Equal(OriginLon, items[0].GetProperty("longitude").GetDouble());
    }

    // FIX-003: GetNearbyMuralsTests (Block 2) invoca el Handler directo, sin pasar por
    // serialización JSON — este es el único punto donde el formato real de fecha que devuelve
    // GET /api/discovery/nearby-murals puede verificarse. JsonDateTimeUtcConverter estaba
    // registrado en el JsonOptions equivocado y nunca aplicaba a esta respuesta — ver
    // docs/daw/specs/rca-FIX-003.md, causa raíz #2.
    [Fact]
    public async Task CreatedAt_is_serialized_with_the_full_utc_format()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        await SeedPublishedMuralAsync(factory, OriginLat, OriginLon);

        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/discovery/nearby-murals?lat={OriginLat}&lng={OriginLon}&radiusKm=5");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {response.StatusCode}: {raw}");

        var items = JsonDocument.Parse(raw).RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        var createdAt = items[0].GetProperty("createdAt").GetString();
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", createdAt);
    }
}
