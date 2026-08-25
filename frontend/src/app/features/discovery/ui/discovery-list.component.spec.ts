import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';
import { DiscoveryListComponent } from './discovery-list.component';

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

describe('DiscoveryListComponent', () => {
  let fixture: ComponentFixture<DiscoveryListComponent>;
  let component: DiscoveryListComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DiscoveryListComponent] });
    fixture = TestBed.createComponent(DiscoveryListComponent);
    component = fixture.componentInstance;
  });

  // Required test: orden respetado tal como llega — no reordena, aunque no venga ascendente.
  it('respeta el orden recibido de items, sin reordenar por distanceKm', () => {
    const items = [
      buildItem({ id: 'mural-far', distanceKm: 4.5 }),
      buildItem({ id: 'mural-near', distanceKm: 0.5 }),
      buildItem({ id: 'mural-mid', distanceKm: 2.0 }),
    ];
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();

    const renderedIds = Array.from(
      fixture.nativeElement.querySelectorAll('[data-testid^="discovery-item-"]'),
    ).map((el) => (el as HTMLElement).getAttribute('data-testid'));

    expect(renderedIds).toEqual([
      'discovery-item-mural-far',
      'discovery-item-mural-near',
      'discovery-item-mural-mid',
    ]);
  });

  // Required test: selección de un ítem muestra su detalle inline (foto/fecha/ubicación, AC-04).
  it('seleccionar un ítem muestra su detalle inline: foto, fecha y ubicación (AC-04)', () => {
    const items = [
      buildItem({
        id: 'mural-1',
        photoUrl: 'https://storage.example.com/mural-photos/mural-1.jpg?sas=token',
        latitude: -34.6037,
        longitude: -58.3816,
        createdAt: new Date('2026-08-19T00:00:00Z'),
      }),
    ];
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="item-detail"]')).toBeNull();

    let emitted: NearbyMuralItemResponse | undefined;
    component.muralSelected.subscribe((mural) => {
      emitted = mural;
    });

    const itemElement = fixture.nativeElement.querySelector(
      '[data-testid="discovery-item-mural-1"]',
    ) as HTMLElement;
    itemElement.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const detail = fixture.nativeElement.querySelector('[data-testid="item-detail"]');
    expect(detail).not.toBeNull();

    const photo = fixture.nativeElement.querySelector(
      '[data-testid="item-photo"]',
    ) as HTMLImageElement;
    expect(photo.src).toBe('https://storage.example.com/mural-photos/mural-1.jpg?sas=token');
    // Regression test (FIX-002, RCA causa raíz #4): sin restricción de tamaño, una foto a
    // resolución nativa desborda el panel de detalle.
    expect(photo.style.maxWidth).toBe('300px');

    const createdAt = fixture.nativeElement.querySelector('[data-testid="item-created-at"]');
    expect(createdAt.textContent).toContain('2026');

    const location = fixture.nativeElement.querySelector('[data-testid="item-location"]');
    expect(location.textContent).toContain('-34.6037');
    expect(location.textContent).toContain('-58.3816');

    expect(emitted).toEqual(items[0]);
  });

  // Required test: `items: []` muestra el mensaje de sin resultados (AC-06), sin botón de
  // ampliar radio (Out of Scope, RF-021).
  it('items vacío muestra el mensaje de sin resultados, sin botón de ampliar radio (AC-06)', () => {
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const emptyMessage = fixture.nativeElement.querySelector('[data-testid="empty-message"]');
    expect(emptyMessage).not.toBeNull();
    expect(emptyMessage.textContent).toContain(
      'No se encontraron murales publicados en este radio',
    );

    const expandRadiusButton = fixture.nativeElement.querySelector(
      '[data-testid="expand-radius-button"]',
    );
    expect(expandRadiusButton).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('ampliar');
  });
});
