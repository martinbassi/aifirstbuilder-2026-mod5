import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthCardComponent } from './auth-card.component';

@Component({
  standalone: true,
  imports: [AuthCardComponent],
  template: `<app-auth-card><p class="projected-test-content">test content</p></app-auth-card>`,
})
class HostComponent {}

describe('AuthCardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
    }).compileComponents();
  });

  it('renderiza .login-page con sus dos hijos directos .brand-panel y .form-panel', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    const page: HTMLElement = fixture.nativeElement.querySelector('.login-page');
    expect(page).toBeTruthy();

    const brandPanel = page.querySelector(':scope > .brand-panel');
    expect(brandPanel).toBeTruthy();

    const formPanel = page.querySelector(':scope > .form-panel');
    expect(formPanel).toBeTruthy();
  });

  it('el .brand-panel contiene el wordmark de texto, el mensaje de marca y la imagen .brand-image', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    const brandPanel: HTMLElement = fixture.nativeElement.querySelector('.brand-panel');
    expect(brandPanel).toBeTruthy();

    expect(brandPanel.textContent).toContain('Paretto');

    const brandMessage = brandPanel.querySelector('.brand-message');
    expect(brandMessage).toBeTruthy();

    const brandImage = brandPanel.querySelector('.brand-image img');
    expect(brandImage).toBeTruthy();
  });

  it('.form-panel .login-container proyecta el contenido recibido vía ng-content y tiene la clase de max-width', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    const container: HTMLElement = fixture.nativeElement.querySelector(
      '.form-panel .login-container',
    );
    expect(container).toBeTruthy();

    const projected = container.querySelector('.projected-test-content');
    expect(projected).toBeTruthy();
    expect(projected?.textContent?.trim()).toBe('test content');
  });

  it('no deja rastro del bloque carousel-dots (ni siquiera comentado) en el markup de .brand-panel', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    const brandPanel: HTMLElement = fixture.nativeElement.querySelector('.brand-panel');
    expect(brandPanel).toBeTruthy();
    expect(brandPanel.innerHTML).not.toContain('carousel-dots');
  });
});
