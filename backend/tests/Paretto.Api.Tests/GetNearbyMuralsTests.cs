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
/// Block 2 (GetNearbyMuralsQuery, Features/Discovery) de FEAT-009.
///
/// FEAT-009 reemplaza el bounding box + Haversine en memoria por una consulta espacial vía
/// LINQ-to-Entities (<c>Location.Distance(searchPoint)</c>), que EF Core traduce a <c>STDistance</c>
/// de SQL Server (metros) SOLO cuando el proveedor activo es realmente SqlServer. El proveedor
/// InMemory que usaba este archivo antes de FEAT-009 no traduce esa expresión: ejecuta
/// <c>Point.Distance()</c> de NetTopologySuite tal cual, que devuelve una distancia euclidiana plana
/// en grados (no en metros) — rompería en silencio tanto el filtro de radio (los grados nunca superan
/// <c>radiusKm * 1000</c>, así que el `Where` dejaría de filtrar nada) como el test de conversión
/// metros→km que exige el spec. Por eso este archivo corre contra una instancia real de SQL Server
/// 2025, con el mismo mecanismo que MuralPersistenceTests.cs/AuthPersistenceTests.cs: lee
/// `ConnectionStrings__DefaultConnection` del entorno y falla ruidosamente si no está seteada (no se
/// salta ni cae a otro motor). Documentado como supuesto en el reporte de este bloque — el spec no
/// especifica el mecanismo de aislamiento de este archivo de test, solo que la conversión metros→km
/// se valide con una distancia conocida.
///
/// Aislamiento de datos (testing.instructions.md, Regla #0): cada test siembra su propio `User` y su
/// propio `Mural` (con `Username`/`Email`/`PhotoBlobName` sufijados con GUID) y borra exactamente lo
/// que creó en un `finally`, sin tocar ninguna fila que no haya creado.
/// </summary>
public class GetNearbyMuralsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public GetNearbyMuralsTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    // Obelisco, Buenos Aires — punto de referencia usado en todo el bloque; lat != lon claramente
    // distinguibles (mitigación de threat model R2: un swap de ejes X/Y rompería un test que use un
    // punto donde lat ≈ lon).
    private const double OriginLat = -34.6037;
    private const double OriginLon = -58.3816;

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection to run GetNearbyMurals tests against a real SQL Server instance.");
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var factory = _baseFactory.WithWebHostBuilder(builder =>
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
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(
                        GetConnectionString(),
                        sqlServerOptions => sqlServerOptions.UseNetTopologySuite()));

                services.RemoveAll<IBlobStorageService>();
                services.AddScoped<IBlobStorageService>(_ => new FakeBlobStorageService());
            });
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

        return factory;
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for GetNearbyMurals tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    /// <summary>
    /// Siembra un `User` (dependencia FK de `Mural`, restringida por `DeleteBehavior.Restrict`) y un
    /// `Mural` construido SIEMPRE con <see cref="Mural.CreateLocation"/> — nunca asignando
    /// `Latitude`/`Longitude` directo (son de solo lectura desde FEAT-009 Block 1).
    /// </summary>
    private static async Task<Guid> SeedMuralAsync(
        WebApplicationFactory<Program> factory,
        double latitude,
        double longitude,
        MuralStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Username = $"nearby-{suffix}",
            Email = $"nearby-{suffix}@example.com",
            PasswordHash = "hash-1",
        };
        db.Users.Add(user);

        var mural = new Mural
        {
            UserId = user.Id,
            PhotoBlobName = $"{Guid.NewGuid()}.jpg",
            Location = Mural.CreateLocation(latitude, longitude),
            Status = status,
        };
        db.Murals.Add(mural);
        await db.SaveChangesAsync();

        return mural.Id;
    }

    /// <summary>
    /// Borra exactamente el `Mural` sembrado por <see cref="SeedMuralAsync"/> y su `User` asociado —
    /// en ese orden (`OnDelete(DeleteBehavior.Restrict)` en `Mural.UserId` lo exige). Regla #0 de
    /// testing.instructions.md: nunca dejar filas huérfanas en la base compartida.
    /// </summary>
    private static async Task CleanupMuralAsync(WebApplicationFactory<Program> factory, Guid muralId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mural = await db.Murals.SingleAsync(m => m.Id == muralId);
        var userId = mural.UserId;
        db.Murals.Remove(mural);
        await db.SaveChangesAsync();

        var user = await db.Users.SingleAsync(u => u.Id == userId);
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    private static async Task CleanupMuralsAsync(WebApplicationFactory<Program> factory, IEnumerable<Guid> muralIds)
    {
        foreach (var muralId in muralIds)
        {
            await CleanupMuralAsync(factory, muralId);
        }
    }

    // Desplaza `originLat` en `km` kilómetros (aprox., 1 grado ~ 111 km) — suficiente para construir
    // puntos claramente dentro/fuera de un radio dado sin depender de la implementación bajo prueba.
    private static double LatOffsetKm(double originLat, double km) => originLat + (km / 111.0);

    [Fact]
    public async Task Returns_only_Published_murals_within_radius_excluding_out_of_radius_and_non_Published_inside_radius()
    {
        var factory = CreateFactory();
        var insidePublishedId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Published);
        var insidePendingId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1.5), OriginLon, MuralStatus.Pending);
        var insideRejectedId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 2), OriginLon, MuralStatus.Rejected);
        var outsidePublishedId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 20), OriginLon, MuralStatus.Published);

        try
        {
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
            Assert.Contains(insidePublishedId, ids);
        }
        finally
        {
            await CleanupMuralsAsync(factory, new[] { insidePublishedId, insidePendingId, insideRejectedId, outsidePublishedId });
        }
    }

    [Fact]
    public async Task Results_are_ordered_ascending_by_DistanceKm()
    {
        var factory = CreateFactory();
        var farId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 4), OriginLon, MuralStatus.Published);
        var nearId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Published);
        var midId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 2), OriginLon, MuralStatus.Published);

        try
        {
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
        finally
        {
            await CleanupMuralsAsync(factory, new[] { farId, nearId, midId });
        }
    }

    [Fact]
    public async Task Radius_not_specified_defaults_to_5_km()
    {
        var factory = CreateFactory();
        var withinDefaultId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 4.5), OriginLon, MuralStatus.Published);
        var beyondDefaultId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 6), OriginLon, MuralStatus.Published);

        try
        {
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
        finally
        {
            await CleanupMuralsAsync(factory, new[] { withinDefaultId, beyondDefaultId });
        }
    }

    [Fact]
    public async Task No_Published_murals_within_radius_returns_empty_items_without_error()
    {
        var factory = CreateFactory();
        var pendingId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Pending);

        try
        {
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
        finally
        {
            await CleanupMuralAsync(factory, pendingId);
        }
    }

    /// <summary>
    /// AC-01 / mitigación R2 del threat model: dos puntos separados por ~1km exacto (calculados con la
    /// misma fórmula de gran círculo del test eliminado del viejo cálculo de distancia en memoria,
    /// independiente de la implementación bajo prueba) deben producir `DistanceKm` ≈ 1.0 con
    /// tolerancia &lt; 0.01 —
    /// valida que la conversión metros→km (`Location.Distance(searchPoint) / 1000`) sobre `STDistance`
    /// de SQL Server (geodésica, WGS84) es consistente con la distancia real, no un valor en grados sin
    /// convertir (lo que daría ~0.000009, no ~1.0).
    /// </summary>
    [Fact]
    public async Task DistanceKm_reflects_the_meters_to_km_conversion_for_two_points_known_to_be_about_1km_apart()
    {
        const double earthRadiusKm = 6371.0;
        const double expectedDistanceKm = 1.0;
        var deltaLatDeg = (expectedDistanceKm / earthRadiusKm) * (180.0 / Math.PI);
        var otherLat = OriginLat + deltaLatDeg;

        var factory = CreateFactory();
        var muralId = await SeedMuralAsync(factory, otherLat, OriginLon, MuralStatus.Published);

        try
        {
            using var scope = factory.Services.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var response = await mediator.Send(new GetNearbyMuralsQuery
            {
                Latitude = OriginLat,
                Longitude = OriginLon,
                RadiusKm = 5.0,
            });

            var item = Assert.Single(response.Items, i => i.Id == muralId);
            Assert.InRange(item.DistanceKm, expectedDistanceKm - 0.01, expectedDistanceKm + 0.01);
        }
        finally
        {
            await CleanupMuralAsync(factory, muralId);
        }
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(50.1)]
    public async Task RadiusKm_out_of_range_throws_validation_exception(double radiusKm)
    {
        var factory = CreateFactory();

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
        var factory = CreateFactory();

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
        var factory = CreateFactory();
        var muralId = await SeedMuralAsync(factory, LatOffsetKm(OriginLat, 1), OriginLon, MuralStatus.Published);

        try
        {
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
        finally
        {
            await CleanupMuralAsync(factory, muralId);
        }
    }

    /// <summary>
    /// AC-07: la vieja clase de cálculo de distancia en memoria (bounding box + fórmula del gran
    /// círculo) y su test deben quedar completamente eliminados del árbol del backend tras este
    /// bloque. Escanea los archivos fuente (no los binarios compilados) de `backend/src` y
    /// `backend/tests` en busca del nombre de esa clase, construido por concatenación tanto en la
    /// aserción como en este comentario para que ni este método ni este archivo se autodetecten como
    /// una coincidencia (equivalente a `grep -r "GeoDistance" + "Calculator" backend/`, sin el propio
    /// literal contiguo en el árbol).
    /// </summary>
    [Fact]
    public void Old_in_memory_distance_calculator_class_no_longer_exists_anywhere_in_the_backend_source_tree()
    {
        var forbiddenTerm = "GeoDistance" + "Calculator";
        var backendRoot = FindBackendRoot(AppContext.BaseDirectory);

        var matches = Directory
            .EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains(forbiddenTerm, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(matches);
    }

    private static string FindBackendRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (current.Name == "backend" && Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the 'backend' root walking up from '{startDirectory}'.");
    }
}
