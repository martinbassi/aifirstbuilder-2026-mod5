# Fix-plan FIX-002: Assets rotos en discovery (marcadores Leaflet, CSP local, fallback center, foto sin max-width)

| Campo | Valor |
|-------|-------|
| Ticket | FIX-002 |
| Tier | FIX |
| RCA | docs/daw/specs/rca-FIX-002.md |
| Date | 2026-08-25 |
| Spec loops | 0 |

## Problem

La pantalla de discovery (mapa/lista de murales cercanos) es inutilizable al correr la app en
local: el mapa no muestra ningún marcador, sin ubicación GPS el mapa abre centrado en medio del
océano, las fotos de murales cargadas contra el emulador local de Azure Storage no se ven, y la
foto de detalle de un mural desborda el layout del panel. El mismo problema de foto sin límite de
tamaño aparece también en la pantalla de moderación (hallazgo del impact scan de esta fase).

## Root cause

4 causas raíz independientes, detalladas en `docs/daw/specs/rca-FIX-002.md`: resolución de íconos
de Leaflet incompatible con el bundler de Angular, `FALLBACK_CENTER` en `(0,0)`, CSP sin el origen
del storage local, y `<img>` de foto sin restricción de tamaño.

## Solution — steps

1. `frontend/src/app/features/discovery/ui/discovery-map.component.ts` — antes de la definición de
   `MapCenter`, agregar el override de íconos por defecto de Leaflet:
   ```typescript
   delete (L.Icon.Default.prototype as any)._getIconUrl;

   L.Icon.Default.mergeOptions({
     iconRetinaUrl: 'images/leaflet/marker-icon-2x.png',
     iconUrl: 'images/leaflet/marker-icon.png',
     shadowUrl: 'images/leaflet/marker-shadow.png',
   });
   ```
   y cambiar `FALLBACK_CENTER`:
   ```typescript
   const FALLBACK_CENTER: MapCenter = { latitude: -34.905830, longitude: -56.191388 };
   ```
   (Montevideo, en vez de `{ latitude: 0, longitude: 0 }`.)

2. `frontend/src/app/features/discovery/ui/discovery-list.component.html` — el `<img
   data-testid="item-photo">` del panel de detalle agrega `style="max-width: 300px;"`:
   ```html
   <img style="max-width: 300px;" [src]="item.photoUrl" alt="Foto del mural" data-testid="item-photo" />
   ```

3. `frontend/src/app/features/moderation/ui/pending-murals-list.component.html` — mismo defecto
   detectado por el impact scan de esta fase en la pantalla de moderación (FEAT-001c): el `<img
   data-testid="mural-photo">` agrega el mismo estilo, por consistencia:
   ```html
   <img style="max-width: 300px;" [src]="mural.photoUrl" alt="Foto del mural pendiente" data-testid="mural-photo" />
   ```

4. `frontend/src/index.html` — revertir la CSP `img-src` a su valor original (sin el origen de
   Azurite); este archivo queda como el usado por defecto, incluida la configuración `production`:
   ```
   img-src 'self' data: https://*.tile.openstreetmap.org
   ```

5. `frontend/src/index.development.html` (nuevo archivo) — copia de `index.html` con la CSP
   `img-src` ampliada al origen del emulador local de Azure Storage (Azurite):
   ```
   img-src 'self' data: https://*.tile.openstreetmap.org http://127.0.0.1:10000
   ```

6. `frontend/angular.json` — en `projects.frontend.architect.build.configurations.development`,
   agregar:
   ```json
   "index": "src/index.development.html"
   ```
   Mitigación R1 del threat model. Nota de implementación (CODE): `fileReplacements` — lo planeado
   originalmente en PLAN — resultó inválido para este caso: el schema del builder de Angular solo lo
   permite sobre archivos `.ts`/`.js`/`.json` (pensado para `environment.ts`), no sobre `index.html`.
   La opción `index` del builder sí es overrideable por configuración y es el mecanismo que Angular
   documenta para esto — mismo resultado (`ng serve`, que usa `development` por default, sirve
   `index.development.html` con la CSP relajada; `ng build`, que usa `production` por default, sigue
   empaquetando `index.html` sin el origen de Azurite), mecanismo corregido. Deja el mismo patrón
   preparado para que `staging`/`production`, cuando se definan como configuraciones propias, sumen
   su propio `index.{env}.html` con su propio override de `index`.

7. `frontend/public/images/leaflet/marker-icon.png`, `marker-icon-2x.png`, `marker-shadow.png`,
   `layers.png`, `layers-2x.png` — agregar a git (ya existen en el working tree sin trackear;
   confirmado sin ningún `.gitignore` que los excluya).

## Dependencies between steps

Los pasos 1–3 (discovery-map, discovery-list, pending-murals-list) son independientes entre sí. Los
pasos 4–6 (separación de `index.html` por entorno) están relacionados: 4 y 5 deben aplicarse juntos
antes de 6 (si `angular.json` referencia `index.development.html` antes de que exista, el build
falla). El paso 7 es independiente — solo agrega archivos binarios ya presentes en el working tree.

## Error handling

- Si `frontend/public/images/leaflet/*.png` faltaran en el build (por ejemplo, un `git add`
  incompleto), Leaflet vuelve a su comportamiento roto original (marcadores invisibles, 404 en
  consola) — mismo síntoma que antes del fix, sin excepción ni crash: es un defecto visual
  detectable a simple vista, no un error silencioso de lógica.
- Si `angular.json` referenciara `index.development.html` sin que el archivo exista, `ng serve`
  falla al arrancar con un error explícito de Angular CLI (archivo no encontrado) — no hay
  degradación silenciosa.
- Ningún otro código de error nuevo: los cambios son assets estáticos, una constante de UI y un
  atributo de presentación, sin lógica de negocio ni llamadas a la API involucradas.

## Tests

- [ ] **Regression test** — `discovery-map.component.spec.ts`,
  `renders_leaflet_markers_with_the_project's_own_icon_assets`: monta el componente con al menos un
  mural en `items`, y afirma que el marcador renderizado en el DOM usa
  `images/leaflet/marker-icon.png` como `src` del ícono (no la URL rota por defecto de Leaflet).
  Reproduce el bug original: sin el fix, Leaflet intenta resolver una URL que 404.
- [ ] **Regression test** — `discovery-map.component.spec.ts`,
  `centers_the_map_on_Montevideo_when_there_is_no_location`: sin ubicación GPS ni resultados,
  afirma que el centro del mapa es `{ latitude: -34.905830, longitude: -56.191388 }`, no `{0, 0}`.
- [ ] **Regression test** — `discovery-list.component.spec.ts`,
  `constrains_the_detail_photo_to_a_maximum_width`: selecciona un item y afirma que el `<img
  data-testid="item-photo">` tiene `max-width: 300px` aplicado.
- [ ] **Regression test** — `pending-murals-list.component.spec.ts`,
  `constrains_the_mural_photo_to_a_maximum_width`: afirma lo mismo sobre `<img
  data-testid="mural-photo">`.
- [ ] **Verificación manual** (no automatizable con Vitest/jsdom — requiere un navegador real y CSP
  aplicada por el browser): correr `ng serve` + backend local con Azurite, confirmar visualmente
  marcadores visibles en el mapa, fotos de murales cargando, mapa centrado en Montevideo sin
  ubicación, y correr `ng build` (producción) para confirmar que el `index.html` de `dist/` no
  incluye `http://127.0.0.1:10000` en su CSP.

## Regression risk

**Bajo.** Los cambios se limitan a assets estáticos, una constante de UI, un atributo de
presentación y configuración de build (`fileReplacements`) — sin tocar lógica de negocio, llamadas a
la API, ni el mecanismo de URLs firmadas (SAS) que controla el acceso a fotos de murales
pendientes/rechazados (RNF-009). El único punto con más superficie que el resto —la separación de
`index.html` por entorno— es un mecanismo estándar de Angular, ya usado tal cual en la mayoría de
los proyectos Angular con múltiples entornos.

## Rollback plan

- **Trivial: revertir el commit.** El fix es directo, sin bloques — un único commit en CODE. Todos
  los cambios son aditivos o de reemplazo de valores (íconos, coordenadas, CSP, `fileReplacements`);
  revertir el commit deja el comportamiento exactamente como estaba antes del fix (discovery
  inutilizable en local, mismo síntoma original).
- **Indicadores para aplicarlo:** si `ng build` (producción) empezara a fallar por la nueva entrada
  de `fileReplacements`, o si `index.development.html` divergiera de `index.html` en algo más que la
  CSP (indicando que alguien empezó a mantenerlos como dos archivos separados en vez de una copia
  sincronizada, lo cual necesitaría su propio ticket de mejora de build, no un revert de este fix).
