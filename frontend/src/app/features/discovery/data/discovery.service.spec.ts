import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  ApiException,
  DiscoveryClient,
  GetNearbyMuralsResponse,
  NearbyMuralItemResponse,
} from '../../../core/api-client/api-client.generated';
import { DiscoveryService } from './discovery.service';

describe('DiscoveryService', () => {
  let discoveryClient: {
    getNearbyMurals: ReturnType<typeof vi.fn>;
  };
  let service: DiscoveryService;

  beforeEach(() => {
    discoveryClient = {
      getNearbyMurals: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [DiscoveryService, { provide: DiscoveryClient, useValue: discoveryClient }],
    });

    service = TestBed.inject(DiscoveryService);
  });

  // Required test: mapeo de respuesta exitosa.
  it('getNearbyMurals() exitoso devuelve los items mapeados, pasando lat/lng/radiusKm explícitos', () => {
    const item = new NearbyMuralItemResponse({
      id: 'mural-1',
      photoUrl: 'https://storage.example.com/mural-photos/mural-1.jpg?sas=token',
      latitude: -34.6,
      longitude: -58.4,
      createdAt: new Date('2026-08-19T00:00:00Z'),
      distanceKm: 1.2,
    });
    const response = new GetNearbyMuralsResponse({ items: [item] });
    discoveryClient.getNearbyMurals.mockReturnValue(of(response));

    let received: NearbyMuralItemResponse[] | undefined;

    service.getNearbyMurals(-34.6, -58.4, 5).subscribe((result) => {
      received = result;
    });

    expect(received).toEqual([item]);
    expect(discoveryClient.getNearbyMurals).toHaveBeenCalledWith(-34.6, -58.4, 5);
  });

  // `lat`/`lng` quedaron opcionales en TypeScript pese a ser requeridos por el backend — el
  // servicio siempre debe pasarlos explícitamente, nunca omitirlos, incluso cuando son 0.
  it('getNearbyMurals() pasa lat/lng explícitos incluso cuando son 0 (falsy)', () => {
    discoveryClient.getNearbyMurals.mockReturnValue(of(new GetNearbyMuralsResponse({ items: [] })));

    service.getNearbyMurals(0, 0).subscribe();

    expect(discoveryClient.getNearbyMurals).toHaveBeenCalledWith(0, 0, undefined);
  });

  // `items` puede venir `undefined` del cliente generado (nunca debe propagarse como tal, AC-06).
  it('getNearbyMurals() con items undefined devuelve una lista vacía', () => {
    discoveryClient.getNearbyMurals.mockReturnValue(of(new GetNearbyMuralsResponse({})));

    let received: NearbyMuralItemResponse[] | undefined;

    service.getNearbyMurals(-34.6, -58.4).subscribe((result) => {
      received = result;
    });

    expect(received).toEqual([]);
  });

  // Required test: mapeo de errores tipados (ApiException → ApiError).
  it('getNearbyMurals() con error 400 devuelve un ApiError tipado con el mensaje del backend', () => {
    const apiException = new ApiException(
      'Bad Request',
      400,
      JSON.stringify({ title: 'radiusKm must be between 0.1 and 50.' }),
      {},
      null,
    );
    discoveryClient.getNearbyMurals.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;
    let completed = false;

    service.getNearbyMurals(-34.6, -58.4, 100).subscribe({
      next: () => {
        completed = true;
      },
      error: (error) => {
        receivedError = error;
      },
    });

    expect(completed).toBe(false);
    expect(receivedError).toEqual({
      status: 400,
      message: 'radiusKm must be between 0.1 and 50.',
    });
  });

  // Required test: mapeo de errores tipados (network/429 sin body parseable).
  it('getNearbyMurals() con error 429 devuelve un ApiError tipado', () => {
    const apiException = new ApiException('Too Many Requests', 429, '', {}, null);
    discoveryClient.getNearbyMurals.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;

    service.getNearbyMurals(-34.6, -58.4).subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect((receivedError as { status: number }).status).toBe(429);
  });
});
