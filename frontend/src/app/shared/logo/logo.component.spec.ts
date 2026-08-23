import { TestBed } from '@angular/core/testing';
import { LogoComponent } from './logo.component';

describe('LogoComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LogoComponent],
    }).compileComponents();
  });

  it('renderiza un <img> apuntando a /images/logo.jpg con un alt no vacío', () => {
    const fixture = TestBed.createComponent(LogoComponent);
    fixture.detectChanges();

    const img: HTMLImageElement = fixture.nativeElement.querySelector('img');

    expect(img).toBeTruthy();
    expect(img.getAttribute('src')).toBe('/images/logo.jpg');
    expect(img.getAttribute('alt')?.trim()).not.toBe('');
  });
});
