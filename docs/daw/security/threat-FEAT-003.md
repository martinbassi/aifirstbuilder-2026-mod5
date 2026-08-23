# Threat Model FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Diseño analizado | 2 bloques: `AuthCardComponent` (nuevo, presentacional) + integración en `login-form`/`register-form` (solo marcado, sin cambios de lógica) |
| Fecha | 2026-08-23 |

## Componentes y superficies de ataque

1. **`AuthCardComponent` (nuevo)** — componente Angular standalone, presentacional puro: sin
   `@Input()`, sin `@Output()`, sin llamadas a servicios, sin `HttpClient`. Proyecta contenido vía
   `<ng-content />`, cuyo contenido proviene exclusivamente de los templates de `login-form` y
   `register-form` (código propio, compilado en build-time por Angular) — nunca de input de usuario
   ni de datos del servidor. No hay superficie de inyección: `ng-content` no interpreta strings como
   HTML (a diferencia de `[innerHTML]`), proyecta nodos del DOM ya compilados por el compilador de
   Angular.
2. **Modificación de `login-form`/`register-form`** — solo envuelve el `<app-logo />` + `<form>`
   existentes dentro de `<app-auth-card>`. No toca `submit()`, los `FormControl`, los validadores, ni
   la llamada a `AuthService`. Ninguna lógica de autenticación cambia.

## Trust boundaries

Ninguno nuevo. Este ticket no cruza el límite cliente-servidor: no agrega llamadas HTTP, no cambia el
contrato de `AuthService`/`AuthClient`, no toca headers, cookies ni el interceptor de sesión. El
único límite relevante (navegador ↔ API, ya cubierto por el threat model de FEAT-001a) permanece sin
cambios.

## Análisis STRIDE

| Categoría | Aplica | Nota |
|---|---|---|
| Spoofing | No | Sin identidad ni autenticación involucrada en este cambio |
| Tampering | No | Sin datos que modificar; el contenido proyectado es 100% estático, compilado en build-time |
| Repudiation | No | Sin acciones nuevas que requieran trazabilidad |
| Information Disclosure | No | Sin datos sensibles nuevos; `AuthCardComponent` no recibe ni expone ningún dato |
| Denial of Service | Bajo | `nz-card` ya está en el bundle (usado en `discovery-list`/`pending-murals-list`); el CSS nuevo (`auth-card.component.css`) agrega bytes despreciables al bundle, sin riesgo de exceder el budget de producción (1.1MB, verificado en Final verification del spec) |
| Elevation of Privilege | No | Sin cambios de autorización ni de roles |

## Datos sensibles

Ninguno clasificable — `AuthCardComponent` no maneja PII, credenciales ni datos financieros; es
puramente de layout.

## Riesgos identificados

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| R1 — Bundle de producción crece por CSS/componente nuevo, acercándose al budget de error (1.1MB) | D (Denial of Service, sobre el proceso de build/deploy) | Baja | Baja | El agregado es un componente Angular trivial (~15 líneas de TS + HTML + CSS corto): impacto estimado en bytes de un solo dígito de KB. Mitigación: medir el build de producción al cierre de CODE (ya incluido como paso de "Final verification" en el spec), igual que se hizo en tickets anteriores. |

## Resultado

**PASSED.** 0 riesgos CRITICAL/HIGH. 1 riesgo LOW (R1), con mitigación de verificación (medición de
build) ya incorporada al spec — no requiere cambio de arquitectura ni riesgo aceptado formalmente.

`gates.threat` → `true`.
