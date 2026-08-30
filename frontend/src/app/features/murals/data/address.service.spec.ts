import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  AddressesClient,
  AddressSuggestionDto,
  ApiException,
  ResolveAddressResponse,
  ReverseGeocodeResponse,
  SearchAddressesResponse,
} from '../../../core/api-client/api-client.generated';
import { AddressService } from './address.service';

describe('AddressService', () => {
  let addressesClient: {
    searchAddresses: ReturnType<typeof vi.fn>;
    reverseGeocodeAddress: ReturnType<typeof vi.fn>;
    resolveAddress: ReturnType<typeof vi.fn>;
  };
  let httpClient: { request: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn> };
  let service: AddressService;

  beforeEach(() => {
    addressesClient = {
      searchAddresses: vi.fn(),
      reverseGeocodeAddress: vi.fn(),
      resolveAddress: vi.fn(),
    };
    // Never expected to be called by AddressService — the service must go exclusively through
    // AddressesClient (AC-20). Throwing here turns any accidental direct use into a failing test.
    httpClient = {
      request: vi.fn(() => {
        throw new Error('AddressService must never call HttpClient directly (AC-20).');
      }),
      get: vi.fn(() => {
        throw new Error('AddressService must never call HttpClient directly (AC-20).');
      }),
    };

    TestBed.configureTestingModule({
      providers: [
        AddressService,
        { provide: AddressesClient, useValue: addressesClient },
        { provide: HttpClient, useValue: httpClient },
      ],
    });

    service = TestBed.inject(AddressService);
  });

  // Required test 1: search() con respuesta con resultados devuelve el array mapeado.
  it('search() con respuesta con resultados devuelve el array mapeado', () => {
    const response = new SearchAddressesResponse({
      suggestions: [
        new AddressSuggestionDto({
          address: 'Av. 18 de Julio 1234',
          latitude: -34.9,
          longitude: -56.16,
        }),
      ],
    });
    addressesClient.searchAddresses.mockReturnValue(of(response));

    let received: unknown;

    service.search('18 de julio').subscribe((result) => {
      received = result;
    });

    expect(received).toEqual([
      { address: 'Av. 18 de Julio 1234', latitude: -34.9, longitude: -56.16 },
    ]);
    expect(addressesClient.searchAddresses).toHaveBeenCalledWith('18 de julio');
  });

  // Required test 2: search() con respuesta 503 propaga un ApiError (sad path, AC-19).
  it('search() con respuesta 503 propaga un ApiError', () => {
    const apiException = new ApiException(
      'Service Unavailable',
      503,
      JSON.stringify({ title: 'El servicio de direcciones no está disponible.' }),
      {},
      null,
    );
    addressesClient.searchAddresses.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;

    service.search('18 de julio').subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect(receivedError).toEqual({
      status: 503,
      message: 'El servicio de direcciones no está disponible.',
    });
  });

  // Required test 3: search() con lista vacía devuelve [] sin error (AC-18).
  it('search() con lista vacía devuelve [] sin error', () => {
    const response = new SearchAddressesResponse({ suggestions: [] });
    addressesClient.searchAddresses.mockReturnValue(of(response));

    let received: unknown;
    let errored = false;

    service.search('direccion inexistente').subscribe({
      next: (result) => {
        received = result;
      },
      error: () => {
        errored = true;
      },
    });

    expect(errored).toBe(false);
    expect(received).toEqual([]);
  });

  // Required test 4: reverseGeocode() con resultado devuelve la sugerencia.
  it('reverseGeocode() con resultado devuelve la sugerencia', () => {
    const response = new ReverseGeocodeResponse({
      suggestion: new AddressSuggestionDto({
        address: 'Plaza Independencia',
        latitude: -34.906,
        longitude: -56.199,
      }),
    });
    addressesClient.reverseGeocodeAddress.mockReturnValue(of(response));

    let received: unknown;

    service.reverseGeocode(-34.906, -56.199).subscribe((result) => {
      received = result;
    });

    expect(received).toEqual({
      address: 'Plaza Independencia',
      latitude: -34.906,
      longitude: -56.199,
    });
    expect(addressesClient.reverseGeocodeAddress).toHaveBeenCalledWith(-34.906, -56.199);
  });

  // Required test 5: reverseGeocode() con suggestion: null devuelve null sin error.
  it('reverseGeocode() con suggestion: null devuelve null sin error', () => {
    const response = new ReverseGeocodeResponse({ suggestion: undefined });
    addressesClient.reverseGeocodeAddress.mockReturnValue(of(response));

    let received: unknown;
    let errored = false;

    service.reverseGeocode(0, 0).subscribe({
      next: (result) => {
        received = result;
      },
      error: () => {
        errored = true;
      },
    });

    expect(errored).toBe(false);
    expect(received).toBeNull();
  });

  // Required test 6: reverseGeocode() con respuesta 503 propaga un ApiError (sad path).
  it('reverseGeocode() con respuesta 503 propaga un ApiError', () => {
    const apiException = new ApiException('Service Unavailable', 503, '', {}, null);
    addressesClient.reverseGeocodeAddress.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;

    service.reverseGeocode(-34.9, -56.1).subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect((receivedError as { status: number }).status).toBe(503);
  });

  // Required test 7: search()/reverseGeocode() invocan AddressesClient, nunca HttpClient/fetch
  // directo a un host externo (AC-20).
  it('search() y reverseGeocode() invocan AddressesClient, nunca HttpClient directamente', () => {
    addressesClient.searchAddresses.mockReturnValue(of(new SearchAddressesResponse({ suggestions: [] })));
    addressesClient.reverseGeocodeAddress.mockReturnValue(
      of(new ReverseGeocodeResponse({ suggestion: undefined })),
    );

    service.search('montevideo').subscribe();
    service.reverseGeocode(-34.9, -56.1).subscribe();

    expect(addressesClient.searchAddresses).toHaveBeenCalledTimes(1);
    expect(addressesClient.reverseGeocodeAddress).toHaveBeenCalledTimes(1);
    expect(httpClient.request).not.toHaveBeenCalled();
    expect(httpClient.get).not.toHaveBeenCalled();
  });

  // FIX-005 — Required test 8: resolveIfNeeded() con coordenadas ya reales no llama a la red.
  it('resolveIfNeeded() con coordenadas ya reales no llama a la red', () => {
    const suggestion = new AddressSuggestionDto({
      address: 'Plaza Independencia',
      latitude: -34.906,
      longitude: -56.199,
    });

    let received: unknown;

    service.resolveIfNeeded(suggestion).subscribe((result) => {
      received = result;
    });

    expect(received).toEqual(suggestion);
    expect(addressesClient.resolveAddress).not.toHaveBeenCalled();
  });

  // FIX-005 — Required test 9: resolveIfNeeded() con 0,0 llama a resolveAddress() con los 4 campos
  // de la sugerencia y devuelve el resultado resuelto.
  it('resolveIfNeeded() con 0,0 llama a resolveAddress() y devuelve las coordenadas resueltas', () => {
    const suggestion = new AddressSuggestionDto({
      address: 'Bulevar General Artigas 1234, Montevideo',
      latitude: 0,
      longitude: 0,
      streetId: 8143,
      portalNumber: 1234,
      locality: 'MONTEVIDEO',
      type: 'CALLEyPORTAL',
    });
    const resolved = new AddressSuggestionDto({
      address: 'Bulevar General Artigas 1234, Montevideo',
      latitude: -34.9059,
      longitude: -56.1639,
    });
    addressesClient.resolveAddress.mockReturnValue(of(new ResolveAddressResponse({ suggestion: resolved })));

    let received: unknown;

    service.resolveIfNeeded(suggestion).subscribe((result) => {
      received = result;
    });

    expect(addressesClient.resolveAddress).toHaveBeenCalledWith(8143, 1234, 'MONTEVIDEO', 'CALLEyPORTAL');
    expect(received).toEqual(resolved);
  });

  // FIX-005 — Required test 10: resolveIfNeeded() sin resultado del proveedor devuelve null.
  it('resolveIfNeeded() sin resultado del proveedor devuelve null', () => {
    const suggestion = new AddressSuggestionDto({
      address: 'Dirección inexistente 1234',
      latitude: 0,
      longitude: 0,
      streetId: 99999,
      portalNumber: 1234,
      locality: 'MONTEVIDEO',
      type: 'CALLEyPORTAL',
    });
    addressesClient.resolveAddress.mockReturnValue(of(new ResolveAddressResponse({ suggestion: undefined })));

    let received: unknown;

    service.resolveIfNeeded(suggestion).subscribe((result) => {
      received = result;
    });

    expect(received).toBeNull();
  });

  // FIX-005 — Required test 11: resolveIfNeeded() con el proveedor caído (503) devuelve null, sin
  // propagar el error (el componente no necesita distinguir "sin resultado" de "proveedor caído").
  it('resolveIfNeeded() con el proveedor caído (503) devuelve null sin propagar el error', () => {
    const suggestion = new AddressSuggestionDto({
      address: 'Bulevar General Artigas 1234, Montevideo',
      latitude: 0,
      longitude: 0,
      streetId: 8143,
      portalNumber: 1234,
      locality: 'MONTEVIDEO',
      type: 'CALLEyPORTAL',
    });
    const apiException = new ApiException('Service Unavailable', 503, '', {}, null);
    addressesClient.resolveAddress.mockReturnValue(throwError(() => apiException));

    let received: unknown;
    let errored = false;

    service.resolveIfNeeded(suggestion).subscribe({
      next: (result) => {
        received = result;
      },
      error: () => {
        errored = true;
      },
    });

    expect(errored).toBe(false);
    expect(received).toBeNull();
  });
});
