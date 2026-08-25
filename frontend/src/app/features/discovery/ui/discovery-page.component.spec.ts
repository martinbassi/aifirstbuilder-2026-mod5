import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';
import { GeolocationService } from '../../../shared/geolocation.service';
import { DiscoveryService } from '../data/discovery.service';
import { DiscoveryMapComponent } from './discovery-map.component';
import { DiscoveryPageComponent } from './discovery-page.component';

function buildItem(overrides: Partial<NearbyMuralItemResponse> = {}): NearbyMuralItemResponse {
  return new NearbyMuralItemResponse({
    id: 'mural-1',
    photoUrl: 'https://storage.example.com/mural-photos/mural-1.jpg?sas=token',
    latitude: -34.6,
    longitude: -58.4,
    createdAt: new Date('2026-08-19T00:00:00Z'),
    distanceKm: 1.0,
    ...overrides,
  });
}

/** Flushes the microtask queue so `GeolocationService.getCurrentPosition()` (a Promise) settles,
 * then re-runs change detection — same pattern as `create-mural-form.component.spec.ts`. */
async function flushMicrotasks(fixture: { detectChanges: () => void }): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();
}

describe('DiscoveryPageComponent', () => {
  let geolocationService: { getCurrentPosition: ReturnType<typeof vi.fn> };
  let discoveryService: { getNearbyMurals: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    geolocationService = { getCurrentPosition: vi.fn() };
    discoveryService = { getNearbyMurals: vi.fn() };

    TestBed.configureTestingModule({
      imports: [DiscoveryPageComponent],
      providers: [
        { provide: GeolocationService, useValue: geolocationService },
        { provide: DiscoveryService, useValue: discoveryService },
      ],
    });
  });

  // Required test: con GeolocationService resolviendo, dispara la consulta con esas coordenadas.
  it('con geolocalización resuelta, dispara discoveryService.getNearbyMurals con esas coordenadas', async () => {
    geolocationService.getCurrentPosition.mockReturnValue(
      Promise.resolve({ latitude: -34.6037, longitude: -58.3816 }),
    );
    const items = [buildItem()];
    discoveryService.getNearbyMurals.mockReturnValue(of(items));

    const fixture = TestBed.createComponent(DiscoveryPageComponent);
    fixture.detectChanges();
    await flushMicrotasks(fixture);

    expect(discoveryService.getNearbyMurals).toHaveBeenCalledWith(-34.6037, -58.3816);
    expect(fixture.componentInstance.items()).toEqual(items);
    expect(fixture.nativeElement.querySelector('[data-testid="manual-location-form"]')).toBeNull();
  });

  // Required test: con GeolocationService rechazando, NO dispara la consulta y ofrece el fallback
  // manual.
  it('con geolocalización rechazada, no dispara la consulta y ofrece el fallback manual', async () => {
    geolocationService.getCurrentPosition.mockReturnValue(Promise.reject({ kind: 'denied' }));

    const fixture = TestBed.createComponent(DiscoveryPageComponent);
    fixture.detectChanges();
    await flushMicrotasks(fixture);

    expect(discoveryService.getNearbyMurals).not.toHaveBeenCalled();
    expect(fixture.componentInstance.items()).toEqual([]);

    const manualForm = fixture.nativeElement.querySelector('[data-testid="manual-location-form"]');
    expect(manualForm).not.toBeNull();
  });

  it('el fallback manual dispara la consulta con las coordenadas ingresadas', async () => {
    geolocationService.getCurrentPosition.mockReturnValue(Promise.reject({ kind: 'unavailable' }));
    const items = [buildItem()];
    discoveryService.getNearbyMurals.mockReturnValue(of(items));

    const fixture = TestBed.createComponent(DiscoveryPageComponent);
    fixture.detectChanges();
    await flushMicrotasks(fixture);

    fixture.componentInstance.onLatitudeChange({
      target: { valueAsNumber: -34.6037 },
    } as unknown as Event);
    fixture.componentInstance.onLongitudeChange({
      target: { valueAsNumber: -58.3816 },
    } as unknown as Event);
    fixture.componentInstance.searchManually();
    fixture.detectChanges();

    expect(discoveryService.getNearbyMurals).toHaveBeenCalledWith(-34.6037, -58.3816);
    expect(fixture.componentInstance.items()).toEqual(items);
  });

  // Required test: con discovery.service.ts devolviendo un ApiError, muestra el mensaje de error
  // genérico sin swallear la falla.
  it('con discoveryService devolviendo un ApiError, muestra el mensaje de error sin swallearlo', async () => {
    geolocationService.getCurrentPosition.mockReturnValue(
      Promise.resolve({ latitude: -34.6037, longitude: -58.3816 }),
    );
    discoveryService.getNearbyMurals.mockReturnValue(
      throwError(() => ({ status: 500, message: 'Ocurrió un error inesperado. Intentá nuevamente.' })),
    );

    const fixture = TestBed.createComponent(DiscoveryPageComponent);
    fixture.detectChanges();
    await flushMicrotasks(fixture);

    expect(fixture.componentInstance.errorMessage()).toBe(
      'Ocurrió un error inesperado. Intentá nuevamente.',
    );
    const errorElement = fixture.nativeElement.querySelector('[data-testid="error-message"]');
    expect(errorElement).not.toBeNull();
    expect(errorElement.textContent).toContain('Ocurrió un error inesperado');
    // El error nunca queda "silencioso": items se mantiene vacío en vez de dejar un estado
    // inconsistente/loading colgado.
    expect(fixture.componentInstance.items()).toEqual([]);
  });

  // Block 3 — output `mapMoved` y botón "Buscar en esta área" (spec-FEAT-005).

  // Required test: el botón "Buscar en esta área" no está presente al cargar la página; aparece
  // después de que app-discovery-map emite mapMoved — valida AC-04.
  it('el botón "Buscar en esta área" no está al cargar y aparece tras mapMoved (AC-04)', async () => {
    geolocationService.getCurrentPosition.mockReturnValue(
      Promise.resolve({ latitude: -34.6037, longitude: -58.3816 }),
    );
    discoveryService.getNearbyMurals.mockReturnValue(of([]));

    const fixture = TestBed.createComponent(DiscoveryPageComponent);
    fixture.detectChanges();
    await flushMicrotasks(fixture);

    expect(
      fixture.nativeElement.querySelector('[data-testid="search-area-button"]'),
    ).toBeNull();

    const mapDebugEl = fixture.debugElement.query(By.directive(DiscoveryMapComponent));
    const mapComponent = mapDebugEl.componentInstance as DiscoveryMapComponent;
    mapComponent.mapMoved.emit({ latitude: -34.0, longitude: -58.0 });
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('[data-testid="search-area-button"]');
    expect(button).not.toBeNull();
  });
});
