import { TestBed } from '@angular/core/testing';
import { GeolocationService } from './geolocation.service';

/**
 * Stubs `navigator.geolocation.getCurrentPosition` with the given behavior. Mirrors the stub
 * `create-mural-form.component.spec.ts` used before this service existed (kept here now that the
 * browser call moved into `GeolocationService`).
 */
function stubGeolocation(
  behavior: 'success' | 'denied' | 'unavailable' | 'timeout' | 'unsupported',
  position?: { latitude: number; longitude: number },
): void {
  if (behavior === 'unsupported') {
    Object.defineProperty(navigator, 'geolocation', { value: undefined, configurable: true });
    return;
  }

  const errorCodeByBehavior: Record<'denied' | 'unavailable' | 'timeout', number> = {
    denied: 1,
    unavailable: 2,
    timeout: 3,
  };

  Object.defineProperty(navigator, 'geolocation', {
    configurable: true,
    value: {
      getCurrentPosition: (success: PositionCallback, error?: PositionErrorCallback) => {
        if (behavior === 'success') {
          success({
            coords: {
              latitude: position?.latitude ?? -34.6037,
              longitude: position?.longitude ?? -58.3816,
            },
          } as GeolocationPosition);
        } else if (error) {
          error({
            code: errorCodeByBehavior[behavior],
            message: `stubbed ${behavior}`,
          } as GeolocationPositionError);
        }
      },
    },
  });
}

describe('GeolocationService', () => {
  let service: GeolocationService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(GeolocationService);
  });

  afterEach(() => {
    Object.defineProperty(navigator, 'geolocation', { value: undefined, configurable: true });
  });

  it('resuelve con { latitude, longitude } cuando el navegador concede el permiso', async () => {
    stubGeolocation('success', { latitude: -34.6037, longitude: -58.3816 });

    const result = await service.getCurrentPosition();

    expect(result).toEqual({ latitude: -34.6037, longitude: -58.3816 });
  });

  it('rechaza con { kind: "denied" } cuando el usuario deniega el permiso', async () => {
    stubGeolocation('denied');

    await expect(service.getCurrentPosition()).rejects.toEqual({ kind: 'denied' });
  });

  it('rechaza con { kind: "unavailable" } cuando la posición no está disponible', async () => {
    stubGeolocation('unavailable');

    await expect(service.getCurrentPosition()).rejects.toEqual({ kind: 'unavailable' });
  });

  it('rechaza con { kind: "timeout" } cuando la solicitud excede el tiempo de espera', async () => {
    stubGeolocation('timeout');

    await expect(service.getCurrentPosition()).rejects.toEqual({ kind: 'timeout' });
  });

  it('rechaza con { kind: "unavailable" } cuando el navegador no soporta geolocalización', async () => {
    stubGeolocation('unsupported');

    await expect(service.getCurrentPosition()).rejects.toEqual({ kind: 'unavailable' });
  });
});
