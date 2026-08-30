import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  AddressesClient,
  AddressSuggestionDto,
  ApiException,
  ReverseGeocodeResponse,
  SearchAddressesResponse,
} from '../../../core/api-client/api-client.generated';
import { AddressService } from './address.service';

describe('AddressService', () => {
  let addressesClient: {
    searchAddresses: ReturnType<typeof vi.fn>;
    reverseGeocodeAddress: ReturnType<typeof vi.fn>;
  };
  let httpClient: { request: ReturnType<typeof vi.fn>; get: ReturnType<typeof vi.fn> };
  let service: AddressService;

  beforeEach(() => {
    addressesClient = {
      searchAddresses: vi.fn(),
      reverseGeocodeAddress: vi.fn(),
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
});
