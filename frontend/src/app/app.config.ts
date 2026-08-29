import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { rehydrateSessionOnStartup } from './core/bootstrap/session-rehydration.initializer';
import { en_US, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import {
  CalendarOutline,
  CheckCircleOutline,
  CloseCircleOutline,
  CloudUploadOutline,
  CompassOutline,
  DeleteOutline,
  DownloadOutline,
  EnvironmentOutline,
  ExclamationCircleOutline,
  GoogleOutline,
  InfoCircleOutline,
  LogoutOutline,
  MenuFoldOutline,
  MenuUnfoldOutline,
  SafetyCertificateOutline,
  SearchOutline,
  UserOutline,
} from '@ant-design/icons-angular/icons';
import { registerLocaleData } from '@angular/common';
import en from '@angular/common/locales/en';
import {
  API_BASE_URL,
  AuthClient,
  DiscoveryClient,
  ModerationClient,
  MuralsClient,
} from './core/api-client/api-client.generated';
import { authInterceptor } from './core/interceptors/auth.interceptor';

registerLocaleData(en);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Rehidrata sessionStore.user() (rol) desde GET /api/auth/session al arrancar, antes de que el
    // router resuelva cualquier ruta protegida (FEAT-007, NFR-04/AC-07/AC-08).
    provideAppInitializer(rehydrateSessionOnStartup),
    // Backend local de desarrollo (Block 1, perfil `https` de launchSettings.json). Sin un archivo
    // de environments todavía (no lo introdujo Block 2) — mover a `environment.ts` es trabajo de un
    // futuro ticket que agregue configuración por entorno real (staging/producción).
    { provide: API_BASE_URL, useValue: 'https://localhost:7126' },
    AuthClient,
    ModerationClient,
    // Pre-existing gap fixed here (FEAT-001c Block 6, spec-flagged): MuralsClient was used by
    // MuralService but never registered here, causing a NullInjectorError in production — masked
    // in tests because mural.service.spec.ts provides MuralsClient manually.
    MuralsClient,
    // Pre-existing gap fixed here (QUICK-FIX-001): same class of bug as MuralsClient above —
    // DiscoveryClient was used by DiscoveryService but never registered here, causing NG0201 on
    // /discover as soon as it loaded.
    DiscoveryClient,
    provideNzI18n(en_US),
    // Íconos usados por nz-alert (mensajes de error de los formularios de auth, Block 8).
    provideNzIcons([
      CheckCircleOutline,
      CloseCircleOutline,
      ExclamationCircleOutline,
      InfoCircleOutline,
      GoogleOutline,
      // Íconos del shell de navegación global (Block 2/3/4, FEAT-004).
      CompassOutline,
      CloudUploadOutline,
      SafetyCertificateOutline,
      LogoutOutline,
      UserOutline,
      MenuFoldOutline,
      MenuUnfoldOutline,
      EnvironmentOutline,
      SearchOutline,
      // Pre-existing gap fixed here (FEAT-006): `discovery-list.component.html` usa
      // `nzType="calendar"` desde antes del rediseño Card→NzList, pero `CalendarOutline` nunca se
      // registró — el ícono de fecha estaba roto en producción (mismo patrón que MuralsClient/
      // DiscoveryClient arriba: sin este registro, `IconNotFoundError` en tiempo de ejecución).
      CalendarOutline,
      // Ícono de eliminar de nz-upload-list (create-mural-form, FEAT-008 Block 1): con
      // nzListType="picture" renderiza este botón de forma incondicional aunque el click todavía
      // no tenga handler.
      DeleteOutline,
      DownloadOutline,
    ]),
  ],
};
