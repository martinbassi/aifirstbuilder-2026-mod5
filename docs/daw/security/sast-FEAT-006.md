# SAST FEAT-006: Mostrar título y fecha de creación al hacer click en un marcador del mapa de /discover

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-006 |
| Fecha | 2026-08-26 |
| Resultado | PASSED |

## Alcance

- `frontend/src/app/app.config.ts` (modificado)
- `frontend/src/app/features/discovery/ui/discovery-map.component.ts` (modificado)
- `frontend/src/app/features/discovery/ui/discovery-map.component.spec.ts` (modificado)
- `frontend/src/app/features/discovery/ui/discovery-list.component.html` (modificado)
- `frontend/src/app/features/discovery/ui/discovery-list.component.ts` (modificado)
- `frontend/src/app/features/discovery/ui/discovery-list.component.spec.ts` (modificado)

## Secretos
✅ F-SAST-01: sin API keys, passwords, tokens ni connection strings en el diff.

## Inyección
✅ F-SAST-02/03: sin queries ni comandos — el cambio es exclusivamente frontend/Angular (UI de mapa y lista), sin nuevos endpoints ni acceso a datos.

## XSS y funciones inseguras
✅ F-SAST-06 (High, mitigado por diseño desde PLAN — ver `docs/daw/security/threat-FEAT-006.md`): `discovery-map.component.ts` (`buildPopupContent`) construye el contenido del popup de Leaflet **exclusivamente** vía `document.createElement` + `.textContent`, nunca interpolando `item.title` (texto libre de usuario, sin sanitización HTML en el backend) en un string HTML. El único match de "innerHTML" en el diff es el comentario que documenta el riesgo y su mitigación, no código ejecutable. Cubierto por un test de regresión dedicado en `discovery-map.component.spec.ts` que verifica que un título con `<img src=x onerror=alert(1)>` se renderiza como texto literal (sin `<img>` inyectado en el DOM).

✅ F-SAST-04: sin `eval`, sin deserialización insegura.

## Resto de categorías obligatorias
✅ N/A para este alcance: sin SSRF, sin modo debug, sin logging de datos sensibles (`console.*` ausente en el diff final), sin upload de archivos, sin endpoints nuevos que requieran CSRF o validación de input adicional (el input ya venía validado desde FEAT-001b).

## Dependencias
✅ F-SAST-13/16: `npm audit --omit=dev` → 0 vulnerabilidades.

## Suppressions
0 — no se documentó ninguna supresión (no hubo hallazgos Medium/Low).

## Total
6 archivos limpios, 0 vulnerabilidades (0 Critical, 0 High, 0 Medium, 0 Low).
