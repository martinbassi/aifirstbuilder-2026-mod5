# Spec FEAT-003: Rediseño visual de login/register (card centrada)

| Field | Value |
|-------|-------|
| Ticket | FEAT-003 |
| PRD | docs/daw/prd/prd-FEAT-003.md |
| Tier | FEATURE |
| Date | 2026-08-23 |
| Spec loops | 0 |

## Summary

Un componente presentacional nuevo, `AuthCardComponent` (`features/auth/ui/auth-card/`), envuelve en un
`nz-card` (ya disponible en `ng-zorro-antd`, usado hoy en `discovery-list` y
`pending-murals-list` — sin dependencia nueva) centrado horizontal y verticalmente en el viewport
(flexbox + `min-height: 100vh`), con fondo blanco liso y ancho máximo 420px. Proyecta su contenido
vía `<ng-content />`, así que `login-form` y `register-form` solo necesitan envolver su
`<app-logo />` + `<form>` existentes dentro de `<app-auth-card>`, sin tocar la lógica de ninguno de
los dos formularios.

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 2 |
| FR-02 | Block 2 |
| FR-03 | Block 1, Block 2 |
| FR-04 | Block 1 |
| FR-05 | Block 1 |
| FR-06 | Block 1 |
| FR-07 | Block 1, Block 2 |
| NFR-01 | Block 1 (max-width: 420px en `.auth-card`) |

## Dependencies between blocks

Block 2 depende de Block 1 (`AuthCardComponent` debe existir antes de poder importarlo en
login-form/register-form). Ejecución secuencial.

## Block 1 — AuthCardComponent

**Files**
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.ts` (new)
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.html` (new)
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.css` (new)
- `frontend/src/app/features/auth/ui/auth-card/auth-card.component.spec.ts` (new)

**Logic**
`AuthCardComponent` es standalone y puramente presentacional (sin `@Input()`, sin lógica, sin
llamadas a servicios). Vive en `features/auth/ui/` (no en `shared/`): aunque lo consumen dos
pantallas (`login`, `register`), ambas son parte de la MISMA feature (`auth`) — decisión tomada en
PLAN tras un WARN del arch-auditor, que notó que `shared/` está reservado por `AGENTS.md` para lo
reusable ENTRE features, no entre pantallas de una misma feature. `LogoComponent` (FEAT-002) quedó
en `shared/logo/` bajo un criterio más laxo; ese componente queda fuera de alcance de este ticket —
su posible reubicación es un ítem pendiente aparte, no parte de FEAT-003.

`@Component` decorator explícito: `selector: 'app-auth-card'`, `standalone: true`,
`templateUrl: './auth-card.component.html'` (nunca `template:` inline),
`changeDetection: ChangeDetectionStrategy.OnPush` — mismo patrón que `LogoComponent`,
`LoginFormComponent` y `RegisterFormComponent`.

Template (`auth-card.component.html`):
```html
<div class="auth-card-page">
  <nz-card class="auth-card">
    <ng-content />
  </nz-card>
</div>
```

CSS (`auth-card.component.css`):
```css
.auth-card-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #fff;
  padding: 24px;
  box-sizing: border-box;
}

.auth-card {
  width: 100%;
  max-width: 420px;
}
```

Import `NzCardModule` de `ng-zorro-antd/card` en el array `imports` del componente (patrón ya usado
en `discovery-list.component.ts` y `pending-murals-list.component.ts`).

**Error handling**
- No aplica (componente presentacional sin estado ni llamadas externas).

**Required tests**
- [ ] `auth-card.component.spec.ts`: renderiza `nz-card` con la clase `auth-card`, y el contenido
  proyectado vía `<ng-content>` aparece dentro (verificar con contenido de prueba proyectado en el
  fixture) — valida FR-05.
- [ ] `auth-card.component.spec.ts`: el contenedor externo (`.auth-card-page`) tiene la clase
  correcta para que el CSS de centrado aplique (verificación estructural, no de estilos computados —
  Vitest/jsdom no calcula layout real) — valida FR-01/FR-02 a nivel de marcado.

**Completion criterion**
Los 2 tests pasan, y el componente compila e importa sin errores en un componente host de prueba.

## Block 2 — Integrar AuthCardComponent en login-form y register-form

**Files**
- `frontend/src/app/features/auth/ui/login-form.component.html` (modified) — envolver
  `<app-logo />` + `<form>` dentro de `<app-auth-card>`
- `frontend/src/app/features/auth/ui/login-form.component.ts` (modified) — agregar
  `AuthCardComponent` al array `imports`
- `frontend/src/app/features/auth/ui/register-form.component.html` (modified) — mismo envoltorio
- `frontend/src/app/features/auth/ui/register-form.component.ts` (modified) — agregar
  `AuthCardComponent` al array `imports`

**Logic**
Ambos formularios son componentes standalone: `AuthCardComponent` debe agregarse a su array
`imports` en el `.ts`, no alcanza con tocar el `.html` (mismo gap NG8001 ya documentado en FEAT-002
Block 3, detectado por el impact scanner en PLAN de ese ticket — se repite acá por precaución, ya
confirmado sin gaps por el impact scanner de este ticket también). Ningún cambio a la lógica de
`submit()`, validadores, ni a los tests existentes de comportamiento del formulario — solo el
marcado se envuelve.

**Error handling**
- No aplica — sin cambios de lógica, `AuthCardComponent` no introduce nuevos casos de error.

**Required tests**
- [ ] `login-form.component.spec.ts` (actualizado): el DOM renderizado contiene `app-auth-card`
  envolviendo `app-logo` y el `<form>`, en ese orden (logo antes que el form en el árbol) — valida
  AC-01, AC-03.
- [ ] `register-form.component.spec.ts` (actualizado): el DOM renderizado contiene `app-auth-card`
  envolviendo `app-logo` y el `<form>` en el mismo orden, con el mismo ancho máximo/clase que login
  (mismo componente compartido, no duplicación) — valida AC-02, AC-03, AC-08.
- [ ] Confirmar (revisando ambos `.spec.ts`) que los tests preexistentes de comportamiento
  (envío de formulario, mensajes de error, validación) siguen pasando sin modificación — el wrapper
  no debe romper ninguna aserción existente basada en `querySelector` (confirmado sin riesgo por el
  impact scanner: ambos usan `querySelector` sobre todo el subárbol, no como hijo directo).
- [ ] **Verificación manual (VERIFY)** — Vitest/jsdom no calcula layout real (centrado,
  `min-height: 100vh`, overflow), así que no hay forma razonable de testear esto con el stack
  actual: abrir `/login` y `/register` en el navegador y confirmar visualmente (a) fondo blanco
  fuera de la card (AC-04), (b) sin overflow horizontal ni contenido cortado a 320px de ancho
  (AC-06), (c) la card se adapta al viewport cuando este es más angosto que 420px, sin forzar
  scroll horizontal (AC-07).

**Completion criterion**
Los tests nuevos y preexistentes de ambos formularios pasan, y una inspección visual manual de
`/login` y `/register` (en VERIFY) confirma que la card queda centrada, sin overflow horizontal a
320px de ancho.

## Final verification

- Los 2 bloques completos y sus tests en verde.
- `ng build --configuration production` no supera el budget de error (1.1MB, ajustado en FEAT-001d).
- Inspección visual manual en navegador (VERIFY, igual que AC-05/AC-06 de FEAT-002): `/login` y
  `/register` muestran la card centrada, con el logo arriba del formulario, fondo blanco, sin
  overflow a 320px de ancho — valida AC-01, AC-02, AC-04, AC-06, AC-07 (Vitest/jsdom no calcula
  layout real, así que el centrado/overflow efectivo se confirma visualmente, no por assertion).
