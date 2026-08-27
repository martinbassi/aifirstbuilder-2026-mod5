# PRD FEAT-001a: Autenticación básica

| Field | Value |
|-------|-------|
| Ticket | FEAT-001a |
| Tracker | none |
| Date | 2026-08-15 |
| PRD loops | 2 |

## Context and Problem

Es el primer sub-ticket de FEAT-001 (ver `docs/daw/prd/prd-FEAT-001.md`, índice del split), sin
dependencias previas. Crear un mural requiere que el usuario esté autenticado (RF-026 del PRD de
producto), y la moderación mínima (FEAT-001c) requiere distinguir un rol Administrador. Sin una base
de autenticación y de modelo de usuario, ningún sub-ticket posterior de la cadena a→b→c→d puede
avanzar.

## Goals

- Que una persona pueda crear una cuenta y autenticarse con usuario y contraseña.
- Que la sesión autenticada quede disponible para que otros sub-tickets la exijan (crear mural) o la
  usen para autorizar acciones (moderación).
- Que el modelo de usuario contemple desde el inicio el campo de rol que la moderación va a
  necesitar, evitando una migración de esquema posterior.

## Functional Requirements

- FR-01: El sistema debe permitir crear una cuenta nueva ingresando un nombre de usuario, contraseña
  y email. (RF-040)
- FR-02: El sistema debe rechazar el registro con un mensaje genérico de registro fallido, sin
  precisar si el motivo fue el email o el nombre de usuario, cuando cualquiera de los dos ya está en
  uso. (RF-040 — mensaje deliberadamente genérico, ver "Risks and Mitigations": mensajes específicos
  habilitan enumeración de cuentas, hallazgo del threat modeling de PLAN)
- FR-03: El sistema debe rechazar el registro, informando el motivo, cuando la contraseña tiene
  menos de 8 caracteres, más de 128 caracteres, o no incluye letras y números. (RF-040, RF-054)
- FR-04: El sistema debe permitir al usuario autenticarse con usuario y contraseña. (RF-022)
- FR-05: El sistema debe rechazar el intento de login cuando el usuario o la contraseña son
  incorrectos, sin indicar cuál de los dos falló.
- FR-06: El sistema debe permitir al usuario cerrar su sesión activa. (RF-033)
- FR-07: El sistema debe almacenar cada cuenta con un campo de rol, con valor por defecto
  "Colaborador/Explorador" y "Administrador" como valor alternativo posible, sin exponer ninguna
  funcionalidad para asignarlo desde la interfaz.
- FR-08: El sistema debe recuperar los datos de la sesión actual (usuario y rol) al arrancar la
  aplicación, cuando existe un token de sesión almacenado pero esos datos todavía no están
  disponibles en memoria (por ejemplo, tras recargar la página). (gap detectado en FEAT-007: el rol
  vive solo en un signal en memoria, sin rehidratarse tras un refresh, aunque el token —y la sesión
  server-side— sigan siendo válidos)

## Non-Functional Requirements

- NFR-01: Las contraseñas deben almacenarse mediante un algoritmo de hashing, nunca en texto plano.
  (RNF-006)
- NFR-02: La aplicación debe servirse exclusivamente sobre HTTPS. (RNF-006)
- NFR-03: La sesión del usuario debe expirar a los 7 días de haber iniciado sesión. (RNF-006)
- NFR-04: La resolución de rutas protegidas debe esperar a que la rehidratación de sesión (FR-08)
  termine antes de renderizar contenido dependiente del rol, para evitar que la interfaz muestre
  brevemente un estado incorrecto (p. ej. el menú de administrador ausente para un administrador
  real).

## Acceptance Criteria

- AC-01: WHEN un visitante completa el registro con un nombre de usuario, contraseña y email no
  utilizados previamente, THE sistema SHALL crear la cuenta con rol por defecto y permitir iniciar
  sesión con esas credenciales. (FR-01, FR-07)
- AC-02: IF el email o el nombre de usuario ingresados en el registro ya están en uso, THEN THE
  sistema SHALL rechazar el registro con un mensaje genérico de registro fallido, sin indicar cuál
  de los dos campos está duplicado. (FR-02)
- AC-03: IF la contraseña ingresada en el registro tiene menos de 8 caracteres, más de 128
  caracteres, o no incluye letras y números, THEN THE sistema SHALL rechazar el registro e informar
  el motivo. (FR-03)
- AC-04: WHEN el usuario ingresa su usuario y contraseña correctos, THE sistema SHALL autenticarlo y
  emitir una sesión válida por 7 días. (FR-04)
- AC-05: IF el usuario o la contraseña ingresados son incorrectos, THEN THE sistema SHALL rechazar
  el intento de login con un mensaje genérico, sin indicar cuál de los dos campos falló. (FR-05)
- AC-06: WHEN un usuario con sesión activa cierra sesión, THE sistema SHALL invalidar su sesión,
  exigiendo autenticación nuevamente para acceder a funcionalidades protegidas. (FR-06)
- AC-07: WHEN la aplicación arranca con un token de sesión almacenado pero sin datos de usuario en
  memoria, THE sistema SHALL solicitar los datos de sesión actuales (usuario y rol) antes de
  resolver cualquier ruta protegida. (FR-08, NFR-04)
- AC-08: IF el token almacenado ya no corresponde a una sesión válida (expiró o fue invalidada),
  THEN THE sistema SHALL limpiar la sesión y redirigir a la pantalla de login, sin mostrar contenido
  protegido ni un mensaje adicional — mismo comportamiento que ya aplica hoy ante un 401. (FR-08)
- AC-09: WHEN la rehidratación de sesión se completa exitosamente, THE sistema SHALL reflejar el rol
  correcto en la interfaz (incluyendo el ítem de menú de Moderación para administradores) sin
  requerir un nuevo login. (FR-08)

## Out of Scope

- **RF-023, RF-034** Login con Google y vinculación automática de cuentas por email compartido.
- **RF-036 a RF-039, RF-055 a RF-057** Reseteo de contraseña completo.
- **RF-052** Bloqueo temporal de login tras intentos fallidos consecutivos. De baja prioridad para
  el circuito completo del producto (crear → moderar → publicar → descubrir); se corta para no
  agrandar este sub-ticket y queda para un ticket de seguridad de auth posterior.
- **Gestión de roles vía interfaz.** El campo de rol se modela en el esquema (FR-07), pero asignar
  "Administrador" a una cuenta es una tarea operativa fuera de la aplicación (a definir en PLAN —
  p. ej. seed de datos o configuración), no una funcionalidad expuesta al usuario. Esto ya está
  marcado como fuera de alcance del producto en `docs/daw/prd/PRD.md`.
- **RF-050** Pantalla de entrada según sesión (login / exploración). Depende de que exista la
  pantalla de exploración, que construye FEAT-001d — ese sub-ticket define el enrutamiento completo.
- **Renovación/rotación de sesión (refresh tokens).** FR-08 solo recupera los datos de una sesión
  que sigue siendo válida; no extiende su duración ni introduce un mecanismo de renovación más allá
  de los 7 días de NFR-03.

## Risks and Mitigations

### Enumeración de cuentas vía mensajes de registro duplicado

**Riesgo:** informar específicamente "email ya en uso" o "usuario ya en uso" permite a un atacante
enumerar qué emails/usuarios están registrados, probando valores contra el endpoint de registro.

**Mitigación:** mensaje genérico único de registro fallido (FR-02, AC-02), sin precisar cuál de los
dos campos está duplicado. Hallazgo del threat modeling de PLAN — ver
`docs/daw/security/threat-FEAT-001a.md`. Este cambio reemplazó el diseño original (que sí precisaba
el motivo) mediante el loop correctivo PLAN→DEFINE.

### Exposición por sesiones de larga duración

**Riesgo:** una sesión válida por 7 días (NFR-03) amplía la ventana de exposición ante un
dispositivo robado o compartido.

**Mitigación:** cierre de sesión explícito disponible (FR-06); sesión servida exclusivamente sobre
HTTPS (NFR-02). El bloqueo por intentos fallidos (RF-052) queda fuera de este sub-ticket — ver "Out
of Scope".

### Endpoint de sesión actual como superficie nueva (FR-08)

**Riesgo:** un endpoint que devuelve los datos de la sesión a partir del token es un nuevo punto que
un token robado o filtrado podría usar para obtener el rol de la cuenta.

**Mitigación:** el endpoint no expone nada que la sesión ya no exponga hoy — el rol viaja en la
respuesta de login (FR-04) sin protección adicional; FR-08 solo repite esa misma exposición en un
punto distinto, protegido por el mismo mecanismo de sesión (NFR-01/NFR-02). No amplía el radio de lo
que un token comprometido ya permitía. Detalle de amenazas específico a definir en el threat modeling
de PLAN de FEAT-007.

### Ataques de fuerza bruta sobre el login

**Riesgo:** sin RF-052 en este sub-ticket, el login no tiene límite de intentos.

**Mitigación:** aceptada como limitación temporal del MVP incremental; se revisa en el ticket que
retome RF-052. No hay reseteo de contraseña en este sub-ticket, por lo que el login es el único
vector expuesto y se documenta como riesgo conocido, no silencioso. El threat modeling de PLAN
agrega una mitigación complementaria de rate limiting básico — ver
`docs/daw/security/threat-FEAT-001a.md`.

## Dependencies

- SQL Server 2025 + EF Core para la persistencia de usuarios.
- Ninguna dependencia de otro sub-ticket — es el primero de la cadena a→b→c→d.
- FR-08/NFR-04 (agregado por FEAT-007) requiere un endpoint que devuelva los datos de la sesión
  actual a partir del token — a definir en PLAN de FEAT-007.
