# Spec FEAT-006: Mostrar título y fecha de creación al hacer click en un marcador del mapa de /discover

| Field | Value |
|-------|-------|
| Ticket | FEAT-006 |
| PRD | docs/daw/prd/prd-FEAT-001d.md |
| Tier | FEATURE |
| Date | 2026-08-26 |
| Spec loops | 0 |

## Summary

Dos bloques independientes en el feature `discovery` (frontend). Block 1 agrega un popup de Leaflet
(título + fecha de creación) al hacer click en un marcador del mapa — hoy no hace nada, el output
`muralSelected` existe pero su handler en `discovery-page` está vacío a propósito. El popup se
construye exclusivamente vía DOM API (`createElement`/`textContent`), nunca interpolando el título
del mural como string HTML, para evitar un XSS almacenado (título = texto libre de usuario, sin
sanitización backend). Block 2 completa AC-04 en la lista: el rediseño Card→NzList (`d65842f`) ya
muestra título/foto/distancia/ubicación/fecha en cada fila sin necesidad de click, pero dejó 2 tests
probando un panel de detalle que ya no existe, y una señal de estado (`selectedItem`) sin
consumidores — se corrigen los tests y se elimina el código muerto.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-09 / AC-10 (popup del mapa, nuevo) | Block 1 |
| FR-04 / AC-04 (título en la lista) | Block 2 (el campo ya se renderiza; el bloque corrige la cobertura de test) |

## Dependencies between blocks

Ninguna — Block 1 (`discovery-map.component.*`) y Block 2 (`discovery-list.component.*`) tocan
componentes distintos y pueden implementarse en cualquier orden.

## Block 1 — Popup del mapa

**Files**
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (modified)

**Logic**

En `renderMarkers()`, además del `marker.on('click', () => this.muralSelected.emit(item))` ya
existente (sin cambios), agregar:

```ts
const popupContent = document.createElement('div');
const titleEl = document.createElement('strong');
titleEl.textContent = item.title ?? '';
const dateEl = document.createElement('div');
dateEl.textContent = formatDate(item.createdAt as Date, 'dd/MM/yyyy HH:mm', 'en-US', 'UTC-3');
popupContent.append(titleEl, dateEl);
marker.bindPopup(popupContent);
```

`formatDate` se importa de `@angular/common` (función pura — sin agregar `providers:` ni `inject()`
al componente, a diferencia de un `DatePipe` inyectado, que el arch-auditor de PLAN señaló como un
patrón sin precedente en el proyecto para uso imperativo puntual). Mismo formato y huso horario que
ya usa `discovery-list.component.html` como pipe de template (`'dd/MM/yyyy HH:mm' : 'UTC-3'`);
locale `'en-US'` porque el proyecto no registra un `LOCALE_ID` explícito (default de Angular, el
mismo que usaría `DatePipe`). Leaflet abre el popup automáticamente al click sobre el marker en
cuanto tiene un popup bindeado — no hace falta lógica adicional para mostrarlo.

**CRÍTICO DE SEGURIDAD** (threat model FEAT-006, riesgo HIGH mitigado por diseño — ver
`docs/daw/security/threat-FEAT-006.md`): el popup se construye EXCLUSIVAMENTE vía DOM API
(`document.createElement` + `.textContent`). Está PROHIBIDO interpolar `item.title` en un string
HTML pasado a `bindPopup()`/`setContent()`. Confirmado en el código fuente de Leaflet
(`leaflet-src.js`, `Popup._updateContent`): un `string` se inserta vía `node.innerHTML = content`
(XSS con el título de un mural, texto libre no sanitizado por el backend más allá de
`NotEmpty()`/`MaximumLength(50)`); un `HTMLElement` se inserta vía `appendChild` (seguro, sin
parsear HTML). Este es un criterio de aceptación del bloque, no un detalle de implementación
opcional.

**Input validation**

N/A — este bloque no acepta input nuevo del usuario. `item.title`/`item.createdAt` ya vienen
validados y persistidos desde FEAT-001b (`CreateMuralCommandValidator`).

**Error handling**

Si `item.title` o `item.createdAt` vienen `undefined` (`NearbyMuralItemResponse` los tipa
opcionales), usar fallback vacío para el título (`?? ''`, ya incluido en el snippet de arriba) — el
render de un marker con datos incompletos no debe lanzar ni interrumpir el render de los demás
markers.

**Required tests**

- [ ] click en un marcador abre un popup (`.leaflet-popup-content` presente en el DOM) — AC-10
- [ ] el popup contiene el título del mural correspondiente (verificado vía `textContent`, no vía
  `innerHTML`/regex sobre HTML) — AC-10
- [ ] el popup contiene la fecha de creación formateada (`dd/MM/yyyy HH:mm`) — AC-10
- [ ] **(sad path / regression test de seguridad)** un mural con título conteniendo caracteres HTML
  especiales (ej. `<img src=x onerror=alert(1)>`) se renderiza en el popup como TEXTO literal: no
  aparece ningún `<img>` nuevo en el DOM del popup, y el texto completo — incluidos los símbolos
  `<`/`>` — está presente como `textContent`
- [ ] **(sad path)** un mural con `title`/`createdAt` `undefined` no lanza excepción al renderizar
  el popup ni impide que se rendericen los demás marcadores (usa el fallback de string vacío)

**Completion criterion**

Los 4 tests nuevos pasan; los tests ya existentes de `discovery-map.component.spec.ts` (marcadores,
`muralSelected`, recentrado, visitante, `mapMoved`) siguen pasando sin cambios.

## Block 2 — Completar AC-04 en la lista + limpieza

**Files**
- `frontend/src/app/features/discovery/ui/discovery-list.component.html` (modified)
- `frontend/src/app/features/discovery/ui/discovery-list.component.ts` (modified)
- `frontend/src/app/features/discovery/ui/discovery-list.component.spec.ts` (modified)

**Logic**

1. `discovery-list.component.html`: agregar `data-testid="empty-message"` al `<nz-list-empty>` ya
   existente — un atributo no reconocido como `@Input` de Angular se aplica al elemento host del
   componente, sin lógica nueva. Sigue el mismo patrón de `data-testid` explícitos ya usado en el
   resto del template (`item-distance`, `item-location`, etc.).
2. `discovery-list.component.ts`: eliminar la señal `selectedItem` (declaración y su `.set(item)`
   dentro de `select()`) — confirmado sin consumidores, ni en este template ni en
   `discovery-page.component.ts`, por dos revisiones independientes en PLAN (impact scan +
   arch-auditor). `select()` queda solo con `this.muralSelected.emit(item);`. Corregir el docstring
   de la clase: ya no describe un panel de detalle separado que se revela al seleccionar — el
   rediseño Card→NzList (`d65842f`) muestra título/foto/distancia/ubicación/fecha siempre, por fila,
   sin necesidad de click.
3. `discovery-list.component.spec.ts`: reemplazar el test "seleccionar un ítem muestra su detalle
   inline: foto, fecha y ubicación (AC-04)" (que hoy espera `[data-testid="item-detail"]` y una foto
   con `maxWidth: 300px`, ninguno de los cuales existe en el template actual) por una verificación de
   que cada fila renderizada — SIN necesidad de click — expone: título (texto), foto (`photoUrl` vía
   `[data-testid="item-photo"]`), distancia (`[data-testid="item-distance"]`), ubicación
   (`[data-testid="item-location"]`) y fecha (`[data-testid="item-created-at"]`). Actualizar el test
   "items vacío muestra el mensaje de sin resultados..." para usar el selector
   `[data-testid="empty-message"]` agregado en el paso 1.

**Error handling**

N/A — Block 2 no introduce manejo de errores nuevo: es corrección de tests y eliminación de código
muerto sobre comportamiento ya existente en producción.

**Required tests**

- [ ] fila de la lista muestra título, foto, distancia, ubicación y fecha sin necesidad de click —
  AC-04 (reemplaza al test obsoleto de detalle-al-seleccionar)
- [ ] lista vacía muestra el mensaje de sin resultados vía `[data-testid="empty-message"]` — AC-06
  (corrige el selector roto del test existente)

**Completion criterion**

`discovery-list.component.spec.ts` pasa completo; sin referencias a `selectedItem` ni a
`[data-testid="item-detail"]` en ningún archivo del feature `discovery`.

## Final verification

Suite completa de `discovery/*.spec.ts` (map, list, page) en verde. Ningún test ni código de
producción interpola `item.title` como HTML. `prd-FEAT-001d.md` AC-04 y AC-10 tienen ambos al menos
un test que los verifica.

## Evidencia TDD

Reproducida en el corrective loop de VERIFY (ronda 1): se restauró temporalmente el código de
producción de Block 1 y Block 2 al estado del commit `c80cccc` (previo a CODE, solo spec/threat)
manteniendo los tests nuevos/reescritos de ambos bloques, y se corrió la suite completa.

**Rojo** (`discovery-map.component.ts`/`discovery-list.component.ts`/`.html`/`app.config.ts` en su
versión pre-CODE, tests en su versión final): 6 tests fallando, 132 pasando —

- `discovery-map.component.spec.ts`:
  - `hacer click en un marcador abre un popup` — Block 1, AC-10
  - `el popup contiene el título del mural` — Block 1, AC-10
  - `el popup contiene la fecha de creación formateada` — Block 1, AC-10
  - `un título con HTML se renderiza como texto literal, no como HTML inyectado` — Block 1, regresión
    de seguridad del threat model
- `discovery-list.component.spec.ts`:
  - `cada fila muestra título, foto, distancia, ubicación y fecha sin necesidad de click (AC-04)` —
    Block 2
  - `items vacío muestra el mensaje de sin resultados, sin botón de ampliar radio (AC-06)` — Block 2
    (fallaba porque `data-testid="empty-message"` todavía no existía en el HTML pre-Block 2)

**Verde**: se restauró el código de producción a su versión final (`git diff --stat` contra el
estado previo a esta reproducción, vacío) y se corrió la suite de nuevo — 138/138 tests, 0 fallos.

El test `un mural con title/createdAt undefined no lanza excepción al renderizar el popup` no forma
parte del rojo: es una aserción de "no lanza", que pre-CODE también se cumplía trivialmente (el
código viejo nunca llamaba a `bindPopup`). Su valor es de regresión hacia adelante, no de TDD
rojo→verde.
