# SAST FIX-002: Assets rotos en discovery (marcadores Leaflet, CSP local, fallback center, foto sin max-width)

| Field | Value |
|-------|-------|
| Ticket | FIX-002 |
| Date | 2026-08-25 |
| Scope | `frontend/src/app/features/discovery/ui/discovery-map.component.ts`, `discovery-list.component.html`, `frontend/src/app/features/moderation/ui/pending-murals-list.component.html`, `frontend/src/index.html`, `frontend/src/index.development.html`, `frontend/angular.json`, `frontend/public/images/leaflet/*.png`, specs de los 3 componentes |

## Secrets (F-SAST-01)

- ✅ Sin patrones de password/api-key/secret/token hardcodeados. Los únicos valores nuevos son rutas
  de assets estáticos, coordenadas de UI y un origen de CSP en texto plano.

## Inyección (F-SAST-02, F-SAST-03, F-SAST-05)

- ✅ Sin queries, sin `exec`/`child_process`, sin paths construidos con input de usuario. Las rutas de
  íconos de Leaflet (`images/leaflet/*.png`) son constantes fijas en código, no derivadas de ningún
  input.

## XSS (F-SAST-06)

- ✅ `[src]="item.photoUrl"` / `[src]="mural.photoUrl"` — bindings preexistentes, sin cambios de este
  fix; Angular los trata como contexto URL (sanitización propia del framework, sin cambios aquí).
- ✅ El `style="max-width: 300px;"` agregado es una cadena estática en el template, sin interpolación
  de datos — no hay superficie de inyección de CSS.
- ✅ Sin `innerHTML`/`bypassSecurityTrust*` en ninguno de los archivos tocados.

## Funciones inseguras / criptografía débil (F-SAST-04, F-SAST-08, F-SAST-17)

- ✅ Sin `eval()`, sin deserialización insegura, sin crypto nuevo. `delete
  (L.Icon.Default.prototype as any)._getIconUrl` borra un método de un prototipo de una librería de
  terceros (Leaflet) en tiempo de carga del módulo — no ejecuta código dinámico ni recibe input.

## Resto de categorías obligatorias (F-SAST-07, F-SAST-09, F-SAST-10, F-SAST-11, F-SAST-12)

- ✅ **SSRF (F-SAST-07):** el único cambio con superficie real es la CSP `img-src`. Ya evaluado en
  threat modeling (`docs/daw/security/threat-FIX-002.md`, R1) — el origen de Azurite
  (`http://127.0.0.1:10000`) queda exclusivamente en `index.development.html`, servido solo por la
  configuración `development` de Angular (`ng serve`); `index.html` de producción no lo incluye
  (verificado: `git diff` de `index.html` contra `HEAD` es vacío, sin cambios respecto al original).
- ✅ Debug mode en producción: no aplica — sin cambios a ningún gate de entorno del backend.
- ✅ Logging de datos sensibles: sin `console.log`/logging nuevo en ninguno de los archivos.
- ✅ Unrestricted upload: no aplica, sin endpoint de upload tocado.
- ✅ CSRF: no aplica, sin formularios ni mutaciones nuevas.

## Medium — validación de input y manejo de errores (F-SAST-14, F-SAST-15)

- ✅ F-SAST-14: no aplica — ningún cambio recibe input de usuario.
- ✅ F-SAST-15: no aplica — sin excepciones ni mensajes de error nuevos.

## Dependencias (F-SAST-13, F-SAST-16)

- ✅ `npm audit --audit-level=moderate` → 0 vulnerabilidades.
- ✅ Sin dependencias nuevas — `leaflet` ya es dependencia del proyecto (FEAT-001d); este fix solo
  agrega assets estáticos propios y ajusta configuración de build.

## Suppressions

Ninguna — no hubo hallazgos Medium que requirieran documentación de supresión.

## Resultado

Total: 12 categorías revisadas, 0 hallazgos Critical/High/Medium, 0 warnings.
