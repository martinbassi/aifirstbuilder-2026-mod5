import { TestBed } from '@angular/core/testing';
import { AuthClient, DiscoveryClient, ModerationClient, MuralsClient } from './core/api-client/api-client.generated';
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
});
