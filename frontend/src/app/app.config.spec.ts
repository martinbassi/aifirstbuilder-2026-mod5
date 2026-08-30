import { TestBed } from '@angular/core/testing';
import {
  API_BASE_URL,
  AuthClient,
  DiscoveryClient,
  ModerationClient,
  MuralsClient,
} from './core/api-client/api-client.generated';
import { appConfig } from './app.config';

describe('appConfig', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: appConfig.providers });
  });

  // Regression test for QUICK-FIX-001: DiscoveryClient was used by DiscoveryService but never
  // registered in appConfig.providers (unlike AuthClient/ModerationClient/MuralsClient), causing
  // NG0201 ("No provider found for DiscoveryClient") as soon as /discover loaded.
  it('resolves DiscoveryClient from the app-wide injector without throwing', () => {
    expect(() => TestBed.inject(DiscoveryClient)).not.toThrow();
  });

  it('resolves the other generated API clients from the app-wide injector without throwing', () => {
    expect(() => TestBed.inject(AuthClient)).not.toThrow();
    expect(() => TestBed.inject(ModerationClient)).not.toThrow();
    expect(() => TestBed.inject(MuralsClient)).not.toThrow();
  });

  // FEAT-012 Block 2: API_BASE_URL resuelve dinámicamente según el host desde el que se sirvió el
  // frontend. `window.location` no es reasignable directamente en jsdom sin `configurable: true`;
  // se mockea antes de inyectar (el factory corre recién al resolver el provider) y se restaura el
  // `location` original en `afterEach` (mismo cuidado de limpieza que
  // create-mural-form.component.spec.ts aplica con `vi.restoreAllMocks()` para spies globales).
  describe('API_BASE_URL dynamic resolution based on window.location.hostname', () => {
    const originalLocation = window.location;

    afterEach(() => {
      Object.defineProperty(window, 'location', { value: originalLocation, configurable: true });
    });

    it('resolves to the local HTTPS backend when served from localhost (FR-02/AC-09)', () => {
      Object.defineProperty(window, 'location', {
        value: { hostname: 'localhost' },
        configurable: true,
      });

      expect(TestBed.inject(API_BASE_URL)).toBe('https://localhost:7126');
    });

    it('resolves to the LAN HTTP backend on the same host when served from a non-localhost hostname (FR-05/AC-02)', () => {
      Object.defineProperty(window, 'location', {
        value: { hostname: '192.168.1.50' },
        configurable: true,
      });

      expect(TestBed.inject(API_BASE_URL)).toBe('http://192.168.1.50:5267');
    });
  });
});
