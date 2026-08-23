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

  it('renderiza nz-card con la clase auth-card, y proyecta el contenido dentro vía ng-content', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    const card: HTMLElement = fixture.nativeElement.querySelector('nz-card.auth-card');
    expect(card).toBeTruthy();

    const projected: HTMLElement | null = card.querySelector('.projected-test-content');
    expect(projected).toBeTruthy();
    expect(projected?.textContent?.trim()).toBe('test content');
  });

  it('envuelve la card en un contenedor externo con la clase auth-card-page para el centrado', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    const page: HTMLElement = fixture.nativeElement.querySelector('.auth-card-page');
    expect(page).toBeTruthy();

    const card: HTMLElement | null = page.querySelector('nz-card.auth-card');
    expect(card).toBeTruthy();
  });
});
