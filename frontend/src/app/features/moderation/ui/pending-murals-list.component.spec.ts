import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ModerationActionResponse, MuralResponse } from '../../../core/api-client/api-client.generated';
import { ModerationService, PendingMuralsPage } from '../data/moderation.service';
import { PendingMuralsListComponent } from './pending-murals-list.component';

function buildMural(overrides: Partial<MuralResponse> = {}): MuralResponse {
  return new MuralResponse({
    id: 'mural-1',
    status: 'Pending',
    photoUrl: 'https://storage.example.com/mural-photos/mural-1.jpg?sas=token',
    latitude: -34.6,
    longitude: -58.4,
    createdAt: new Date('2026-08-19T00:00:00Z'),
    ...overrides,
  });
}

function buildPage(overrides: Partial<PendingMuralsPage> = {}): PendingMuralsPage {
  return {
    murals: [buildMural()],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    ...overrides,
  };
}

describe('PendingMuralsListComponent', () => {
  let moderationService: {
    getPending: ReturnType<typeof vi.fn>;
    approve: ReturnType<typeof vi.fn>;
    rejectMural: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    moderationService = {
      getPending: vi.fn(),
      approve: vi.fn(),
      rejectMural: vi.fn(),
    };

    TestBed.configureTestingModule({
      imports: [PendingMuralsListComponent],
      providers: [{ provide: ModerationService, useValue: moderationService }],
    });
  });

  // Required test: renderiza la lista de pendientes devuelta por el servicio (AC-01).
  it('renderiza la lista de murales pendientes devuelta por el servicio', () => {
    moderationService.getPending.mockReturnValue(of(buildPage()));

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    expect(moderationService.getPending).toHaveBeenCalledWith(1);
    const item: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="mural-item-mural-1"]',
    );
    expect(item).toBeTruthy();
    expect(item.textContent).toContain('-34.6');
    expect(item.textContent).toContain('-58.4');
  });

  // Required test: aprobar un ítem lo remueve de la lista tras una respuesta exitosa (AC-03).
  it('remueve el ítem de la lista al aprobarlo exitosamente', () => {
    moderationService.getPending.mockReturnValue(of(buildPage()));
    moderationService.approve.mockReturnValue(
      of(new ModerationActionResponse({ id: 'mural-1', status: 'Published' })),
    );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    const approveButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="approve-button-mural-1"]',
    );
    approveButton.click();
    fixture.detectChanges();

    expect(moderationService.approve).toHaveBeenCalledWith('mural-1');
    const item: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="mural-item-mural-1"]',
    );
    expect(item).toBeFalsy();
  });

  // Required test: rechazar un ítem lo remueve de la lista tras una respuesta exitosa (AC-05).
  it('remueve el ítem de la lista al rechazarlo exitosamente', () => {
    moderationService.getPending.mockReturnValue(of(buildPage()));
    moderationService.rejectMural.mockReturnValue(
      of(new ModerationActionResponse({ id: 'mural-1', status: 'Rejected' })),
    );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    const rejectButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="reject-button-mural-1"]',
    );
    rejectButton.click();
    fixture.detectChanges();

    expect(moderationService.rejectMural).toHaveBeenCalledWith('mural-1');
    const item: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="mural-item-mural-1"]',
    );
    expect(item).toBeFalsy();
  });

  // Required test (sad path): un error del servicio al listar se muestra sin romper el componente.
  it('muestra un error inline cuando falla la carga de pendientes, sin romper el componente', () => {
    moderationService.getPending.mockReturnValue(
      throwError(() => ({ status: 500, message: 'No se pudo cargar la lista.' })),
    );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    expect(() => fixture.detectChanges()).not.toThrow();

    const errorEl: HTMLElement = fixture.nativeElement.querySelector('[data-testid="load-error"]');
    expect(errorEl).toBeTruthy();
    expect(errorEl.textContent).toContain('No se pudo cargar la lista.');
  });

  // Required test (sad path): un error de approve/rejectMural deja el ítem en la lista y muestra el error.
  it('mantiene el ítem en la lista y muestra el error cuando approve falla', () => {
    moderationService.getPending.mockReturnValue(of(buildPage()));
    moderationService.approve.mockReturnValue(
      throwError(() => ({ status: 409, message: 'El mural ya no está pendiente.' })),
    );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    const approveButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="approve-button-mural-1"]',
    );
    approveButton.click();
    fixture.detectChanges();

    const item: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="mural-item-mural-1"]',
    );
    expect(item).toBeTruthy();
    const itemErrorEl: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="item-error-mural-1"]',
    );
    expect(itemErrorEl).toBeTruthy();
    expect(itemErrorEl.textContent).toContain('El mural ya no está pendiente.');
  });

  // Required test: "Anterior" deshabilitado en page=1; "Siguiente" deshabilitado cuando
  // page * pageSize >= totalCount.
  it('deshabilita Anterior en la primera página y Siguiente cuando no hay más páginas', () => {
    moderationService.getPending.mockReturnValue(
      of(buildPage({ page: 1, pageSize: 20, totalCount: 1 })),
    );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    const previousButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="previous-button"]',
    );
    const nextButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="next-button"]',
    );
    expect(previousButton.disabled).toBe(true);
    expect(nextButton.disabled).toBe(true);
  });

  it('habilita Siguiente cuando quedan más páginas pendientes', () => {
    moderationService.getPending.mockReturnValue(
      of(buildPage({ page: 1, pageSize: 20, totalCount: 40 })),
    );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    const nextButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="next-button"]',
    );
    expect(nextButton.disabled).toBe(false);
  });

  // Required test: click en "Siguiente" pide page + 1 al servicio y reemplaza la lista mostrada.
  it('pide la página siguiente al servicio y reemplaza la lista mostrada al hacer click en Siguiente', () => {
    const secondMural = buildMural({ id: 'mural-2' });
    moderationService.getPending
      .mockReturnValueOnce(of(buildPage({ page: 1, pageSize: 20, totalCount: 40 })))
      .mockReturnValueOnce(
        of(buildPage({ murals: [secondMural], page: 2, pageSize: 20, totalCount: 40 })),
      );

    const fixture = TestBed.createComponent(PendingMuralsListComponent);
    fixture.detectChanges();

    const nextButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="next-button"]',
    );
    nextButton.click();
    fixture.detectChanges();

    expect(moderationService.getPending).toHaveBeenCalledWith(2);
    const firstItem: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="mural-item-mural-1"]',
    );
    const secondItem: HTMLElement = fixture.nativeElement.querySelector(
      '[data-testid="mural-item-mural-2"]',
    );
    expect(firstItem).toBeFalsy();
    expect(secondItem).toBeTruthy();
  });
});
