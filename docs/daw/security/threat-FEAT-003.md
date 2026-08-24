# Threat Model FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Diseño analizado | Spec loop 1 — 2 bloques: `AuthCardComponent` reescrito como split-screen (panel de marca + panel de formulario) + fix del ícono de Google, imports muertos y CSS compartido en `login-form`/`register-form` |
| Fecha | 2026-08-24 |
| Reemplaza | El threat model anterior (2026-08-23), escrito para el diseño de card+`nz-card` que no llegó a construirse |

## Componentes y superficies de ataque

1. **`AuthCardComponent` (split-screen)** — sigue siendo standalone y presentacional puro: sin
   `@Input()`, sin `@Output()`, sin llamadas a servicios, sin `HttpClient`. Proyecta contenido vía
   `<ng-content />` (código propio, compilado en build-time — no interpreta strings como HTML, a
   diferencia de `[innerHTML]`). El wordmark "Paretto." y el mensaje de marca son texto estático en
   el template, no interpolación de datos externos.
2. **`login-background.jpg` (asset nuevo)** — imagen estática servida desde `frontend/public/`
   (mismo origen, sin llamada a un servicio externo). Verificado: JPEG/JFIF de 126KB, sin bloque EXIF
   (formato JFIF y EXIF son mutuamente excluyentes en la cabecera JPEG), solo contiene el string
   `Photoshop 3.0` como metadato de edición — sin GPS, sin datos de dispositivo, sin autor.
3. **Botón "Continuar con Google" (`loginWithGoogle()`/`registerWithGoogle()`)** — visible en ambas
   pantallas, con el handler de click vacío (no-op) en este ticket. No dispara ninguna request HTTP,
   no navega, no crea ningún estado de sesión ni token — confirmado en el spec (AC-07) y en el
   impact scan de PLAN.
4. **Fix de `login-form`/`register-form`** — agrega `NzIconModule` (ya parte del stack, sin
   dependencia nueva) para que el ícono de Google se renderice; elimina imports muertos
   (`LogoComponent`, `NzCardModule`); consolida CSS. No toca `submit()`, los `FormControl`, los
   validadores, ni la llamada a `AuthService`.

## Trust boundaries

Ninguno nuevo. Este ticket no cruza el límite cliente-servidor: no agrega llamadas HTTP, no cambia el
contrato de `AuthService`/`AuthClient`, no toca headers, cookies ni el interceptor de sesión. El botón
de Google no inicia ningún flujo OAuth (ver punto 3) — no hay un nuevo trust boundary hacia un
proveedor de identidad externo, porque no hay integración real, solo un elemento visual. El único
límite relevante (navegador ↔ API, ya cubierto por el threat model de FEAT-001a) permanece sin
cambios.

## Análisis STRIDE

| Categoría | Aplica | Nota |
|---|---|---|
| Spoofing | Bajo (ver R2) | El botón "Continuar con Google" podría leerse como un mecanismo de identidad funcional que no lo es |
| Tampering | No | Sin datos que modificar; contenido proyectado 100% estático, compilado en build-time |
| Repudiation | No | Sin acciones nuevas que requieran trazabilidad |
| Information Disclosure | Bajo (ver R3) | `login-background.jpg` es el único asset nuevo con metadata a revisar; verificado sin PII |
| Denial of Service | Bajo (ver R1) | CSS/imagen nuevos agregan peso al bundle/página, lejos del budget de error |
| Elevation of Privilege | No | Sin cambios de autorización ni de roles |

## Datos sensibles

Ninguno clasificable — ni `AuthCardComponent` ni el asset de imagen manejan PII, credenciales ni
datos financieros. El botón de Google no envía ni recibe datos (no-op).

## Riesgos identificados

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| R1 — El bundle de producción crece por el CSS consolidado (`auth-card.component.css` + `auth-form.css`, con media queries) y por `login-background.jpg` | D (sobre el proceso de build/deploy) | Baja | Baja | El asset de imagen se sirve desde `public/` — Angular no lo empaqueta en el bundle de JS/CSS, así que no cuenta contra el budget de producción (1.1MB, ajustado en FEAT-001d). El CSS agregado es de pocos KB. Mitigación: medir `ng build --configuration production` al cierre de CODE, ya incluido en "Final verification" del spec. |
| R2 — El botón "Continuar con Google" es visualmente indistinguible de un botón funcional, pero no inicia ningún flujo de autenticación | S (Spoofing, a nivel de percepción de UI — no técnico) | Baja | Baja | El handler no produce ningún efecto observable (sin spinner, sin mensaje de error, sin redirect, sin request de red) — no hay una "autenticación falsa" que pueda engañar al usuario haciéndole creer que inició sesión: simplemente no pasa nada al hacer click. AC-07 del PRD deja esto como contrato explícito. Riesgo de UX (confusión/frustración), no de seguridad — no requiere aceptación formal por ser LOW, pero queda documentado para que no se lea como un bug sin reportar. |
| R3 — `login-background.jpg` podría filtrar metadata sensible (GPS, dispositivo, autor) si no se hubiera revisado antes de commitear | I (Information Disclosure) | Baja | Baja | Verificado con `file`/`strings`: formato JFIF (sin bloque EXIF), único string de metadata es `Photoshop 3.0`, sin GPS ni datos de autor/dispositivo. Sin acción adicional requerida. |

## Resultado

**PASSED.** 0 riesgos CRITICAL/HIGH. 3 riesgos LOW (R1, R2, R3), todos con mitigación verificada — no
requiere cambio de arquitectura ni riesgo aceptado formalmente (F-TM-04 solo exige esa formalidad
para CRITICAL/HIGH sin mitigación viable).

`gates.threat` → `true`.
