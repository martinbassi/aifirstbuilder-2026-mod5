import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  ApiException,
  GetPendingMuralsResponse,
  ModerationActionResponse,
  ModerationClient,
  MuralResponse,
} from '../../../core/api-client/api-client.generated';
import { ModerationService } from './moderation.service';

describe('ModerationService', () => {
  let moderationClient: {
    pending: ReturnType<typeof vi.fn>;
    approve: ReturnType<typeof vi.fn>;
    reject: ReturnType<typeof vi.fn>;
  };
  let service: ModerationService;

  beforeEach(() => {
    moderationClient = {
      pending: vi.fn(),
      approve: vi.fn(),
      reject: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [ModerationService, { provide: ModerationClient, useValue: moderationClient }],
    });

    service = TestBed.inject(ModerationService);
  });

  // Required test: getPending() mapea la respuesta del cliente generado, incluyendo
  // page/pageSize/totalCount.
  it('getPending() mapea la respuesta del cliente generado', () => {
    const mural = new MuralResponse({
      id: 'mural-1',
      status: 'Pending',
      photoUrl: 'https://storage.example.com/mural-photos/mural-1.jpg?sas=token',
      latitude: -34.6,
      longitude: -58.4,
      createdAt: new Date('2026-08-19T00:00:00Z'),
    });
    const response = new GetPendingMuralsResponse({
      murals: [mural],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    moderationClient.pending.mockReturnValue(of(response));

    let received: { murals: MuralResponse[]; page: number; pageSize: number; totalCount: number } | undefined;

    service.getPending(1, 20).subscribe((result) => {
      received = result;
    });

    expect(received).toEqual({
      murals: [mural],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    expect(moderationClient.pending).toHaveBeenCalledWith(1, 20);
  });

  // Required test: approve() propaga ApiError en caso de fallo (sad path).
  it('approve() con error 409 devuelve un ApiError tipado', () => {
    const apiException = new ApiException(
      'Conflict',
      409,
      JSON.stringify({ title: 'Mural is not pending.' }),
      {},
      null,
    );
    moderationClient.approve.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;

    service.approve('mural-1').subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect(receivedError).toEqual({ status: 409, message: 'Mural is not pending.' });
    expect(moderationClient.approve).toHaveBeenCalledWith('mural-1');
  });

  it('approve() exitoso devuelve la respuesta mapeada', () => {
    const response = new ModerationActionResponse({ id: 'mural-1', status: 'Published' });
    moderationClient.approve.mockReturnValue(of(response));

    let received: ModerationActionResponse | undefined;

    service.approve('mural-1').subscribe((result) => {
      received = result;
    });

    expect(received).toEqual(response);
  });

  // Required test: rejectMural() propaga ApiError en caso de fallo (sad path).
  it('rejectMural() con error 404 devuelve un ApiError tipado', () => {
    const apiException = new ApiException(
      'Not Found',
      404,
      JSON.stringify({ title: 'Mural not found.' }),
      {},
      null,
    );
    moderationClient.reject.mockReturnValue(throwError(() => apiException));

    let receivedError: unknown;

    service.rejectMural('missing-id').subscribe({
      error: (error) => {
        receivedError = error;
      },
    });

    expect(receivedError).toEqual({ status: 404, message: 'Mural not found.' });
    expect(moderationClient.reject).toHaveBeenCalledWith('missing-id');
  });

  it('rejectMural() exitoso devuelve la respuesta mapeada', () => {
    const response = new ModerationActionResponse({ id: 'mural-1', status: 'Rejected' });
    moderationClient.reject.mockReturnValue(of(response));

    let received: ModerationActionResponse | undefined;

    service.rejectMural('mural-1').subscribe((result) => {
      received = result;
    });

    expect(received).toEqual(response);
  });
});
