# Spec FEAT-002: Identidad visual: tipografía Quicksand, logo y paleta de colores

| Field | Value |
|-------|-------|
| Ticket | FEAT-002 |
| PRD | docs/daw/prd/prd-FEAT-002.md |
| Tier | FEATURE |
| Date | 2026-08-23 |
| Spec loops | 0 |

## Summary

Cuatro cambios transversales de identidad visual, sin tocar lógica de negocio: (1) tipografía
Quicksand self-hosted como fuente global; (2) paleta de colores (primario `#FE6944`, secundario
`#0D2348`) aplicada sobreescribiendo variables CSS de theming de ng-zorro (ADR-006), con impacto
automático en todos los componentes existentes; (3) un componente `LogoComponent` compartido,
usado en las pantallas `/login` y `/register`; (4) favicon reemplazado por el ícono del logo.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 |
| FR-02 | Block 1 |
| FR-03 | Block 3 |
| FR-04 | Block 3 |
| FR-05 | Block 4 |
| FR-06 | Block 2 |
| FR-07 | Block 2 |
| NFR-01 | Strategy: archivos de fuente servidos como asset estático de `'self'` desde `public/fonts/` — la CSP actual (`default-src 'self'`) ya cubre `font-src` por fallback, sin necesidad de ampliarla (Block 1) |

## Dependencies between blocks

Ninguna estricta — los 4 bloques son independientes entre sí (tocan archivos distintos, sin que uno
necesite que otro esté terminado). Orden sugerido: 1 → 2 → 3 → 4, de menor a mayor superficie de
archivos tocados.

## Block 1 — Tipografía Quicksand self-hosted

**Files**
- `frontend/public/fonts/quicksand-400.woff2` (new)
- `frontend/public/fonts/quicksand-700.woff2` (new)
- `frontend/src/styles.css` (modified) — `@font-face` (pesos 400 y 700) + `font-family` global

**Logic**
Descargar los archivos WOFF2 de Quicksand (pesos regular 400 y bold 700) y colocarlos en
`public/fonts/` (se sirven tal cual vía el glob `**/*` ya configurado en `angular.json` → `assets`).
Declarar en `styles.css`:
```css
@font-face {
  font-family: 'Quicksand';
  src: url('/fonts/quicksand-400.woff2') format('woff2');
  font-weight: 400;
  font-display: swap;
}
@font-face {
  font-family: 'Quicksand';
  src: url('/fonts/quicksand-700.woff2') format('woff2');
  font-weight: 700;
  font-display: swap;
}
html, body {
  font-family: 'Quicksand', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
}
```
`font-display: swap` evita bloquear el render mientras la fuente carga (mitigación de UX, no exigida
por un AC pero consistente con NFR-01).

**Error handling**
- Si los archivos `.woff2` no se sirven correctamente (404), el navegador cae automáticamente al
  stack de fallback (`-apple-system, ...`) — comportamiento nativo de `font-family`, sin necesidad
  de manejo explícito en código.

**Required tests**
- [ ] `styles.spec` (nuevo, o test de un componente ya renderizado): `getComputedStyle(document.body).fontFamily` contiene `'Quicksand'` — valida AC-01.
- [ ] Verificación estática (grep/test simple): `frontend/src/index.html` no contiene ningún `<link>` a `fonts.googleapis.com` ni `fonts.gstatic.com` — valida AC-01 (sin dependencia de red a terceros).
- [ ] Verificación estática: el `font-family` declarado en `styles.css` incluye un stack de fallback después de `'Quicksand'` (no es una lista de un solo elemento) — valida AC-02 por inspección del CSS, ya que el comportamiento de fallback real del navegador ante una fuente faltante no es reproducible de forma determinística en un test unitario con jsdom/happy-dom.

**Completion criterion**
Los tests pasan y una inspección visual del build servido confirma que el texto se renderiza con
Quicksand (curvas redondeadas características), sin ningún request de red hacia dominios de Google
en las DevTools.

## Block 2 — Paleta de colores vía theming de ng-zorro (ADR-006)

**Files**
- `frontend/angular.json` (modified) — `styles`: `ng-zorro-antd.min.css` → `ng-zorro-antd.variable.css`
- `frontend/src/styles.css` (modified) — overrides de variables en `:root`

**Logic**
Ver `docs/adr/adr-006-ng-zorro-theming-css-variables.md` para la decisión y su justificación. En
`styles.css`:
```css
:root {
  --ant-primary-color: #FE6944;
  --ant-primary-color-hover: #FE9A81;
  --ant-primary-color-active: #FE3807;
  --ant-primary-color-outline: rgba(254, 105, 68, 0.2);

  --app-color-secondary: #0D2348;
  --app-color-secondary-hover: #163C7C;
  --app-color-secondary-active: #071225;
}
```
No se sobreescriben `--ant-success-color`/`--ant-error-color`/`--ant-warning-color` (fuera de
alcance del PRD, que solo define primario/secundario).

**Error handling**
- No aplica (cambio de configuración estática, sin lógica de runtime).

**Required tests**
- [ ] Test de componente (usando cualquier pantalla con un `nz-button nzType="primary"`, p. ej.
  `login-form.component.spec.ts`): el color de fondo computado del botón coincide con `#FE6944`
  (o su forma `rgb(254, 105, 68)`) — valida AC-06.
- [ ] Verificación estática: `styles.css` define `--ant-primary-color` y `--app-color-secondary`
  dentro de un bloque `:root` (no dentro de una clase CSS aislada aplicada a un componente puntual)
  — valida AC-07.

**Completion criterion**
El test de color de botón pasa, y una inspección visual de al menos dos pantallas ya existentes
(login, discovery) confirma que los componentes `nz-button`/`nz-alert`/`nz-pagination` heredan la
nueva paleta sin haber sido tocados individualmente.

## Block 3 — Logo compartido en login y register

**Files**
- `frontend/public/images/logo.jpg` (new) — logo comprimido/redimensionado (mitigación R2 del threat model: no usar el 1024×1024 original tal cual)
- `frontend/src/app/shared/logo/logo.component.ts` (new) — standalone, `templateUrl`
- `frontend/src/app/shared/logo/logo.component.html` (new)
- `frontend/src/app/shared/logo/logo.component.spec.ts` (new)
- `frontend/src/app/features/auth/ui/login-form.component.ts` (modified) — agregar `LogoComponent` al array `imports`
- `frontend/src/app/features/auth/ui/login-form.component.html` (modified) — agregar `<app-logo>`
- `frontend/src/app/features/auth/ui/register-form.component.ts` (modified) — agregar `LogoComponent` al array `imports`
- `frontend/src/app/features/auth/ui/register-form.component.html` (modified) — agregar `<app-logo>`

**Logic**
`LogoComponent` es puramente presentacional (sin `@Input()`, sin lógica, sin llamadas a servicios):
un `<img src="/images/logo.jpg" alt="paretto — urban art discovery">` en su template. Se ubica en
`shared/` porque lo consumen dos features distintas (`auth/login` y `auth/register`), siguiendo la
convención de AGENTS.md ("shared solo para lo genuinamente reusable entre features") — confirmado
por el arch-auditor en PLAN.

Ambos formularios (`login-form`, `register-form`) son componentes standalone: `LogoComponent` debe
agregarse a su array `imports` en el `.ts`, no alcanza con tocar el `.html` (si no, Angular falla en
build con `NG8001`, elemento desconocido) — gap detectado por el impact scanner en PLAN.

**Error handling**
- Si `logo.jpg` no carga (404), el navegador muestra el `alt` text — comportamiento nativo de
  `<img>`, sin manejo adicional necesario.

**Required tests**
- [ ] `logo.component.spec.ts`: renderiza un `<img>` con `src` apuntando a `/images/logo.jpg` y un
  `alt` no vacío (cubre el caso documentado en Error handling: si la imagen no carga, el `alt` es
  lo único que queda visible).
- [ ] `login-form.component.spec.ts` (actualizado): el DOM renderizado contiene `app-logo` — valida
  AC-03.
- [ ] `register-form.component.spec.ts` (actualizado): el DOM renderizado contiene `app-logo`, con
  el mismo componente que en login (mismo `alt`, mismo `src`) — valida AC-04.

**Completion criterion**
Los 3 tests pasan y ambas pantallas (`/login`, `/register`) muestran el logo al navegarlas
manualmente.

## Block 4 — Favicon

**Files**
- `frontend/public/favicon.ico` (modified) — reemplazado por el ícono del logo (aerosol + pin, sin
  el texto "paretto")

**Logic**
Recortar la región del ícono del archivo de logo original (sin el texto inferior) con un script
Python/PIL ejecutado una sola vez (one-off, fuera del pipeline de build y del repo — no se commitea
ningún script, solo el `favicon.ico` binario resultante, per el WARN del arch-auditor en PLAN),
generando un `.ico` multi-resolución (16×16, 32×32, 48×48) que reemplaza el archivo actual.

**Error handling**
- No aplica (asset estático, sin lógica de runtime).

**Required tests**
- [ ] **Verificación manual (VERIFY)** — no hay forma razonable de testear el contenido visual de un
  `favicon.ico` con el stack de testing actual (Vitest/jsdom no renderiza el ícono de una pestaña de
  navegador): abrir la app en el navegador tras el build y confirmar visualmente que la pestaña
  muestra el ícono del logo (aerosol/pin), no el placeholder genérico de Angular — valida AC-05.
- [ ] Verificación estática: el archivo `favicon.ico` commiteado difiere en bytes del placeholder
  original de Angular (comparable con `git diff --stat` sobre el binario) — confirma que el
  reemplazo ocurrió, aunque no valida el contenido visual por sí solo.

**Completion criterion**
El archivo `favicon.ico` commiteado es distinto (en bytes) al placeholder original, y una
verificación visual en el navegador confirma el ícono correcto.

## Final verification

- Los 4 bloques completos y sus tests en verde.
- `ng build --configuration production` no supera el budget de error (1.1MB) ya ajustado en
  FEAT-001d — mitigación R2 del threat model, medido al cierre de CODE.
- Inspección visual de al menos: `/login`, `/register`, `/discover`, `/moderation` — confirmar que
  ningún componente quedó con contraste roto tras el cambio de paleta (riesgo declarado en el PRD,
  sección "Risks and Mitigations").
