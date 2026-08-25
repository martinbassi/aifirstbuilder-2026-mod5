# Spec FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| PRD | docs/daw/prd/prd-FEAT-003.md |
| Tier | FEATURE |
| Date | 2026-08-24 |
| Spec loops | 1 |

## Summary

**Spec loop 1 — reconstruido desde el código ya escrito en disco.** La implementación real diverge
de la spec original (`nz-card` + card chica): `AuthCardComponent` (`features/auth/ui/auth-card/`)
implementa un layout split-screen — panel de marca (`brand-panel`, wordmark de texto "Paretto.",
mensaje, imagen de fondo) a la izquierda, panel de formulario (`form-panel`) a la derecha, que
colapsa a un solo panel (formulario, 100% de ancho) por debajo de 700px de viewport. `login-form` y
`register-form` proyectan su contenido dentro vía `<ng-content />`, igual que en el diseño original.

Este loop también corrige gaps reales encontrados por el impact scan sobre el código ya escrito:
imports muertos (`NzCardModule`, `LogoComponent`), un bug funcional silencioso (el ícono de Google no
renderiza), colores hardcodeados que no coinciden con los tokens de tema de ADR-006, CSS duplicado
(reglas repetidas dentro de un mismo archivo, y 253 líneas idénticas entre `login-form` y
`register-form`), y tests que ya no validan el DOM real.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 |
| FR-02 | Block 1 |
| FR-03 | Block 1 |
| FR-04 | Block 1 |
| FR-05 | Block 1 |
| FR-06 | Block 1, Block 2 |
| FR-07 | Block 2 |
| NFR-01 | Block 1 (`.form-panel .login-container`, `width: min(410px, 80%)` — nunca excede 420px) |

## Dependencies between blocks

Block 2 depende de Block 1 (`AuthCardComponent` debe existir antes de envolver los formularios).
Ejecución secuencial — igual que en el loop anterior.

## Block 1 — AuthCardComponent (split-screen)

**Files**
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.ts` (modified — quitar el import
  muerto de `NzCardModule`)
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.html` (modified — quitar el bloque
  `carousel-dots` comentado)
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.css` (modified — limpieza)
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.spec.ts` (rewrite — el spec
  anterior validaba `nz-card`/`.auth-card-page`, que ya no existen)

**Logic**

`AuthCardComponent` sigue siendo standalone y puramente presentacional. Ya no importa
`NzCardModule` (el template no usa `nz-card`); ese import queda eliminado del array `imports` y del
`import` en cabecera — es dead code confirmado por el impact scan (no genera warning de build porque
`NzCardModule` es un `NgModule`, no un componente standalone, así que Angular no lo marca con
`NG8113` como sí hizo con `LogoComponent` en Block 2).

Template: sin cambios estructurales respecto al que ya está en disco (`login-page` → `brand-panel` +
`form-panel`, `brand-image` con `login-background.jpg`), salvo un recorte: el bloque `carousel-dots`
(comentado, líneas 22-26 del HTML actual) se **elimina** en vez de quedar comentado — no hay AC que
lo active en este ticket ni en el roadmap cercano (arch-audit de PLAN, WARN), y su CSS asociado ya se
elimina como dead code (ver más abajo); se puede recuperar del historial de git si hace falta más
adelante.

**CSS — limpieza (`auth-card.component.css`)**

El impact scan confirmó una regla `.brand-panel` duplicada (línea ~15 y línea ~202): la segunda gana
por cascada para `background`/`color`, dejando el primer bloque parcialmente muerto. Mismo patrón en
`.brand-message h1` y `.separator`. Se consolida en una sola declaración por selector:

```css
.brand-panel {
  position: relative;
  width: 57%;
  height: 100%;
  overflow: hidden;
  border-radius: 0 0 16px 0;

  background: var(--ant-primary-color);
  color: var(--app-color-secondary);
}

.brand-message h1 {
  color: var(--app-color-secondary);
}

.separator {
  background: var(--app-color-secondary);
}
```

- `background: var(--ant-primary-color)` reemplaza el hex hardcodeado `#ff6e48`, que el impact scan
  confirmó que **no coincide** con el token real (`--ant-primary-color: #fe6944`, definido en
  `styles.css` por ADR-006) — era drift, no un valor intencional. Decisión del usuario: usar la
  variable, no el hex.
- `color: var(--app-color-secondary)` reemplaza `#0d2348`, que sí coincidía con el token — se
  reemplaza igual, por consistencia (nunca hex literal para colores de marca, per ADR-006).
- El teal `#55c1b5` (primer bloque de `.brand-panel`, nunca visible porque el segundo bloque lo
  sobreescribe) se elimina directamente — no tiene token equivalente ni users; era dead code, no una
  paleta a preservar.
- Se elimina el CSS de `.skip-button` (clase huérfana, ningún elemento la usa en ningún template de
  `features/auth/ui/`) y el de `.carousel-dots` (estiliza un bloque que quedó comentado en el HTML) —
  ambos confirmados como dead code por el impact scan.
- Los breakpoints `900px` y `700px` se mantienen tal cual: el impact scan confirmó que no hay ningún
  otro `@media` en el frontend, así que no hay convención previa que seguir — quedan documentados acá
  como la decisión de diseño de este ticket, no como algo ya establecido en el codebase.

**Error handling**
- No aplica (componente presentacional sin estado ni llamadas externas).

**Required tests** (`auth-card.component.spec.ts`, reescrito — el anterior validaba `nz-card`, que ya
no existe)
- [ ] Renderiza `.login-page` con sus dos hijos directos `.brand-panel` y `.form-panel` — valida
  AC-01, AC-02 a nivel de marcado (el condicionamiento por ancho de viewport se confirma en Final
  verification, Vitest/jsdom no evalúa media queries).
- [ ] `.brand-panel` contiene el wordmark de texto ("Paretto."), el mensaje de marca y la imagen de
  `.brand-image` — valida AC-03.
- [ ] `.form-panel .login-container` proyecta el contenido recibido vía `<ng-content>` (contenido de
  prueba en el fixture aparece dentro) y tiene la clase que aplica el `max-width` — valida AC-04 a
  nivel de marcado (el ancho computado real se confirma en Final verification).
- [ ] **Verificación manual (VERIFY)** — Vitest/jsdom no evalúa media queries (no hay forma
  razonable de testear el colapso responsive con el stack actual): abrir `/login` en el navegador,
  achicar el viewport por debajo de 700px y confirmar que `.brand-panel` desaparece y `.form-panel`
  ocupa el 100% del ancho — valida AC-05.

**Completion criterion**
Los 3 tests pasan, el componente compila sin el import muerto de `NzCardModule`, y `auth-card.component.css`
no tiene reglas duplicadas ni selectores huérfanos.

## Block 2 — Formularios: ícono de Google, CSS compartido, imports muertos

**Files**
- `frontend/src/app/features/auth/ui/login-form.component.ts` (modified)
- `frontend/src/app/features/auth/ui/login-form.component.html` (modified — renombrar 3 clases)
- `frontend/src/app/features/auth/ui/register-form.component.ts` (modified)
- `frontend/src/app/features/auth/ui/register-form.component.html` (modified — mismo renombrado)
- `frontend/src/app/features/auth/ui/auth-form.css` (new — estilos compartidos, reemplaza a
  `login-form.component.css` y `register-form.component.css`)
- `frontend/src/app/features/auth/ui/login-form.component.css` (deleted)
- `frontend/src/app/features/auth/ui/register-form.component.css` (deleted)
- `frontend/src/app/features/auth/ui/login-form.component.spec.ts` (modified — quitar las 2
  aserciones sobre `app-logo`)
- `frontend/src/app/features/auth/ui/register-form.component.spec.ts` (modified — quitar las 2
  aserciones sobre `app-logo`/`nz-card`)

**Logic**

1. **Bug funcional — el ícono de Google no renderiza.** El impact scan confirmó que ni
   `login-form.component.ts` ni `register-form.component.ts` importan `NzIconModule` (de
   `ng-zorro-antd/icon`) en su array `imports`. `app.config.ts` registra `GoogleOutline` en
   `provideNzIcons([...])`, pero eso solo carga el ícono en `NzIconService` — sin la directiva
   `NzIconModule` importada en el componente, `<nz-icon nzType="google" nzTheme="outline" />` no
   tiene quién lo consuma y Angular lo compila como un elemento custom vacío (confirmado en el bundle
   por el impact scan). Se agrega `NzIconModule` al array `imports` de ambos componentes — 1 línea
   por archivo, sin la cual AC-07 ("botón visible") queda incompleto: el botón está, el ícono no.

2. **Imports muertos.** `LogoComponent` se importa en ambos `.ts` (`imports` array + `import`
   statement) sin usarse en ningún template — confirmado por el warning `NG8113` de `ng build` y por
   grep del impact scan. Se elimina de ambos archivos. Esto es consistente con la decisión de PRD de
   usar wordmark de texto en el panel de marca en vez de `LogoComponent` (ver Out of Scope del PRD).

3. **CSS compartido.** El impact scan confirmó que `login-form.component.css` y
   `register-form.component.css` son idénticos byte a byte (253 líneas), con clases nombradas
   `login-*` reusadas también en `register-form.component.html` sin que el nombre refleje la
   pantalla real. Se consolida en un único archivo `auth-form.css`, referenciado por ambos
   componentes vía `styleUrls: ['./auth-form.css']`, con 3 clases renombradas para dejar de estar
   atadas a "login":
   - `.login-header` → `.auth-header`
   - `.login-form` (la clase del `<form>`, no el `FormGroup` del componente — no hay colisión) →
     `.auth-form`
   - `.login-button` → `.auth-submit-button`

   El resto de las clases (`.google-button`, `.divider`, `.forgot-password`, `.signup`) ya son
   genéricas y no se tocan. Se actualiza el HTML de ambos formularios para usar los nombres nuevos.

   **Notas de arquitectura (arch-audit de PLAN, WARNs no bloqueantes):**
   - `auth-form.css` vive suelto en `features/auth/ui/`, sin un `.component.ts` propio — no hay
     precedente de esto en el resto del codebase. Es coherente con AGENTS.md (`shared/` es solo para
     lo reusable ENTRE features, y `login-form`/`register-form` son 2 pantallas de la MISMA feature),
     pero es un patrón nuevo: si se repite en otra feature, vale la pena revisarlo como convención
     explícita en vez de caso a caso.
   - Al no existir un componente que agrupe HTML+CSS compartido, la relación entre `login-form` y
     `register-form` queda sostenida por convención de nombres de clase, no por el compilador — un
     cambio futuro a `auth-form.css` pensado solo para uno de los dos podría romper el otro sin que
     Angular avise. El test de equivalencia estructural (AC-06, más abajo) mitiga esto pero no lo
     elimina; queda como riesgo conocido y documentado, no una omisión.
   - `AuthCardComponent` usa `styleUrl` (singular, sintaxis moderna, ya en disco); `LoginFormComponent`/
     `RegisterFormComponent` usan `styleUrls: ['./auth-form.css']` (plural). Angular soporta ambas —
     se mantiene el array plural acá porque es la forma que ya tenían ambos archivos antes de este
     loop, y cambiarla no aporta nada al alcance de este ticket; no es una inconsistencia a resolver
     en FEAT-003.

4. **Formateo.** `registerWithGoogle()` en `register-form.component.ts` tiene indentación rota (sin
   los 2 espacios del resto del archivo) y una línea en blanco con espacios sueltos — no pasa
   Prettier tal como está (confirmado por el impact scan). Se corrige junto con el resto del bloque.

Ningún cambio a `submit()`, a los validadores, ni a los tests existentes de comportamiento
(envío de formulario, mensajes de error) — confirmado sin riesgo por el impact scan, que verificó que
esos tests usan `querySelector` sobre `[data-testid="error-message"]`, que no se toca.

**Error handling**
- No aplica — sin cambios de lógica de negocio, solo wiring de la directiva de ícono, limpieza de
  imports y consolidación de CSS.

**Required tests**
- [ ] `login-form.component.spec.ts`: quitar las 2 aserciones que buscan `app-logo` (ya no aplica,
  ver PRD Out of Scope). Los tests de comportamiento existentes (mensaje de error genérico, envío
  exitoso) quedan sin cambios — el impact scan confirmó que siguen pasando.
- [ ] `register-form.component.spec.ts`: mismo ajuste — quitar las 2 aserciones sobre `app-logo`/
  `nz-card`.
- [ ] `login-form.component.spec.ts` (nuevo): el fixture contiene `app-auth-card` envolviendo el
  formulario — valida AC-01.
- [ ] `register-form.component.spec.ts` (nuevo): el fixture contiene `app-auth-card` envolviendo el
  formulario — valida AC-02.
- [ ] Nuevo test (en ambos spec): el elemento raíz `.auth-header`/`.auth-form`/`.auth-submit-button`
  está presente y con la misma clase en login y en register (equivalencia estructural, ya que ambos
  consumen `auth-form.css`) — valida AC-06 a nivel de marcado (el ancho/padding computado real se
  confirma en Final verification).
- [ ] Nuevo test en `login-form.component.spec.ts` y `register-form.component.spec.ts`: el botón con
  clase `.google-button` contiene un `nz-icon[nzType="google"]` — valida AC-07 (fix del bug de
  Block 2.1; sin este test, el bug hubiera quedado sin cobertura otra vez).
- [ ] Confirmar visualmente en VERIFY que el ícono de Google se pinta en pantalla (Vitest/jsdom no
  renderiza el SVG real de `NzIconService`, así que la aserción del punto anterior valida la
  directiva/binding, no el pixel).

**Completion criterion**
Los tests nuevos y actualizados de ambos formularios pasan, `ng build` no emite `NG8113` para
`LogoComponent`, y una inspección visual manual (VERIFY) confirma que el ícono de Google se ve en
ambos botones.

## Final verification

- Los 2 bloques completos y sus tests en verde.
- `ng build --configuration production` no supera el budget de error (1.1MB, ajustado en FEAT-001d) y
  no emite warnings `NG8113` para `LogoComponent`.
- Inspección visual manual en navegador (VERIFY): `/login` y `/register` muestran el layout
  split-screen (panel de marca + panel de formulario) en desktop, colapsan a un solo panel por debajo
  de 700px de ancho (AC-05), el panel de formulario no supera 420px de contenido (AC-04), ambas
  pantallas usan el mismo ancho de panel y el mismo tratamiento visual una al lado de la otra (AC-06),
  y el ícono de Google es visible en ambos botones (AC-07) — Vitest/jsdom no calcula layout real ni
  renderiza SVGs de ícono, así que estos puntos se confirman visualmente, no por assertion.
