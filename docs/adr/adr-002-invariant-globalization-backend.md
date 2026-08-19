# ADR-002: Activar InvariantGlobalization en toda la API backend

| Field | Value |
|-------|-------|
| Date | 2026-08-19 |
| Ticket | FEAT-001b |
| Status | Accepted |

## Context

El binding de `[FromForm] double` (`Latitude`/`Longitude` en `CreateMuralCommand`, Block 4) parseaba
mal valores negativos/decimales cuando el proceso corría bajo un locale con coma decimal (`es_ES`):
`"-34.6037"` se leía como `-346037`. La causa es que `CultureInfo.CurrentCulture` se toma del entorno
del proceso (`LANG`/`LC_ALL` del SO) salvo que algo la fije explícitamente, y el model binder de
tipos simples de ASP.NET Core parsea con esa cultura ambiente.

Un primer intento acotó el fix a un `ModelBinder` custom solo para `Latitude`/`Longitude`. Funcionaba,
pero no escala: cualquier otro endpoint futuro con `double`/`decimal`/`DateTime` desde form/query
repetiría el mismo bug.

## Options considered

### Option A: `InvariantGlobalization=true` a nivel de proyecto
- **Pros:** fix global y definitivo — cubre cualquier tipo numérico/fecha en cualquier endpoint
  presente o futuro, sin depender de que cada desarrollador recuerde aplicar un binder. Es el default
  recomendado por Microsoft para APIs JSON sin requisito de i18n. Una sola línea declarativa por
  proyecto, sin código de arranque.
- **Cons:** deshabilita ICU completo en el proceso — no solo el parseo culture-aware de
  números/fechas, también comparaciones de string y casing Unicode fuera de ASCII en toda la app.
  Debe aplicarse en **ambos** `.csproj` (`Paretto.Api` y `Paretto.Api.Tests`): es un `AppContext`
  switch fijado en el `runtimeconfig.json` de cada proceso, no algo que se herede vía
  `ProjectReference` — `WebApplicationFactory` hostea la API dentro del proceso del proyecto de
  tests, así que sin el flag ahí los tests no ejercitan el mismo comportamiento que producción (se
  verificó empíricamente: con el flag solo en `Paretto.Api`, el test bajo locale `es_ES` seguía
  fallando).

### Option B: fijar `CultureInfo.DefaultThreadCurrentCulture` en `Program.cs`
- **Pros:** más quirúrgico — no deshabilita ICU, solo fija la cultura por defecto de los threads
  nuevos (incluidos los de request de ASP.NET Core).
- **Cons:** código de arranque fácil de pisar sin darse cuenta (p. ej. si más adelante se agrega
  `RequestLocalizationMiddleware`); no resuelve el mismo problema en el proceso de test a menos que
  también se configure ahí explícitamente; menos descubrible que una propiedad del `.csproj`.

## Decision

Se eligió la **Opción A**. El proyecto es una API JSON pura consumida por un frontend Angular vía
NSwag, sin ningún requisito de i18n/localización en el PRD — nada en el dominio depende de
formateo/comparación sensible a cultura. Se auditó el código ya commiteado de Blocks 1-3
(Auth, Storage, Moderation) sin encontrar ningún uso de `ToUpper`/`ToLower`/`string.Compare`/
`CultureInfo`/`DateTime.Parse` que dependiera de una cultura no invariante. Bajo esas condiciones,
el costo de la Opción A (ICU deshabilitado) es nulo en la práctica, y su cobertura (todo tipo,
todo endpoint, presente y futuro) supera claramente a la Opción B.

## Consequences

- `<InvariantGlobalization>true</InvariantGlobalization>` agregado en
  `backend/src/Paretto.Api/Paretto.Api.csproj` y `backend/tests/Paretto.Api.Tests/Paretto.Api.Tests.csproj`.
- Se removió el `InvariantDoubleModelBinder` local que Block 4 había introducido como workaround
  puntual — queda redundante.
- Cualquier `double`/`decimal`/`DateTime` recibido por cualquier endpoint futuro parsea de forma
  invariante sin necesidad de tratamiento especial por endpoint.
- Limitación aceptada: si en el futuro el producto necesitara formateo/comparación
  culture-aware (p. ej. mostrar fechas en formato `es-AR`), habría que reintroducir `CultureInfo`
  explícito en ese punto puntual — el flag global no lo permite de forma ambiente.
