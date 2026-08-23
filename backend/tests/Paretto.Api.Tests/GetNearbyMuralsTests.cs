using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Api.Features.Discovery.Queries;
using Paretto.Domain.Entities;
using Paretto.Domain.Enums;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 2 (GetNearbyMuralsQuery, Features/Discovery) de FEAT-001d.
///
/// A diferencia de GetMuralByIdTests/GetPendingMuralsTests, este bloque todavía no expone el
/// endpoint HTTP (DiscoveryController es Block 3, fuera de este alcance) — los tests resuelven
/// IMediator desde el contenedor de WebApplicationFactory y llaman Send(query) directamente. Esto
/// sigue ejercitando el pipeline completo de MediatR (incluido ValidationBehavior/FluentValidation),
/// solo que sin la traducción HTTP a 422 que hace ExceptionHandlingMiddleware (esa traducción ya
/// está probada por los tests HTTP de otras features y no cambia en este bloque).
/// </summary>
public class GetNearbyMuralsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public GetNearbyMuralsTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    // Obelisco, Buenos Aires — punto de referencia usado en todo el bloque.
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
            throw new NotSupportedException("Not needed for GetNearbyMurals tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    private static async Task<Guid> SeedMuralAsync(
        WebApplicationFactory<Program> factory,
        double latitude,
        double longitude,
        MuralStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mural = new Mural
        {
            UserId = Guid.NewGuid(),
            PhotoBlobName = $"{Guid.NewGuid()}.jpg",
            Latitude = latitude,
            Longitude = longitude,
            Status = status,
        };
        db.Murals.Add(mural);
        await db.SaveChangesAsync();

        return mural.Id;
    }

    // Desplaza `originLat` en `km` kilómetros (aprox., 1 grado ~ 111 km) — suficiente para construir
    // puntos claramente dentro/fuera de un radio dado sin depender de la implementación bajo prueba.
    private static double LatOffsetKm(double originLat, double km) => originLat + (km / 111.0);

    [Fact]
    public async Task Returns_only_Published_murals_within_radius_excluding_out_of_radius_and_non_Published_inside_radius()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var insidePublishedId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Published);
        var insidePendingId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1.5), OriginLon, MuralStatus.Pending);
        var insideRejectedId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 2), OriginLon, MuralStatus.Rejected);
        var outsidePublishedId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 20), OriginLon, MuralStatus.Published);

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
            RadiusKm = 5.0,
        });

        var ids = response.Items.Select(i => i.Id).ToList();
        Assert.Contains(insidePublishedId, ids);
        Assert.DoesNotContain(insidePendingId, ids);
        Assert.DoesNotContain(insideRejectedId, ids);
        Assert.DoesNotContain(outsidePublishedId, ids);
        Assert.Single(ids);
    }

    [Fact]
    public async Task Results_are_ordered_ascending_by_DistanceKm()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var farId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 4), OriginLon, MuralStatus.Published);
        var nearId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Published);
        var midId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 2), OriginLon, MuralStatus.Published);

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
            RadiusKm = 5.0,
        });

        Assert.Equal(new[] { nearId, midId, farId }, response.Items.Select(i => i.Id).ToArray());
        var distances = response.Items.Select(i => i.DistanceKm).ToArray();
        Assert.True(distances.SequenceEqual(distances.OrderBy(d => d)));
    }

    [Fact]
    public async Task Radius_not_specified_defaults_to_5_km()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var withinDefaultId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 4.5), OriginLon, MuralStatus.Published);
        var beyondDefaultId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 6), OriginLon, MuralStatus.Published);

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
            RadiusKm = null,
        });

        var ids = response.Items.Select(i => i.Id).ToList();
        Assert.Contains(withinDefaultId, ids);
        Assert.DoesNotContain(beyondDefaultId, ids);
    }

    [Fact]
    public async Task No_Published_murals_within_radius_returns_empty_items_without_error()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Pending);

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
            RadiusKm = 5.0,
        });

        Assert.NotNull(response.Items);
        Assert.Empty(response.Items);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(50.1)]
    public async Task RadiusKm_out_of_range_throws_validation_exception(double radiusKm)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
            RadiusKm = radiusKm,
        }));
    }

    [Theory]
    [InlineData(90.1, 0.0)]
    [InlineData(-90.1, 0.0)]
    [InlineData(0.0, 180.1)]
    [InlineData(0.0, -180.1)]
    public async Task Latitude_or_longitude_out_of_range_throws_validation_exception(double lat, double lon)
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ValidationException>(() => mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = lat,
            Longitude = lon,
            RadiusKm = 5.0,
        }));
    }

    [Fact]
    public async Task PhotoUrl_is_a_valid_SAS_url()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Published);

        using var scope = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new GetNearbyMuralsQuery
        {
            Latitude = OriginLat,
            Longitude = OriginLon,
            RadiusKm = 5.0,
        });

        Assert.Single(response.Items);
        Assert.False(string.IsNullOrWhiteSpace(response.Items[0].PhotoUrl));
        Assert.Contains("sas=fake", response.Items[0].PhotoUrl);
    }
}
