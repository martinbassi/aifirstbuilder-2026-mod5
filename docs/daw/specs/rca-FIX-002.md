# RCA FIX-002: Assets rotos en discovery (marcadores Leaflet, CSP local, fallback center, foto sin max-width)

| Campo | Valor |
|-------|-------|
| Ticket | FIX-002 |
| Fecha | 2026-08-25 |
| Reportado por | Usuario, al probar la app localmente tras el merge de FEAT-003 |

## Síntoma

La pantalla de discovery (mapa/lista de murales cercanos) es inutilizable al correr la app en
local: el mapa no muestra ningún marcador, cuando no hay ubicación GPS el mapa abre centrado en
medio del océano, las fotos de los murales cargados contra el emulador local de Azure Storage no
se ven, y la foto de detalle de un mural desborda el layout del panel.

## Root cause (4 defectos independientes, mismo síntoma agregado)

### 1. Marcadores del mapa invisibles

`L.Icon.Default.prototype._getIconUrl` (Leaflet) resuelve las rutas de los íconos por defecto del
marcador (`marker-icon.png`, `marker-icon-2x.png`, `marker-shadow.png`) inspeccionando la URL del
propio script de Leaflet en el bundle — una técnica que asume el layout de assets de un bundler
clásico tipo webpack. Con el bundler de Angular 21 (esbuild), esa resolución produce una URL
inválida y las tres imágenes dan 404: el mapa se renderiza sin ningún pin visible, aunque los datos
de murales sí llegan. Es un problema documentado de Leaflet combinado con bundlers ESM modernos, no
un bug de lógica propia — `frontend/src/app/features/discovery/ui/discovery-map.component.ts` nunca
sobreescribió `L.Icon.Default.mergeOptions(...)` para apuntar a assets propios servidos como
estáticos.

### 2. Mapa centrado en "null island" sin ubicación

`FALLBACK_CENTER` en el mismo archivo usaba `{ latitude: 0, longitude: 0 }` — coordenadas que caen
en el golfo de Guinea, sin relación con la app. Cuando el usuario deniega el permiso de
geolocalización o no hay resultados que ubiquen el mapa, éste abre en un punto sin murales ni
contexto, en vez de un centro razonable para desarrollo/demo.

### 3. Fotos de murales no cargan contra el storage local

`frontend/src/index.html` declara una Content-Security-Policy con `img-src 'self' data:
https://*.tile.openstreetmap.org` — sin el origen del emulador local de Azure Storage (Azurite,
`http://127.0.0.1:10000`), que es de donde el backend sirve las fotos de murales subidas en
desarrollo. El navegador bloquea toda etiqueta `<img>` que apunte a ese origen por violar la CSP,
sin generar ningún error de red visible salvo en la consola. Quedó fuera cuando FIX-001 configuró
CORS/CSP para el tráfico API↔frontend, sin contemplar el storage local usado por Azurite.

### 4. Layout roto en el detalle de un mural

`frontend/src/app/features/discovery/ui/discovery-list.component.html` renderiza
`<img [src]="item.photoUrl" ... />` en el panel de detalle sin ninguna restricción de tamaño. Una
foto subida a resolución nativa (hasta el límite de 10 MB de RNF-003) se muestra a su tamaño real,
desbordando el panel y rompiendo el layout del resto de la pantalla.

## Componente afectado

`frontend/src/app/features/discovery/ui/discovery-map.component.ts`,
`frontend/src/app/features/discovery/ui/discovery-list.component.html`, `frontend/src/index.html`
(CSP).

## Por qué no se detectó en FEAT-001d ni en VERIFY

Los tests de discovery (Vitest) montan los componentes en jsdom, sin motor de renderizado real de
imágenes ni aplicación de CSP por parte del navegador — un `<img>` con `src` roto o bloqueado no
falla ningún assert existente, y los tests de mapa no verifican la carga real de los tiles/íconos de
Leaflet (fuera del alcance práctico de un test unitario). El gap solo se manifiesta corriendo la app
real contra un navegador, sirviendo fotos desde el storage local — exactamente lo que reveló el
síntoma.

## PRD relacionado

`docs/daw/prd/prd-FEAT-001d.md` — revisado completo. FR-03/AC-06 solo exigen "un marcador por cada
mural en su ubicación correspondiente"; no especifican rutas de assets, política CSP ni restricciones
de tamaño de imagen. Sin gap de PRD que resolver: son detalles de implementación, no requisitos
funcionales no cubiertos.

## Confirmación

Confirmado por el usuario el 2026-08-25.
