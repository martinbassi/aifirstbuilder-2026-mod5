import { ComponentFixture, TestBed } from '@angular/core/testing';
import * as L from 'leaflet';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';
import { DiscoveryMapComponent } from './discovery-map.component';

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

describe('DiscoveryMapComponent', () => {
  let fixture: ComponentFixture<DiscoveryMapComponent>;
  let component: DiscoveryMapComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DiscoveryMapComponent] });
    fixture = TestBed.createComponent(DiscoveryMapComponent);
    component = fixture.componentInstance;
  });

  // Required test: un marcador por mural en `items` (AC-03).
  it('renderiza un marcador por cada mural en items (AC-03)', () => {
    const items = [
      buildItem({ id: 'mural-1', latitude: -34.6, longitude: -58.4 }),
      buildItem({ id: 'mural-2', latitude: -34.61, longitude: -58.41 }),
      buildItem({ id: 'mural-3', latitude: -34.62, longitude: -58.42 }),
    ];
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();

    const markerIcons = fixture.nativeElement.querySelectorAll('.leaflet-marker-icon');
    expect(markerIcons.length).toBe(items.length);
  });

  it('items vacío no renderiza ningún marcador', () => {
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const markerIcons = fixture.nativeElement.querySelectorAll('.leaflet-marker-icon');
    expect(markerIcons.length).toBe(0);
  });

  // Required test: seleccionar un marcador emite el mural correspondiente.
  it('seleccionar un marcador emite el mural correspondiente vía muralSelected', () => {
    const items = [
      buildItem({ id: 'mural-1', latitude: -34.6, longitude: -58.4 }),
      buildItem({ id: 'mural-2', latitude: -34.61, longitude: -58.41 }),
    ];
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();

    let emitted: NearbyMuralItemResponse | undefined;
    component.muralSelected.subscribe((mural) => {
      emitted = mural;
    });

    const markerIcons = fixture.nativeElement.querySelectorAll('.leaflet-marker-icon');
    const secondIcon = markerIcons[1] as HTMLElement;
    secondIcon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));

    expect(emitted).toEqual(items[1]);
  });

  // Reactividad: `discovery-page` carga `items` de forma asíncrona (Geolocation → fetch), por lo
  // que el mapa se monta primero con `items: []` y se actualiza cuando llega la respuesta.
  it('actualiza los marcadores cuando items cambia después del render inicial', () => {
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.leaflet-marker-icon').length).toBe(0);

    const items = [buildItem({ id: 'mural-1' })];
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.leaflet-marker-icon').length).toBe(1);
  });

  // Regression test (FIX-002, RCA causa raíz #1): Leaflet resuelve los íconos por defecto
  // inspeccionando la URL del propio script del bundle, algo que rompe con esbuild (Angular 21) y
  // deja los marcadores sin ícono visible (404). Sin el override de `L.Icon.Default.mergeOptions`
  // en la carga del módulo, el `src` del marcador apuntaría a la URL rota de Leaflet en vez de a
  // los assets propios del proyecto.
  it('los marcadores usan los íconos propios del proyecto, no los rotos por defecto de Leaflet (FIX-002)', () => {
    fixture.componentRef.setInput('items', [buildItem()]);
    fixture.detectChanges();

    const markerIcon = fixture.nativeElement.querySelector(
      '.leaflet-marker-icon',
    ) as HTMLImageElement;
    expect(markerIcon.src).toContain('images/leaflet/marker-icon.png');
  });

  // Regression test (FIX-002, RCA causa raíz #2): sin `center` ni `items` con coordenadas,
  // `resolveCenter()` cae a `FALLBACK_CENTER`. Antes del fix ese valor era `{0, 0}` ("null
  // island"); el mapa debe abrir centrado en Montevideo en su lugar.
  it('centra el mapa en Montevideo cuando no hay center ni items con ubicación (FIX-002)', () => {
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    const center = map.getCenter();
    expect(center.lat).toBeCloseTo(-34.90583, 5);
    expect(center.lng).toBeCloseTo(-56.191388, 5);
  });
});
