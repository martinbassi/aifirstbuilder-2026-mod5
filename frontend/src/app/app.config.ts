import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { en_US, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import {
  CheckCircleOutline,
  CloseCircleOutline,
  ExclamationCircleOutline,
  InfoCircleOutline,
} from '@ant-design/icons-angular/icons';
import { registerLocaleData } from '@angular/common';
import en from '@angular/common/locales/en';
import { API_BASE_URL, AuthClient } from './core/api-client/api-client.generated';
import { authInterceptor } from './core/interceptors/auth.interceptor';

registerLocaleData(en);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Backend local de desarrollo (Block 1, perfil `https` de launchSettings.json). Sin un archivo
    // de environments todavía (no lo introdujo Block 2) — mover a `environment.ts` es trabajo de un
    // futuro ticket que agregue configuración por entorno real (staging/producción).
    { provide: API_BASE_URL, useValue: 'https://localhost:7126' },
    AuthClient,
    provideNzI18n(en_US),
    // Íconos usados por nz-alert (mensajes de error de los formularios de auth, Block 8).
    provideNzIcons([
      CheckCircleOutline,
      CloseCircleOutline,
      ExclamationCircleOutline,
      InfoCircleOutline,
    ]),
  ],
};
