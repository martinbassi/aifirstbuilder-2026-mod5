import { TestBed } from '@angular/core/testing';
import { LayoutStore } from './layout.store';

/**
 * `LayoutStore.collapsedSignal` reads `window.innerWidth` exactly once, at construction. To test
 * both breakpoint outcomes deterministically, each test must set `window.innerWidth` BEFORE
 * instantiating the store, and instantiate it through its own `TestBed.configureTestingModule` —
 * each call creates a fresh injector, so `TestBed.inject(LayoutStore)` always constructs a new
 * instance that reads the `innerWidth` already fixed for that test. Reusing a single `beforeEach`
 * instance across tests would read whichever width was set first and never re-evaluate it.
 */
function setInnerWidth(width: number): void {
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
}

describe('LayoutStore', () => {
  it('collapsed() es false cuando window.innerWidth es 992 o más al construirse', () => {
    setInnerWidth(992);
    TestBed.configureTestingModule({});
    const store = TestBed.inject(LayoutStore);

    expect(store.collapsed()).toBe(false);
  });

  it('collapsed() es true cuando window.innerWidth es menor a 992 al construirse', () => {
    setInnerWidth(991);
    TestBed.configureTestingModule({});
    const store = TestBed.inject(LayoutStore);

    expect(store.collapsed()).toBe(true);
  });

  it('toggle() invierte el valor de collapsed()', () => {
    setInnerWidth(1200);
    TestBed.configureTestingModule({});
    const store = TestBed.inject(LayoutStore);

    expect(store.collapsed()).toBe(false);

    store.toggle();
    expect(store.collapsed()).toBe(true);

    store.toggle();
    expect(store.collapsed()).toBe(false);
  });
});
