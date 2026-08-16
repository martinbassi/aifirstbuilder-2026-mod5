import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { API_BASE_URL } from '../api-client/api-client.generated';
import { SessionStore } from '../../features/auth/state/session.store';
import { authInterceptor } from './auth.interceptor';

// Misma base que la configurada en app.config.ts para el backend local (Block 8).
const TEST_API_BASE_URL = 'https://localhost:7126';

describe('authInterceptor', () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;
  let sessionStore: SessionStore;
  let router: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    router = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        SessionStore,
        { provide: Router, useValue: router },
        { provide: API_BASE_URL, useValue: TEST_API_BASE_URL },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);
    sessionStore = TestBed.inject(SessionStore);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // Required test 4: adjunta el header Authorization cuando hay token, no lo adjunta cuando no hay sesión.
  it('adjunta el header Authorization cuando session.store tiene un token', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    http.get(`${TEST_API_BASE_URL}/api/whatever`).subscribe();

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/api/whatever`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-abc');
    request.flush({});
  });

  it('no adjunta el header Authorization cuando no hay sesión activa', () => {
    http.get(`${TEST_API_BASE_URL}/api/whatever`).subscribe();

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/api/whatever`);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('ante un 401, limpia la sesión y redirige a /login', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    http.get(`${TEST_API_BASE_URL}/api/whatever`).subscribe({ error: () => undefined });

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/api/whatever`);
    request.flush('unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(sessionStore.isAuthenticated()).toBe(false);
    expect(sessionStore.token()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('ante un error que no es 401, no toca la sesión', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    http.get(`${TEST_API_BASE_URL}/api/whatever`).subscribe({ error: () => undefined });

    const request = httpMock.expectOne(`${TEST_API_BASE_URL}/api/whatever`);
    request.flush('server error', { status: 500, statusText: 'Internal Server Error' });

    expect(sessionStore.isAuthenticated()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  // Hallazgo 2 (ronda de corrección 3): el header Authorization solo debe viajar hacia la API
  // propia (API_BASE_URL), nunca hacia un origen externo (CDN, analytics, etc.) — evita una fuga
  // del token si en el futuro se agrega una llamada a un tercero.
  it('no adjunta el header Authorization a un origen externo, pero sigue adjuntándolo hacia API_BASE_URL', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    http.get('https://otrodominio.com/algo').subscribe();
    http.get(`${TEST_API_BASE_URL}/api/whatever`).subscribe();

    const externalRequest = httpMock.expectOne('https://otrodominio.com/algo');
    expect(externalRequest.request.headers.has('Authorization')).toBe(false);
    externalRequest.flush({});

    const ownApiRequest = httpMock.expectOne(`${TEST_API_BASE_URL}/api/whatever`);
    expect(ownApiRequest.request.headers.get('Authorization')).toBe('Bearer token-abc');
    ownApiRequest.flush({});
  });

  // Hallazgo de seguridad (daw-arch-auditor, corrección puntual sobre Block 8): el filtro de
  // "request hacia la API propia" comparaba con `startsWith`, un prefix match de string, no un
  // origen real. Un dominio adversario que empieza igual que API_BASE_URL (p. ej.
  // "https://localhost:71260.evil.com") matcheaba el prefijo y se llevaba el header Authorization.
  it('no adjunta el header Authorization a un origen que spoofea el prefijo de API_BASE_URL', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    http.get('https://localhost:71260.evil.com/algo').subscribe();

    const spoofedRequest = httpMock.expectOne('https://localhost:71260.evil.com/algo');
    expect(spoofedRequest.request.headers.has('Authorization')).toBe(false);
    spoofedRequest.flush({});
  });

  // Variante del mismo hallazgo con userinfo (`user@host`): también matchea `startsWith` contra
  // API_BASE_URL pero resuelve a un origen distinto (evil.com) — a diferencia del caso anterior,
  // esta sí es una URL válida según WHATWG y por lo tanto un vector explotable en un navegador real.
  it('no adjunta el header Authorization a un origen que spoofea via userinfo (user@host)', () => {
    sessionStore.setSession('token-abc', { username: 'ana' });

    http.get(`${TEST_API_BASE_URL}@evil.com/algo`).subscribe();

    const spoofedRequest = httpMock.expectOne(`${TEST_API_BASE_URL}@evil.com/algo`);
    expect(spoofedRequest.request.headers.has('Authorization')).toBe(false);
    spoofedRequest.flush({});
  });
});
