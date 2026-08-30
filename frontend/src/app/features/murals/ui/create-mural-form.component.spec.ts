import { TestBed } from '@angular/core/testing';
import { provideNzIconsTesting } from 'ng-zorro-antd/icon/testing';
import { NzNotificationService } from 'ng-zorro-antd/notification';
import { NzUploadFile } from 'ng-zorro-antd/upload';
import { Subject, of, throwError } from 'rxjs';
import { AddressSuggestionDto, CreateMuralResponse } from '../../../core/api-client/api-client.generated';
import { GeolocationService } from '../../../shared/geolocation.service';
import { AddressService, AddressSuggestion } from '../data/address.service';
import { MuralService } from '../data/mural.service';
import { CreateMuralFormComponent } from './create-mural-form.component';

/**
 * `beforeUpload` es el único punto de alta de archivo desde Block 1 (`nz-upload` con
 * `nzBeforeUpload` devolviendo `false` de forma síncrona nunca dispara `nzChange` para el archivo
 * nuevo — ver Logic de spec-FEAT-008 Block 1). Los tests llaman a `beforeUpload` directamente en
 * vez de simular un file picker real (no simulable en jsdom), pasando el `File` tal cual: es
 * exactamente lo que `nz-upload` le pasa en producción (`ng-zorro-antd-upload.mjs` invoca
 * `beforeUpload` con el `File` crudo, no con un `NzUploadFile` envuelto — `originFileObj` sólo
 * existe si algo más arma la entrada a mano).
 */
function asUploadFile(file: File): NzUploadFile {
  return file as unknown as NzUploadFile;
}

function numberInputEvent(value: number): Event {
  const input = document.createElement('input');
  input.type = 'number';
  input.value = String(value);
  return { target: input } as unknown as Event;
}

// FIX-003: Title es obligatorio desde el commit 9cecf21 (`canSubmit` lo exige) — helper análogo a
// `numberInputEvent` para que los tests puedan simular su ingreso.
function titleInputEvent(value: string): Event {
  const input = document.createElement('input');
  input.type = 'text';
  input.value = value;
  return { target: input } as unknown as Event;
}

/**
 * Stubs the injected `GeolocationService` mock (Block 6) instead of `navigator.geolocation`
 * directly — the component no longer touches the browser API itself, it delegates to the
 * service. `getCurrentPosition()` now returns a `Promise`, so callers awaiting a resolved/rejected
 * test must flush a microtask (see `flushGeolocation` below) before asserting on the result.
 */
function stubGeolocation(
  geolocationService: { getCurrentPosition: ReturnType<typeof vi.fn> },
  behavior: 'success' | 'denied' | 'unsupported',
  position?: { latitude: number; longitude: number },
): void {
  if (behavior === 'success') {
    geolocationService.getCurrentPosition.mockReturnValue(
      Promise.resolve({
        latitude: position?.latitude ?? -34.6,
        longitude: position?.longitude ?? -58.4,
      }),
    );
    return;
  }

  // 'denied' and 'unsupported' both surface to the component as a rejection — which of the 3
  // typed `GeolocationError.kind` values it actually is does not matter to this component: it
  // reacts identically to any of them (falls back to manual input). `GeolocationService`'s own
  // spec is what verifies each `kind` is produced correctly.
  geolocationService.getCurrentPosition.mockReturnValue(
    Promise.reject({ kind: behavior === 'denied' ? 'denied' : 'unavailable' }),
  );
}

/** Flushes the microtask queue so the `GeolocationService.getCurrentPosition()` promise settles,
 * then re-runs change detection so the resulting signal updates reach the DOM. */
async function flushGeolocation(fixture: { detectChanges: () => void }): Promise<void> {
  await Promise.resolve();
  fixture.detectChanges();
}

describe('CreateMuralFormComponent', () => {
  let muralService: { create: ReturnType<typeof vi.fn> };
  let geolocationService: { getCurrentPosition: ReturnType<typeof vi.fn> };
  let addressService: { search: ReturnType<typeof vi.fn>; reverseGeocode: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    muralService = { create: vi.fn() };
    geolocationService = { getCurrentPosition: vi.fn() };
    // Default: sin resultados / sin match — cada test de Block 3 sobreescribe lo que necesita.
    // Ambos métodos deben existir en TODOS los tests (incluso los que no tocan direcciones), porque
    // `requestGeolocation()` siempre invoca `reverseGeocode()` tras un GPS exitoso (spec Block 3).
    addressService = {
      search: vi.fn().mockReturnValue(of([])),
      reverseGeocode: vi.fn().mockReturnValue(of(null)),
    };

    TestBed.configureTestingModule({
      imports: [CreateMuralFormComponent],
      providers: [
        { provide: MuralService, useValue: muralService },
        { provide: GeolocationService, useValue: geolocationService },
        { provide: AddressService, useValue: addressService },
        // nz-upload-list con nzListType="picture" renderiza incondicionalmente sus íconos (upload
        // button, delete, picture/file placeholder) — sin un registro de íconos, NzIconService
        // lanza IconNotFoundError apenas se hace detectChanges(). provideNzIconsTesting() registra
        // el set completo de @ant-design/icons-angular en vez de listar uno por uno (patrón usado
        // por login-form.component.spec.ts/register-form.component.spec.ts para íconos puntuales;
        // acá conviene el set completo porque nz-upload-list decide sus propios íconos
        // internamente, no los elige este componente).
        provideNzIconsTesting(),
      ],
    });
  });

  // `vi.spyOn(URL, 'revokeObjectURL')` (Block 2 tests) envuelve el método global sin restaurarlo
  // solo: sin este afterEach, los spies se anidan entre tests y sus contadores de llamadas se
  // arrastran de un test a otro.
  afterEach(() => {
    vi.restoreAllMocks();
  });

  // Required test (Block 1, AC-01): archivo válido (JPEG ≤10MB) → preview inmediato.
  it('selecciona un archivo válido y muestra el preview inmediatamente', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });

    const result = component.beforeUpload(asUploadFile(validFile));
    fixture.detectChanges();

    expect(result).toBe(false);
    expect(component.fileList()).toHaveLength(1);
    expect(component.fileList()[0].thumbUrl).toBeTruthy();
    // `fileError` es `signal<boolean>(false)` desde el commit 1965e1b (reemplazó el alert inline
    // por un toast de NzNotificationService) — este test verificaba el contrato viejo
    // (`string | null`), desactualizado.
    expect(component.fileError()).toBe(false);
    const previewImg: HTMLImageElement = fixture.nativeElement.querySelector(
      '[data-testid="photo-upload"] img',
    );
    expect(previewImg).toBeTruthy();
  });

  // Required test (Block 1, AC-04): archivo >10MB → error inline, sin preview.
  it('rechaza un archivo oversized con error inline y no arma preview', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const oversizedFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    Object.defineProperty(oversizedFile, 'size', { value: 11 * 1024 * 1024 });

    const result = component.beforeUpload(asUploadFile(oversizedFile));
    fixture.detectChanges();

    expect(result).toBe(false);
    // Ver comentario del test anterior: `fileError` es boolean desde 1965e1b, el mensaje ahora se
    // muestra por `NzNotificationService`, no en el signal.
    expect(component.fileError()).toBe(true);
    expect(component.fileList()).toHaveLength(0);
    const previewImg = fixture.nativeElement.querySelector('[data-testid="photo-upload"] img');
    expect(previewImg).toBeFalsy();
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test (Block 1, AC-05): archivo de tipo inválido (application/pdf) → error inline, sin preview.
  it('rechaza un archivo de tipo inválido con error inline y no arma preview', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const invalidTypeFile = new File(['%PDF-1.4'], 'wall.pdf', { type: 'application/pdf' });

    const result = component.beforeUpload(asUploadFile(invalidTypeFile));
    fixture.detectChanges();

    expect(result).toBe(false);
    // Ver comentario de los dos tests anteriores.
    expect(component.fileError()).toBe(true);
    expect(component.fileList()).toHaveLength(0);
  });

  // Required test (Block 1, AC-08): sin archivo seleccionado → "Guardar" deshabilitado.
  it('deshabilita Guardar cuando no hay ningún archivo seleccionado', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    fixture.detectChanges();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test 3: geolocalización exitosa → lat/lng se completan solos (AC-03).
  it('completa latitud y longitud automáticamente cuando la geolocalización tiene éxito', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6037, longitude: -58.3816 });

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.latitude()).toBe(-34.6037);
    expect(component.longitude()).toBe(-58.3816);
    expect(component.manualLocationRequired()).toBe(false);
    const manualInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    expect(manualInput).toBeFalsy();
  });

  // Required test 4: geolocalización denegada → aparecen inputs manuales, formulario sigue usable (AC-04).
  it('revela inputs manuales cuando la geolocalización es denegada, sin bloquear el formulario', async () => {
    stubGeolocation(geolocationService, 'denied');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.manualLocationRequired()).toBe(true);
    const latitudeInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    const longitudeInput = fixture.nativeElement.querySelector('[data-testid="longitude-input"]');
    expect(latitudeInput).toBeTruthy();
    expect(longitudeInput).toBeTruthy();

    // El resto del formulario (selector de foto) sigue disponible.
    const photoUpload = fixture.nativeElement.querySelector('[data-testid="photo-upload"]');
    expect(photoUpload).toBeTruthy();
  });

  // Required test 5: ingreso manual válido → "Guardar" habilitado (AC-05).
  it('habilita Guardar cuando el ingreso manual de coordenadas es válido junto con una foto válida', async () => {
    stubGeolocation(geolocationService, 'unsupported');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.canSubmit()).toBe(false);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
    component.onLatitudeChange(numberInputEvent(-34.6));
    component.onLongitudeChange(numberInputEvent(-58.4));
    fixture.detectChanges();

    expect(component.canSubmit()).toBe(true);
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(false);
  });

  // Required test 6: envío exitoso → notificación de confirmación (AC-12).
  //
  // Test desactualizado corregido (no el código): el commit 1965e1b reemplazó el `nz-alert`
  // inline (`data-testid="success-message"`) por un toast de `NzNotificationService` — este test
  // seguía buscando el elemento viejo, que ya no existe en el HTML. Se verifica el contrato nuevo
  // (la notificación se dispara con el mensaje esperado) en vez de reintroducir el DOM removido.
  it('muestra el mensaje de confirmación tras un envío exitoso', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });
    muralService.create.mockReturnValue(
      of(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' })),
    );

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const notificationService = TestBed.inject(NzNotificationService);
    const createSpy = vi.spyOn(notificationService, 'create');

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
    fixture.detectChanges();

    component.submit();
    fixture.detectChanges();

    expect(createSpy).toHaveBeenCalledWith(
      'success',
      'Notificación',
      expect.stringContaining('pendiente de revisión'),
    );
  });

  // Required test 7: envío fallido → foto y ubicación se conservan, "Reintentar" vuelve a enviar
  // sin pedir datos de nuevo (AC-11).
  //
  // Regresión de 1965e1b corregida en este bloque: el commit sacó del HTML el bloque
  // `@if (errorMessage(); as message)` con `data-testid="retry-button"`, dejando sin forma de
  // disparar `retry()` desde la UI aunque el método siguiera existiendo. Ahora el error de guardado
  // se muestra vía `NzNotificationService.template()` (consistente con el resto del rediseño de
  // 1965e1b, que ya usa notificaciones para éxito/rechazo) con un botón de acción dentro de la
  // notificación (mismo testid). `NzNotificationService` renderiza en un overlay de CDK adjunto a
  // `document.body`, fuera del árbol del componente — el botón se busca en `document`, no en
  // `fixture.nativeElement`.
  it('conserva foto y ubicación tras un envío fallido y reintenta sin pedir datos de nuevo', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });
    muralService.create
      .mockReturnValueOnce(throwError(() => ({ status: 500, message: 'No se pudo guardar el mural. Intentá nuevamente.' })))
      .mockReturnValueOnce(of(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' })));

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
    fixture.detectChanges();

    component.submit();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('No se pudo guardar el mural. Intentá nuevamente.');
    expect(component.selectedFile()).toBe(validFile);
    expect(component.latitude()).toBe(-34.6);
    expect(component.longitude()).toBe(-58.4);
    const retryButton: HTMLButtonElement | null = document.querySelector(
      '[data-testid="retry-button"]',
    );
    expect(retryButton).toBeTruthy();

    const notificationService = TestBed.inject(NzNotificationService);
    const createSpy = vi.spyOn(notificationService, 'create');

    retryButton!.click();
    fixture.detectChanges();

    expect(muralService.create).toHaveBeenCalledTimes(2);
    expect(muralService.create).toHaveBeenNthCalledWith(2, {
      photo: validFile,
      title: 'Mural de prueba',
      latitude: -34.6,
      longitude: -58.4,
    });
    // Mismo cambio de contrato que el test 6: el reintento exitoso confirma vía notificación, no
    // vía un `data-testid="success-message"` en el DOM del componente (ver comentario del test 6).
    expect(component.errorMessage()).toBeNull();
    expect(createSpy).toHaveBeenCalledWith(
      'success',
      'Notificación',
      expect.stringContaining('pendiente de revisión'),
    );
  });

  // Required test (Block 2, AC-02): reemplazar el archivo revoca el thumbUrl anterior antes de
  // asignar el nuevo.
  it('revoca el thumbUrl anterior al reemplazar el archivo seleccionado', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');
    const firstFile = new File(['x'], 'wall-1.jpg', { type: 'image/jpeg' });
    const secondFile = new File(['y'], 'wall-2.jpg', { type: 'image/jpeg' });

    component.beforeUpload(asUploadFile(firstFile));
    fixture.detectChanges();
    const firstUrl = component.fileList()[0].thumbUrl;

    component.beforeUpload(asUploadFile(secondFile));
    fixture.detectChanges();

    expect(component.fileList()).toHaveLength(1);
    expect(component.fileList()[0].originFileObj).toBe(secondFile);
    expect(revokeSpy).toHaveBeenCalledWith(firstUrl);
  });

  // Required test (Block 2, AC-03): click en el ícono de eliminar de nz-upload-list limpia el
  // estado (fileList, selectedFile, fileError) y deshabilita "Guardar" de nuevo.
  it('limpia el estado cuando se elimina el archivo desde nz-upload-list', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    fixture.detectChanges();

    // `.ant-upload-list-item-card-actions-btn` matches BOTH the download and the delete buttons
    // (nz-upload-list renders both when `nzShowUploadList` is truthy) — narrow down to the one
    // whose icon is `nz-icon[nzType="delete"]` (rendered as class `anticon-delete`), never the
    // first match, or this would click "download" instead.
    const removeButton: HTMLButtonElement | null = fixture.nativeElement.querySelector(
      '.ant-upload-list-item-card-actions-btn:has(.anticon-delete)',
    );
    expect(removeButton).toBeTruthy();
    removeButton!.click();
    fixture.detectChanges();

    expect(component.fileList()).toHaveLength(0);
    expect(component.selectedFile()).toBeNull();
    // Ver comentario de los tests de fileError más arriba (contrato boolean desde 1965e1b).
    expect(component.fileError()).toBe(false);
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    expect(submitButton.disabled).toBe(true);
  });

  // Required test (Block 2, AC-06): el botón "Guardar" muestra nzLoading hasta que el submit
  // resuelve, y ninguna subida real se dispara antes del submit explícito.
  it('mantiene nzLoading durante el submit sin disparar ninguna subida antes del envío explícito', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });
    const createSubject = new Subject<CreateMuralResponse>();
    muralService.create.mockReturnValue(createSubject.asObservable());

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));
    fixture.detectChanges();

    expect(muralService.create).not.toHaveBeenCalled();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="submit-button"]',
    );
    submitButton.click();
    fixture.detectChanges();

    expect(muralService.create).toHaveBeenCalledTimes(1);
    expect(component.submitting()).toBe(true);
    expect(submitButton.classList.contains('ant-btn-loading')).toBe(true);

    createSubject.next(new CreateMuralResponse({ id: 'mural-1', status: 'Pending' }));
    createSubject.complete();
    fixture.detectChanges();

    expect(component.submitting()).toBe(false);
    expect(submitButton.classList.contains('ant-btn-loading')).toBe(false);
  });

  // Required test (Block 2, AC-07/NFR-01): reemplazar el archivo y destruir el componente revoca
  // el thumbUrl tanto en el reemplazo como en el destroy.
  it('revoca el thumbUrl en el reemplazo y en el destroy del componente', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();

    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');
    const firstFile = new File(['x'], 'wall-1.jpg', { type: 'image/jpeg' });
    const secondFile = new File(['y'], 'wall-2.jpg', { type: 'image/jpeg' });

    component.beforeUpload(asUploadFile(firstFile));
    fixture.detectChanges();
    const firstUrl = component.fileList()[0].thumbUrl;

    component.beforeUpload(asUploadFile(secondFile));
    fixture.detectChanges();
    const secondUrl = component.fileList()[0].thumbUrl;

    expect(revokeSpy).toHaveBeenCalledWith(firstUrl);

    fixture.destroy();

    expect(revokeSpy).toHaveBeenCalledWith(secondUrl);
    expect(revokeSpy).toHaveBeenCalledTimes(2);
  });

  // Edge case (no-op seguro): destruir el componente sin haber seleccionado nunca un archivo no
  // debe lanzar excepción ni invocar URL.revokeObjectURL.
  it('no lanza excepción ni revoca nada al destruir sin haber seleccionado un archivo', () => {
    stubGeolocation(geolocationService, 'success');
    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    fixture.detectChanges();

    const revokeSpy = vi.spyOn(URL, 'revokeObjectURL');

    expect(() => fixture.destroy()).not.toThrow();
    expect(revokeSpy).not.toHaveBeenCalled();
  });

  // ── spec-FEAT-011 Block 3: campo de dirección con autocomplete ──────────────────────────────

  // Required test 1: escribir en el campo de dirección dispara search() tras el debounce de
  // 300ms, no antes (NFR-04/AC-17).
  it('escribir en el campo de dirección dispara search() tras el debounce de 300ms, no antes', async () => {
    vi.useFakeTimers();
    try {
      stubGeolocation(geolocationService, 'unsupported');

      const fixture = TestBed.createComponent(CreateMuralFormComponent);
      const component = fixture.componentInstance;
      fixture.detectChanges();
      await flushGeolocation(fixture);

      component.onAddressQueryChange(titleInputEvent('Av. 18 de Julio'));
      expect(addressService.search).not.toHaveBeenCalled();

      vi.advanceTimersByTime(299);
      expect(addressService.search).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1);
      expect(addressService.search).toHaveBeenCalledWith('Av. 18 de Julio');
    } finally {
      vi.useRealTimers();
    }
  });

  // Required test 2: seleccionar una sugerencia setea latitude/longitude y llama a
  // setCoordinatesInMap() (AC-05/AC-21).
  it('seleccionar una sugerencia setea latitude/longitude y llama a setCoordinatesInMap()', async () => {
    // GPS exitoso: garantiza que el `div#location-preview` que `setCoordinatesInMap()` necesita
    // ya está en el DOM (rama `@else` del template) antes de seleccionar la sugerencia.
    stubGeolocation(geolocationService, 'success', { latitude: -34.6, longitude: -58.4 });

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const setCoordinatesSpy = vi.spyOn(component as any, 'setCoordinatesInMap');

    const suggestion: AddressSuggestion = new AddressSuggestionDto({
      address: 'Av. 18 de Julio 1234',
      latitude: -34.9,
      longitude: -56.16,
    });
    component.onAddressSuggestionSelected(suggestion);

    expect(component.latitude()).toBe(-34.9);
    expect(component.longitude()).toBe(-56.16);
    expect(setCoordinatesSpy).toHaveBeenCalledWith({ latitude: -34.9, longitude: -56.16 });
  });

  // Required test 3: search() sin coincidencias muestra el estado "sin resultados" sin marcar
  // addressProviderUnavailable (AC-18). Verifica el DOM real, no solo el signal — el hallazgo del
  // loop correctivo de VERIFY fue justamente que el signal se vaciaba pero nada se lo indicaba al
  // usuario en pantalla.
  it('search() sin coincidencias muestra el mensaje de "sin resultados" en el DOM sin marcar addressProviderUnavailable', async () => {
    vi.useFakeTimers();
    try {
      stubGeolocation(geolocationService, 'unsupported');
      addressService.search.mockReturnValue(of([]));

      const fixture = TestBed.createComponent(CreateMuralFormComponent);
      const component = fixture.componentInstance;
      fixture.detectChanges();
      await flushGeolocation(fixture);

      component.onAddressQueryChange(titleInputEvent('Dirección inexistente'));
      vi.advanceTimersByTime(300);
      fixture.detectChanges();

      expect(component.addressSuggestions()).toEqual([]);
      expect(component.addressProviderUnavailable()).toBe(false);
      const noResultsMessage = fixture.nativeElement.querySelector(
        '[data-testid="address-no-results"]',
      );
      expect(noResultsMessage).toBeTruthy();
      expect(noResultsMessage.textContent).toContain('No encontramos direcciones que coincidan');
    } finally {
      vi.useRealTimers();
    }
  });

  // Regresión del fix de AC-18: una dirección precompletada por GPS/reverse geocoding nunca pasó
  // por el pipeline de búsqueda (el usuario no escribió nada) — no debe mostrar "sin resultados"
  // antes de que el usuario efectivamente busque algo.
  it('una dirección precompletada por GPS no muestra "sin resultados" antes de que el usuario busque', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.9, longitude: -56.16 });
    addressService.reverseGeocode.mockReturnValue(
      of({ address: 'Av. 18 de Julio 1234', latitude: -34.9, longitude: -56.16 }),
    );

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);
    fixture.detectChanges();

    expect(component.addressQuery()).toBe('Av. 18 de Julio 1234');
    expect(component.addressSuggestions()).toEqual([]);
    const noResultsMessage = fixture.nativeElement.querySelector(
      '[data-testid="address-no-results"]',
    );
    expect(noResultsMessage).toBeNull();
  });

  // Required test 4: search() con error 503 setea addressProviderUnavailable y revela los inputs
  // manuales de lat/lng, sin bloquear el resto del formulario (AC-19, sad path).
  it('search() con error 503 setea addressProviderUnavailable y revela los inputs manuales de lat/lng', async () => {
    vi.useFakeTimers();
    try {
      stubGeolocation(geolocationService, 'unsupported');
      addressService.search.mockReturnValue(
        throwError(() => ({ status: 503, message: 'El servicio de direcciones no está disponible.' })),
      );

      const fixture = TestBed.createComponent(CreateMuralFormComponent);
      const component = fixture.componentInstance;
      fixture.detectChanges();
      await flushGeolocation(fixture);

      component.onAddressQueryChange(titleInputEvent('Av. 18 de Julio'));
      vi.advanceTimersByTime(300);
      fixture.detectChanges();

      expect(component.addressProviderUnavailable()).toBe(true);
      const latitudeInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
      const longitudeInput = fixture.nativeElement.querySelector('[data-testid="longitude-input"]');
      expect(latitudeInput).toBeTruthy();
      expect(longitudeInput).toBeTruthy();
      // El mensaje de "sin resultados" (AC-18) es un caso distinto de "proveedor caído" (AC-19) —
      // no deben mostrarse juntos.
      expect(
        fixture.nativeElement.querySelector('[data-testid="address-no-results"]'),
      ).toBeNull();

      // El resto del formulario sigue disponible.
      const photoUpload = fixture.nativeElement.querySelector('[data-testid="photo-upload"]');
      expect(photoUpload).toBeTruthy();
    } finally {
      vi.useRealTimers();
    }
  });

  // Required test 5: con permiso de geolocalización otorgado, reverseGeocode() exitoso precompleta
  // el campo de dirección (AC-03).
  it('con permiso de geolocalización otorgado, reverseGeocode() exitoso precompleta el campo de dirección', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.9, longitude: -56.16 });
    addressService.reverseGeocode.mockReturnValue(
      of({ address: 'Av. 18 de Julio 1234', latitude: -34.9, longitude: -56.16 }),
    );

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);
    fixture.detectChanges();

    expect(addressService.reverseGeocode).toHaveBeenCalledWith(-34.9, -56.16);
    expect(component.addressQuery()).toBe('Av. 18 de Julio 1234');
  });

  // Required test 6: con permiso de geolocalización otorgado pero reverseGeocode() con error 503,
  // el flujo GPS sigue funcionando (lat/lng seteados, mapa con pin) sin precompletar el texto
  // (sad path).
  it('con reverseGeocode() en error 503 el flujo GPS sigue funcionando sin precompletar el texto', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.9, longitude: -56.16 });
    addressService.reverseGeocode.mockReturnValue(
      throwError(() => ({ status: 503, message: 'El servicio de direcciones no está disponible.' })),
    );

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);
    fixture.detectChanges();

    expect(component.latitude()).toBe(-34.9);
    expect(component.longitude()).toBe(-56.16);
    expect(component.addressQuery()).toBe('');
    // El error de reverseGeocode durante el flujo GPS NO es el mismo caso que un error de
    // search(): no debe marcar el fallback manual (spec Block 3, error handling table).
    expect(component.addressProviderUnavailable()).toBe(false);
    const mapContainer = fixture.nativeElement.querySelector('#location-preview');
    expect(mapContainer).toBeTruthy();
  });

  // Required test 7: con permiso de geolocalización otorgado pero reverseGeocode() devuelve null
  // (sin match), el campo de dirección queda vacío y el mapa igual muestra el pin de GPS
  // (sad path).
  it('con reverseGeocode() devolviendo null el campo de dirección queda vacío y el mapa muestra el pin de GPS', async () => {
    stubGeolocation(geolocationService, 'success', { latitude: -34.9, longitude: -56.16 });
    addressService.reverseGeocode.mockReturnValue(of(null));

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);
    fixture.detectChanges();

    expect(component.addressQuery()).toBe('');
    expect(component.latitude()).toBe(-34.9);
    expect(component.longitude()).toBe(-56.16);
    const mapContainer = fixture.nativeElement.querySelector('#location-preview');
    expect(mapContainer).toBeTruthy();
  });

  // Required test 8: con permiso de geolocalización denegado, se muestra el fallback manual por
  // manualLocationRequired (comportamiento existente, regresión).
  it('con geolocalización denegada se muestra el fallback manual por manualLocationRequired (regresión)', async () => {
    stubGeolocation(geolocationService, 'denied');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    expect(component.manualLocationRequired()).toBe(true);
    expect(component.addressProviderUnavailable()).toBe(false);
    const latitudeInput = fixture.nativeElement.querySelector('[data-testid="latitude-input"]');
    const longitudeInput = fixture.nativeElement.querySelector('[data-testid="longitude-input"]');
    expect(latitudeInput).toBeTruthy();
    expect(longitudeInput).toBeTruthy();
    // El campo de dirección con autocomplete sigue disponible incluso con el fallback manual
    // revelado (spec Block 3: ambos caminos coexisten).
    const addressInput = fixture.nativeElement.querySelector('[data-testid="address-input"]');
    expect(addressInput).toBeTruthy();
  });

  // Required test 9: canSubmit() sigue validando los rangos de lat/lng sin importar el origen del
  // valor (regresión).
  it('canSubmit() sigue validando los rangos de lat/lng sin importar el origen del valor (regresión)', async () => {
    stubGeolocation(geolocationService, 'unsupported');

    const fixture = TestBed.createComponent(CreateMuralFormComponent);
    const component = fixture.componentInstance;
    fixture.detectChanges();
    await flushGeolocation(fixture);

    const validFile = new File(['x'], 'wall.jpg', { type: 'image/jpeg' });
    component.beforeUpload(asUploadFile(validFile));
    component.onTitleChange(titleInputEvent('Mural de prueba'));

    const withinRange: AddressSuggestion = new AddressSuggestionDto({
      address: 'Av. 18 de Julio 1234',
      latitude: -34.9,
      longitude: -56.16,
    });
    component.onAddressSuggestionSelected(withinRange);
    fixture.detectChanges();

    expect(component.canSubmit()).toBe(true);

    const outOfRange: AddressSuggestion = new AddressSuggestionDto({
      address: 'Fuera de rango',
      latitude: 999,
      longitude: 999,
    });
    component.onAddressSuggestionSelected(outOfRange);
    fixture.detectChanges();

    expect(component.canSubmit()).toBe(false);
  });
});
