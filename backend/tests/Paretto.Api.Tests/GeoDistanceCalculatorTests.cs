using Paretto.Domain.Services;

namespace Paretto.Api.Tests;

/// <summary>
/// Block 1 (FEAT-001d) — <see cref="GeoDistanceCalculator"/>. Unitarios puros: sin EF Core, sin
/// <c>WebApplicationFactory</c>.
/// </summary>
public class GeoDistanceCalculatorTests
{
    [Fact]
    public void HaversineKm_distance_between_a_point_and_itself_is_zero()
    {
        const double lat = -34.6037;
        const double lon = -58.3816;

        var distance = GeoDistanceCalculator.HaversineKm(lat, lon, lat, lon);

        Assert.Equal(0.0, distance, precision: 6);
    }

    [Fact]
    public void HaversineKm_known_distance_between_two_real_coordinates_is_within_tolerance()
    {
        // Dos puntos sobre el mismo meridiano (misma longitud), a partir de una coordenada real
        // (Obelisco, Buenos Aires): sobre un mismo meridiano, la distancia del gran círculo es
        // exactamente el arco `radio terrestre * delta de latitud en radianes` — un valor conocido,
        // independiente de la implementación de Haversine bajo prueba.
        const double originLat = -34.6037;
        const double originLon = -58.3816;
        const double earthRadiusKm = 6371.0;
        const double expectedDistanceKm = 5.0;

        var deltaLatDeg = (expectedDistanceKm / earthRadiusKm) * (180.0 / Math.PI);
        var otherLat = originLat + deltaLatDeg;

        var distance = GeoDistanceCalculator.HaversineKm(
            originLat, originLon, otherLat, originLon);

        Assert.InRange(distance, expectedDistanceKm - 0.1, expectedDistanceKm + 0.1);
    }

    [Fact]
    public void BoundingBox_contains_the_origin_point_and_a_point_at_the_real_radius()
    {
        const double originLat = -34.6037;
        const double originLon = -58.3816;
        const double radiusKm = 5.0;

        var (minLat, maxLat, minLon, maxLon) =
            GeoDistanceCalculator.BoundingBox(originLat, originLon, radiusKm);

        Assert.InRange(originLat, minLat, maxLat);
        Assert.InRange(originLon, minLon, maxLon);

        // Punto real a ~radiusKm del origen (desplazado en latitud, aproximación 1 grado ~ 111km).
        var nearbyLat = originLat + radiusKm / 111.0;
        var distanceToNearby = GeoDistanceCalculator.HaversineKm(
            originLat, originLon, nearbyLat, originLon);
        Assert.InRange(distanceToNearby, radiusKm * 0.9, radiusKm * 1.1);
        Assert.InRange(nearbyLat, minLat, maxLat);

        // Punto claramente fuera (3x el radio) no debe estar dentro del bounding box.
        var farLat = originLat + (radiusKm * 3) / 111.0;
        Assert.False(farLat >= minLat && farLat <= maxLat);
    }

    [Fact]
    public void BoundingBox_near_the_poles_does_not_throw_or_return_NaN_or_Infinity()
    {
        // Exactamente en el polo: cos(90°) ≈ 6.12e-17, por debajo del umbral 1e-10 de la guarda
        // (GeoDistanceCalculator.cs:49) — este valor SÍ dispara la rama `deltaLon = 180.0`, a
        // diferencia de 89.9° (cos ≈ 0.0017, muy por encima del umbral), que la dejaba sin probar.
        const double northPoleLat = 90.0;
        const double lon = 10.0;
        const double radiusKm = 5.0;

        var (minLat, maxLat, minLon, maxLon) =
            GeoDistanceCalculator.BoundingBox(northPoleLat, lon, radiusKm);

        Assert.False(double.IsNaN(minLat) || double.IsInfinity(minLat));
        Assert.False(double.IsNaN(maxLat) || double.IsInfinity(maxLat));
        Assert.False(double.IsNaN(minLon) || double.IsInfinity(minLon));
        Assert.False(double.IsNaN(maxLon) || double.IsInfinity(maxLon));

        // La guarda debe haber activado deltaLon = 180.0 (todo el rango de longitud), no el cálculo
        // por división de cosLat.
        Assert.Equal(lon - 180.0, minLon, precision: 9);
        Assert.Equal(lon + 180.0, maxLon, precision: 9);
    }
}
