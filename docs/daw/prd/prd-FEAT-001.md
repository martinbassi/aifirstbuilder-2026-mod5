# Parent PRD: Registro/login básico y carga + descubrimiento de murales cercanos

| Metric | Value |
|--------|-------|
| Ticket | FEAT-001 |
| Date | 2026-08-15 |
| Status | Split |

## Sub-tickets

| Sub-ticket | Title | PRD | Dependencies | Status |
|---|---|---|---|---|
| FEAT-001a | Autenticación básica | prd-FEAT-001a.md | none | done — mergeado a main (PR [#1](https://github.com/martinbassi/aifirstbuilder-2026-mod5/pull/1)) |
| FEAT-001b | Crear mural | prd-FEAT-001b.md | depends on a | done — mergeado a main (PR [#2](https://github.com/martinbassi/aifirstbuilder-2026-mod5/pull/2)) |
| FEAT-001c | Moderación mínima | prd-FEAT-001c.md | depends on a, b | done — PR [#3](https://github.com/martinbassi/aifirstbuilder-2026-mod5/pull/3) creado (borrador), pendiente de merge por el usuario |
| FEAT-001d | Descubrir murales cercanos | prd-FEAT-001d.md | depends on b, c | active |

## Suggested implementation order
a → b → c → d

> **The `Status` column is maintained, not decorative.** RELEASE's closeout moves the finished
> sub-ticket to `done` — with where its branch landed — and the next one to `active`.

## Original context

Primer ticket FEATURE del proyecto: cargar y ver murales cercanos. El PRD inicial (30 ACs, 4 áreas:
auth, crear mural, descubrir, storage) resultó demasiado grande para un solo ticket (umbral: 5-7
ACs). Al revisarlo con el usuario, se agregó moderación mínima (aprobar/rechazar/listar pendientes)
para que el producto cierre el circuito completo — crear → moderar → publicar → descubrir — en vez
de terminar en murales que nunca salen de "pendiente". Para compensar el tamaño, se cortaron dos
ítems de baja prioridad que no bloquean ese circuito: RF-052 (bloqueo de login tras intentos
fallidos) y RF-021 (ampliar radio de búsqueda en pasos automáticos). Ambos quedan pendientes para un
ticket futuro.

División resultante, en 4 sub-tickets encadenados:
- **a. Autenticación básica** — registro, login, logout, y el modelo de usuario con su campo de rol
  (Explorador/Colaborador/Administrador), aunque solo el sub-ticket c use el valor Administrador.
- **b. Crear mural** — foto, ubicación (GPS/manual), validación NSFW, guardado con estado
  "pendiente", casos límite, y el servicio de fotos vía URL firmada (RNF-009) — construido acá
  porque es donde se sube y almacena la foto por primera vez, y lo reutilizan c y d.
- **c. Moderación mínima** — aprobar/rechazar un mural pendiente, listar pendientes, restringido al
  rol Administrador. Reutiliza el servicio de fotos de b para que el administrador pueda revisar la
  imagen.
- **d. Descubrir murales cercanos** — búsqueda por radio (default 5 km, sin auto-expansión), mapa,
  lista, detalle, pantalla de entrada según sesión. Reutiliza el servicio de fotos de b.

Referencia completa de requerimientos del producto: `docs/daw/prd/PRD.md`.
