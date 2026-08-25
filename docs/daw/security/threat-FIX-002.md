# Threat Model FIX-002: Assets rotos en discovery (marcadores Leaflet, CSP local, fallback center, foto sin max-width)

| Campo | Valor |
|-------|-------|
| Ticket | FIX-002 |
| Fecha | 2026-08-25 |
| Diseño analizado | `discovery-map.component.ts` (íconos Leaflet + fallback center), `discovery-list.component.html` + `pending-murals-list.component.html` (max-width en foto), `frontend/src/index.html` / `frontend/src/index.development.html` + `angular.json` (CSP por entorno) |

## Superficies de ataque identificadas

1. **CSP `img-src`, ahora separada por entorno de build**: `frontend/src/index.html` (usado por
   defecto, incluida producción) mantiene la CSP estricta original; `frontend/src/index.development.html`
   (usado solo por la configuración `development` de Angular, la que corre `ng serve`) agrega
   `http://127.0.0.1:10000` (Azurite, el emulador local de Azure Storage). Es la única superficie
   con implicancia de seguridad real del fix — ver R1.
2. **Assets estáticos de Leaflet** (`frontend/public/images/leaflet/*.png`) y override de
   `L.Icon.Default.mergeOptions(...)`: rutas de archivos servidos como estáticos, sin input de
   usuario ni lógica de autorización involucrada.
3. **`FALLBACK_CENTER`** (coordenadas fijas de Montevideo): constante de UI, sin dato sensible ni
   input externo.
4. **`style="max-width: 300px;"` en `<img>`**: atributo de presentación, sin ejecución de código.

## Trust boundary

**Navegador del usuario ↔ orígenes externos permitidos por CSP.** La CSP es la boundary que decide
desde qué orígenes el navegador puede cargar recursos — es la única de las 4 superficies donde este
fix mueve esa frontera. Con la separación por entorno de build, la boundary de producción no se
mueve en absoluto: solo se mueve la de la configuración `development`.

## Análisis STRIDE

| Categoría | Aplica | Análisis |
|---|---|---|
| **Spoofing** | No aplica | Ningún cambio afecta autenticación ni identidad. |
| **Tampering** | No aplica | No se modifica ningún dato en tránsito ni en reposo. |
| **Repudiation** | No aplica | Sin cambios de logging/auditoría. |
| **Information Disclosure** | Sí, ver R1 | Ver riesgo abajo. |
| **Denial of Service** | No aplica | Ningún cambio afecta disponibilidad del backend ni del frontend. |
| **Elevation of Privilege** | No aplica | Sin cambios de autorización; el fix no toca el mecanismo de URLs firmadas (SAS) de RNF-009 que ya controla el acceso a fotos de murales pendientes/rechazados. |

## Datos sensibles (F-TM-05)

Ninguno nuevo. El fix no cambia qué fotos son accesibles ni cómo — el control de acceso a fotos de
murales "pendiente"/"rechazado" (RNF-009, URLs firmadas de corta duración) queda intacto; este
ticket solo corrige que las imágenes ya autorizadas se rendericen correctamente. No aplica cifrado
adicional (F-TM-07).

## Riesgos

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| **R1** — `frontend/src/index.html` no tenía `fileReplacements` en `angular.json` entre las configuraciones `development` y `production` (verificado: no existía ninguna antes de este fix). Sin esa separación, agregar `http://127.0.0.1:10000` al `img-src` se hubiera compilado igual en ambas configuraciones, llegando sin cambios al build de producción — un origen extra permitido en el navegador de cada usuario final sin ningún beneficio funcional ahí, ampliando innecesariamente la superficie que una futura XSS podría explotar para sondear el propio host de la víctima (SSRF-estilo vía CSP). | I (Information Disclosure) | Baja (requiere una XSS previa, que hoy no existe conocida) | Baja (el origen es loopback, no expone nada de la red de la víctima más allá de sondear el puerto 10000 de su propia máquina) | **Mitigado en el diseño**, a pedido explícito del usuario: preparar la CSP para variar por entorno aunque hoy solo exista `development`. `frontend/src/index.html` vuelve a su CSP original (sin `127.0.0.1:10000`) y queda como el archivo por defecto — el que usa el build de producción. Se agrega `frontend/src/index.development.html`, copia con el origen de Azurite sumado al `img-src`, y `angular.json` → `architect.build.configurations.development.fileReplacements` reemplaza `src/index.html` por `src/index.development.html` **solo** en la configuración `development` (la que usa `ng serve` por default, y `serve` hereda). Cuando se definan `staging`/`production` como configuraciones propias, cada una puede sumar su propio `index.{env}.html` con su propio `fileReplacements` sin tocar este mecanismo — es el patrón estándar de Angular para esto, no infraestructura nueva. |
| **R2** — Ninguno de los otros 3 cambios (íconos Leaflet, fallback center, `max-width` en fotos) introduce superficie de ataque. | — | — | — | No aplica mitigación — confirmado en el análisis STRIDE arriba. |

## Mitigaciones a incorporar al fix-plan

1. `frontend/src/index.html` vuelve a su CSP original (sin `127.0.0.1:10000`) — pasa a ser el
   archivo de producción/por defecto (R1).
2. Nuevo archivo `frontend/src/index.development.html`: copia de `index.html` con
   `http://127.0.0.1:10000` agregado al `img-src` (R1).
3. `frontend/angular.json`: `fileReplacements` en `architect.build.configurations.development`
   reemplazando `src/index.html` por `src/index.development.html` (R1).

---

**Total: 0 CRITICAL, 0 HIGH, 0 MEDIUM, 1 LOW (R1, mitigado en el diseño).**

**Resultado: PASSED** — toda mitigación queda incorporada al fix-plan antes de escribirlo a disco.
