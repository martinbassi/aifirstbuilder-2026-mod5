import { haversineDistanceMeters } from './geo-distance.util';

describe('haversineDistanceMeters', () => {
  it('devuelve 0 para dos puntos idénticos', () => {
    const point = { latitude: -34.6037, longitude: -58.3816 };

    expect(haversineDistanceMeters(point, point)).toBe(0);
  });

  it('devuelve ~1000m (tolerancia <10m) para dos puntos conocidos a ~1km de distancia real', () => {
    // Mismo par de coordenadas que el test equivalente que tenía el backend
    // (GeoDistanceCalculator, eliminado por FEAT-009): dos puntos sobre el mismo meridiano de
    // Montevideo separados ~0.009° de latitud (~1km).
    const a = { latitude: -34.905830, longitude: -56.191388 };
    const b = { latitude: -34.896830, longitude: -56.191388 };

    const distance = haversineDistanceMeters(a, b);

    expect(distance).toBeGreaterThan(990);
    expect(distance).toBeLessThan(1010);
  });

  it('es simétrica: haversineDistanceMeters(a, b) === haversineDistanceMeters(b, a)', () => {
    const a = { latitude: -34.6037, longitude: -58.3816 };
    const b = { latitude: -34.0, longitude: -58.0 };

    expect(haversineDistanceMeters(a, b)).toBe(haversineDistanceMeters(b, a));
  });
});
