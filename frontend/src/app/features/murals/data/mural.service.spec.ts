import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  ApiException,
  CreateMuralResponse,
  MuralResponse,
  MuralsClient,
} from '../../../core/api-client/api-client.generated';
import { MuralService } from './mural.service';

describe('MuralService', () => {
  let muralsClient: {
    muralsPOST: ReturnType<typeof vi.fn>;
    muralsGET: ReturnType<typeof vi.fn>;
  };
  let service: MuralService;

  beforeEach(() => {
    muralsClient = {
      muralsPOST: vi.fn(),
      muralsGET: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [MuralService, { provide: MuralsClient, useValue: muralsClient }],
    });

    service = TestBed.inject(MuralService);
  });

  // Required test 1: create() exitoso → devuelve la respuesta mapeada.
  it('create() exitoso devuelve la respuesta mapeada', () => {
    const response = new CreateMuralResponse({ id: 'mural-1', status: 'Pending' });
    muralsClient.muralsPOST.mockReturnValue(of(response));
    const photo = new File(['fake-bytes'], 'wall.jpg', { type: 'image/jpeg' });

    let received: CreateMuralResponse | undefined;

    service.create({ photo, latitude: -34.6, longitude: -58.4 }).subscribe((result) => {
      received = result;
    });

    expect(received).toEqual(response);
    expect(muralsClient.muralsPOST).toHaveBeenCalledWith(
      { data: photo, fileName: 'wall.jpg' },
      -34.6,
      -58.4,
    );
  });

  // Required test 2: create() con error 422/500 → devuelve un ApiError tipado con el mensaje del backend.
  it('create() con error 422 devuelve un ApiError tipado con el mensaje del backend', () => {
    const apiException = new ApiException(
      'Unprocessable Content',
      422,
      JSON.stringify({ title: 'Photo must be a valid JPEG, PNG or WebP file.' }),
      {},
      null,
    );
    muralsClient.muralsPOST.mockReturnValue(throwError(() => apiException));
    const photo = new File(['fake-bytes'], 'wall.jpg', { type: 'image/jpeg' });

    let receivedError: unknown;
    let completed = false;

    service.create({ photo, latitude: -34.6, longitude: -58.4 }).subscribe({
      next: () => {
        completed = true;
      },
      error: (error) => {
        receivedError = error;
      },
    });

    expect(completed).toBe(false);
    expect(receivedError).toEqual({
      status: 422,
      message: 'Photo must be a valid JPEG, PNG or WebP file.',
    });
  });

  it('create() con error 500 devuelve un ApiError tipado', () => {
    const apiException = new ApiException('Internal Server Error', 500, '', {}, null);
    muralsClient.muralsPOST.mockReturnValue(throwError(() => apiException));
    const photo = new File(['fake-bytes'], 'wall.jpg', { type: 'image/jpeg' });

    let receivedError: unknown;

    service.create({ photo, latitude: -34.6, longitude: -58.4 }).subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect((receivedError as { status: number }).status).toBe(500);
  });

  // Required test 3: getById() exitoso → devuelve la respuesta mapeada.
  it('getById() exitoso devuelve la respuesta mapeada', () => {
    const response = new MuralResponse({
      id: 'mural-1',
      status: 'Pending',
      photoUrl: 'https://storage.example.com/mural-photos/mural-1.jpg?sas=token',
      latitude: -34.6,
      longitude: -58.4,
      createdAt: new Date('2026-08-19T00:00:00Z'),
    });
    muralsClient.muralsGET.mockReturnValue(of(response));

    let received: MuralResponse | undefined;

    service.getById('mural-1').subscribe((result) => {
      received = result;
    });

    expect(received).toEqual(response);
    expect(muralsClient.muralsGET).toHaveBeenCalledWith('mural-1');
  });

  // Required test 4: getById() con error 404 → devuelve un ApiError tipado.
  it('getById() con error 404 devuelve un ApiError tipado', () => {
    const apiException = new ApiException(
      'Not Found',
      404,
      JSON.stringify({ title: 'Mural not found.' }),
      {},
      null,
    );
    muralsClient.muralsGET.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;

    service.getById('missing-id').subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect(receivedError).toEqual({ status: 404, message: 'Mural not found.' });
  });
});
