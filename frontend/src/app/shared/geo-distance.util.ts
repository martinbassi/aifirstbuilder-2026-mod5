/** Par de coordenadas geográficas. Declarada localmente en vez de importar `MapCenter`
 * (`features/discovery/ui/discovery-map.component.ts`) para no invertir la dependencia
 * `shared/` → `features/` — mismo criterio ya usado en el proyecto para `MapCenter`, que a su vez
 * redeclara en vez de importar `GeolocationCoordinates`. Estructuralmente compatible con ambas (los
 * mismos dos campos), así que se pasan sin conversión explícita gracias al structural typing de
 * TypeScript. */
export interface Coordinates {
  latitude: number;
  longitude: number;
}

const EARTH_RADIUS_METERS = 6371000;

function toRadians(degrees: number): number {
  return (degrees * Math.PI) / 180;
}

/**
 * Distancia en línea recta entre dos coordenadas, fórmula de Haversine, en METROS. El equivalente
 * que supo tener el backend (`GeoDistanceCalculator`) fue eliminado por FEAT-009 al migrar a
 * `geography` y devolvía kilómetros; este util devuelve metros directamente porque su único
 * consumidor (`discovery-map.component.ts`, spec Block 2) trabaja en metros desde el principio.
 */
export function haversineDistanceMeters(a: Coordinates, b: Coordinates): number {
  const deltaLatitude = toRadians(b.latitude - a.latitude);
  const deltaLongitude = toRadians(b.longitude - a.longitude);
  const latitudeA = toRadians(a.latitude);
  const latitudeB = toRadians(b.latitude);

  const haversine =
    Math.sin(deltaLatitude / 2) ** 2 +
    Math.cos(latitudeA) * Math.cos(latitudeB) * Math.sin(deltaLongitude / 2) ** 2;
  const angularDistance = 2 * Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine));

  return EARTH_RADIUS_METERS * angularDistance;
}
