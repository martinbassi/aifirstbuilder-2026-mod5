import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { en_US, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { registerLocaleData } from '@angular/common';
import en from '@angular/common/locales/en';

registerLocaleData(en);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // El interceptor real (adjuntar el token de sesión) se implementa en Block 8.
    provideHttpClient(withInterceptors([])),
    provideNzI18n(en_US),
    // Sin íconos concretos todavía: no hay UI de negocio en este bloque (Block 8 los agrega).
    provideNzIcons([]),
  ],
};
