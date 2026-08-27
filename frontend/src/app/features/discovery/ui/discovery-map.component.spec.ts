import { ComponentFixture, TestBed } from '@angular/core/testing';
import * as L from 'leaflet';
import { NearbyMuralItemResponse } from '../../../core/api-client/api-client.generated';
import { DiscoveryMapComponent, MapCenter } from './discovery-map.component';

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

    const markerIcons = fixture.nativeElement.querySelectorAll(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    );
    expect(markerIcons.length).toBe(items.length);
  });

  it('items vacío no renderiza ningún marcador', () => {
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();

    const markerIcons = fixture.nativeElement.querySelectorAll(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    );
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

    const markerIcons = fixture.nativeElement.querySelectorAll(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    );
    const secondIcon = markerIcons[1] as HTMLElement;
    secondIcon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));

    expect(emitted).toEqual(items[1]);
  });

  // Reactividad: `discovery-page` carga `items` de forma asíncrona (Geolocation → fetch), por lo
  // que el mapa se monta primero con `items: []` y se actualiza cuando llega la respuesta.
  it('actualiza los marcadores cuando items cambia después del render inicial', () => {
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelectorAll('.leaflet-marker-icon:not(.discovery-visitor-marker)')
        .length,
    ).toBe(0);

    const items = [buildItem({ id: 'mural-1' })];
    fixture.componentRef.setInput('items', items);
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelectorAll('.leaflet-marker-icon:not(.discovery-visitor-marker)')
        .length,
    ).toBe(1);
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
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
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

  // Block 1 — recentrado reactivo del mapa (spec-FEAT-005).

  // Required test: dado el mapa ya renderizado con el centro de fallback, cuando el input `center`
  // cambia a un valor nuevo, `map.getCenter()` refleja el nuevo centro — valida AC-01.
  it('recentra el mapa cuando el input center cambia a un valor nuevo (AC-01)', () => {
    fixture.detectChanges();

    const newCenter: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', newCenter);
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    const center = map.getCenter();
    expect(center.lat).toBeCloseTo(newCenter.latitude, 5);
    expect(center.lng).toBeCloseTo(newCenter.longitude, 5);
  });

  // Required test: el mismo comportamiento se dispara sin importar si el cambio de `center()` viene
  // de geolocalización o de un segundo cambio posterior (simula el caso de coordenadas manuales) —
  // valida AC-02.
  it('recentra el mapa también ante un segundo cambio de center, ej. coordenadas manuales (AC-02)', () => {
    fixture.detectChanges();

    const geolocationCenter: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', geolocationCenter);
    fixture.detectChanges();

    const manualCenter: MapCenter = { latitude: -33.0, longitude: -57.0 };
    fixture.componentRef.setInput('center', manualCenter);
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    const center = map.getCenter();
    expect(center.lat).toBeCloseTo(manualCenter.latitude, 5);
    expect(center.lng).toBeCloseTo(manualCenter.longitude, 5);
  });

  // Required test: si `center()` se vuelve a fijar con el mismo valor que ya tiene aplicado,
  // `map.setView` no se vuelve a invocar (test de regresión anti-loop). Primero confirma que
  // `setView` SÍ se dispara ante un cambio real (si no, el segundo assert sería un falso positivo:
  // pasaría igual aunque no hubiera reactividad ninguna).
  it('no vuelve a llamar map.setView si center() se fija con el mismo valor ya aplicado (anti-loop)', () => {
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    const setViewSpy = vi.spyOn(map, 'setView');

    const center: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', center);
    fixture.detectChanges();
    expect(setViewSpy).toHaveBeenCalledTimes(1);

    fixture.componentRef.setInput('center', { latitude: -34.6, longitude: -58.4 });
    fixture.detectChanges();
    expect(setViewSpy).toHaveBeenCalledTimes(1);
  });

  // Required test: si `center()` cambia a `null` después de tener un valor, el `effect()` no llama
  // `applyCenter()` ni lanza — valida el caso documentado en "Error handling". También confirma
  // primero que `setView` SÍ se dispara ante el cambio previo, por la misma razón que el test
  // anterior.
  it('no llama applyCenter ni lanza cuando center() vuelve a null después de tener un valor', () => {
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    const setViewSpy = vi.spyOn(map, 'setView');

    const center: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', center);
    fixture.detectChanges();
    expect(setViewSpy).toHaveBeenCalledTimes(1);

    expect(() => {
      fixture.componentRef.setInput('center', null);
      fixture.detectChanges();
    }).not.toThrow();

    expect(setViewSpy).toHaveBeenCalledTimes(1);
    expect(map.getCenter().lat).toBeCloseTo(center.latitude, 5);
    expect(map.getCenter().lng).toBeCloseTo(center.longitude, 5);
  });

  // Block 2 — pin distintivo de "tu ubicación" (spec-FEAT-005).

  // Required test: cuando `center()` tiene un valor, aparece en el DOM un elemento con la clase
  // `discovery-visitor-marker`, distinguible de `.leaflet-marker-icon` (los marcadores de murales)
  // — valida AC-03.
  it('renderiza un marcador distintivo del visitante cuando center() tiene un valor (AC-03)', () => {
    const center: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', center);
    fixture.detectChanges();

    const visitorMarkers = fixture.nativeElement.querySelectorAll('.discovery-visitor-marker');
    expect(visitorMarkers.length).toBe(1);
  });

  // Required test: si `center()` cambia dos veces, sigue existiendo un único marcador de
  // visitante (no se acumulan) y su posición refleja el último centro.
  it('reposiciona el marcador de visitante en vez de duplicarlo cuando center() cambia dos veces', () => {
    fixture.detectChanges();

    const firstCenter: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', firstCenter);
    fixture.detectChanges();

    const secondCenter: MapCenter = { latitude: -33.0, longitude: -57.0 };
    fixture.componentRef.setInput('center', secondCenter);
    fixture.detectChanges();

    const visitorMarkers = fixture.nativeElement.querySelectorAll('.discovery-visitor-marker');
    expect(visitorMarkers.length).toBe(1);

    const visitorMarker = (component as unknown as { visitorMarker: L.Marker | null })
      .visitorMarker;
    expect(visitorMarker).not.toBeNull();
    const latLng = visitorMarker!.getLatLng();
    expect(latLng.lat).toBeCloseTo(secondCenter.latitude, 5);
    expect(latLng.lng).toBeCloseTo(secondCenter.longitude, 5);
  });

  // Block 3 — output `mapMoved` y botón "Buscar en esta área" (spec-FEAT-005).

  // Required test: simular un moveend iniciado por el usuario (map.panTo, no vía applyCenter()) y
  // verificar que mapMoved emite con el nuevo centro.
  it('un moveend iniciado por el usuario emite mapMoved con el nuevo centro', () => {
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    let emitted: MapCenter | undefined;
    component.mapMoved.subscribe((center) => {
      emitted = center;
    });

    map.panTo([-33.0, -57.0], { animate: false });

    expect(emitted).toBeDefined();
    expect(emitted!.latitude).toBeCloseTo(-33.0, 5);
    expect(emitted!.longitude).toBeCloseTo(-57.0, 5);
  });

  // Required test: cuando el recentrado lo dispara el propio componente (input `center` cambia,
  // Block 1), mapMoved NO emite — valida la guarda anti-loop.
  it('el recentrado programático vía center() NO emite mapMoved (guarda anti-loop)', () => {
    fixture.detectChanges();

    let emitted = false;
    component.mapMoved.subscribe(() => {
      emitted = true;
    });

    const newCenter: MapCenter = { latitude: -34.6, longitude: -58.4 };
    fixture.componentRef.setInput('center', newCenter);
    fixture.detectChanges();

    expect(emitted).toBe(false);
  });

  // Required test: un zoomend iniciado por el usuario también emite mapMoved con el centro vigente.
  it('un zoomend iniciado por el usuario emite mapMoved con el centro vigente', () => {
    fixture.detectChanges();

    const map = (component as unknown as { map: L.Map }).map;
    let emitted: MapCenter | undefined;
    component.mapMoved.subscribe((center) => {
      emitted = center;
    });

    map.setZoom(map.getZoom() + 1, { animate: false });

    expect(emitted).toBeDefined();
  });

  // Block 1 — popup del mapa (spec-FEAT-006).

  // Required test: click en un marcador abre un popup — AC-10.
  it('hacer click en un marcador abre un popup', () => {
    fixture.componentRef.setInput('items', [buildItem()]);
    fixture.detectChanges();

    const markerIcon = fixture.nativeElement.querySelector(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    ) as HTMLElement;
    markerIcon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const popupContent = fixture.nativeElement.querySelector('.leaflet-popup-content');
    expect(popupContent).toBeTruthy();
  });

  // Required test: el popup contiene el título del mural correspondiente — AC-10.
  it('el popup contiene el título del mural', () => {
    fixture.componentRef.setInput('items', [buildItem({ title: 'Mural del Cerro' })]);
    fixture.detectChanges();

    const markerIcon = fixture.nativeElement.querySelector(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    ) as HTMLElement;
    markerIcon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const popupContent = fixture.nativeElement.querySelector(
      '.leaflet-popup-content',
    ) as HTMLElement;
    expect(popupContent.textContent).toContain('Mural del Cerro');
  });

  // Required test: el popup contiene la fecha de creación formateada — AC-10.
  it('el popup contiene la fecha de creación formateada', () => {
    fixture.componentRef.setInput('items', [
      buildItem({ createdAt: new Date('2026-08-19T14:30:00Z') }),
    ]);
    fixture.detectChanges();

    const markerIcon = fixture.nativeElement.querySelector(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    ) as HTMLElement;
    markerIcon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const popupContent = fixture.nativeElement.querySelector(
      '.leaflet-popup-content',
    ) as HTMLElement;
    expect(popupContent.textContent).toContain('19/08/2026');
  });

  // Required test (seguridad, threat model FEAT-006, riesgo HIGH): un título con caracteres HTML
  // especiales se renderiza como TEXTO literal, nunca como HTML/elemento inyectado.
  it('un título con HTML se renderiza como texto literal, no como HTML inyectado', () => {
    const maliciousTitle = '<img src=x onerror=alert(1)>';
    fixture.componentRef.setInput('items', [buildItem({ title: maliciousTitle })]);
    fixture.detectChanges();

    const markerIcon = fixture.nativeElement.querySelector(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    ) as HTMLElement;
    markerIcon.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    const popupContent = fixture.nativeElement.querySelector(
      '.leaflet-popup-content',
    ) as HTMLElement;
    expect(popupContent.querySelector('img')).toBeNull();
    expect(popupContent.textContent).toContain(maliciousTitle);
  });

  // Required test (sad path): title/createdAt undefined no lanza ni rompe el render de otros
  // marcadores.
  it('un mural con title/createdAt undefined no lanza excepción al renderizar el popup', () => {
    const incomplete = buildItem({ id: 'mural-incomplete', title: undefined, createdAt: undefined });
    const complete = buildItem({ id: 'mural-complete', latitude: -34.61, longitude: -58.41 });

    expect(() => {
      fixture.componentRef.setInput('items', [incomplete, complete]);
      fixture.detectChanges();
    }).not.toThrow();

    const markerIcons = fixture.nativeElement.querySelectorAll(
      '.leaflet-marker-icon:not(.discovery-visitor-marker)',
    );
    expect(markerIcons.length).toBe(2);
  });
});
