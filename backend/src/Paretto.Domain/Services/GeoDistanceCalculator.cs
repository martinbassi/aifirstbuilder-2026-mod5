namespace Paretto.Domain.Services;

/// <summary>
/// Cálculo de distancia geográfica y bounding box, en memoria y sin dependencias de
/// infraestructura (EF Core, MediatR) — función pura de <c>Paretto.Domain</c>.
/// Ver ADR-005 (docs/adr/adr-005-nearby-murals-haversine-sin-geography.md): bounding box en SQL +
/// Haversine en memoria en vez de <c>geography</c>/NetTopologySuite, decisión para el volumen de un
/// MVP.
/// </summary>
public static class GeoDistanceCalculator
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Distancia en kilómetros entre dos coordenadas, usando la fórmula de Haversine.
    /// </summary>
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        var deltaLatRad = DegreesToRadians(lat2 - lat1);
        var deltaLonRad = DegreesToRadians(lon2 - lon1);

        var lat1Rad = DegreesToRadians(lat1);
        var lat2Rad = DegreesToRadians(lat2);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Rectángulo aproximado de <paramref name="radiusKm"/> alrededor de (<paramref name="lat"/>,
    /// <paramref name="lon"/>), usado para acotar el dataset en SQL antes de calcular la distancia
    /// exacta en memoria sobre el subconjunto resultante. En latitudes cercanas a ±90°, el coseno de
    /// la latitud tiende a 0 y haría que <c>deltaLon</c> divergiera a <c>Infinity</c>/<c>NaN</c> — en
    /// ese caso se usa <c>deltaLon = 180</c> (todo el rango de longitud) en su lugar.
    /// </summary>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) BoundingBox(
        double lat, double lon, double radiusKm)
    {
        const double kmPerDegreeLat = 111.0;

        var deltaLat = radiusKm / kmPerDegreeLat;

        var cosLat = Math.Cos(DegreesToRadians(lat));
        var deltaLon = Math.Abs(cosLat) < 1e-10
            ? 180.0
            : radiusKm / (kmPerDegreeLat * cosLat);

        return (lat - deltaLat, lat + deltaLat, lon - deltaLon, lon + deltaLon);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
