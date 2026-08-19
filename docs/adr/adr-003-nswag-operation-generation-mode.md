# ADR-003: Cambiar `operationGenerationMode` de NSwag a `MultipleClientsFromFirstTagAndOperationId`

| Field | Value |
|-------|-------|
| Date | 2026-08-19 |
| Ticket | FEAT-001b |
| Status | Accepted |

## Context

`backend/src/Paretto.Api/nswag.json` gobierna la generación del cliente HTTP Angular
(`api-client.generated.ts`) a partir del OpenAPI de la API — es infraestructura compartida por
todas las features del backend, no algo propio de `murals`.

Con `operationGenerationMode: "MultipleClientsFromPathSegments"` (el valor original, introducido en
FEAT-001a), NSwag agrupa las operaciones en clientes separados usando el segmento de ruta que sigue
al recurso base. `AuthController` funciona con este modo porque cada acción tiene su propio segmento
(`POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`) — NSwag separa
`AuthClient` sin problema.

`MuralsController` (Block 6, FEAT-001b) no tiene esa topología: sus rutas son `POST /api/murals` y
`GET /api/murals/{id}`, sin segmento de acción propio después del recurso. Bajo
`MultipleClientsFromPathSegments`, NSwag no encuentra un segmento por el cual separar un cliente
propio y las operaciones de `MuralsController` terminan cayendo en el `ApiClient` genérico en vez de
en una clase `MuralsClient` — lo cual rompe el patrón que el proyecto usa en `mural.service.ts`
(`inject(MuralsClient)`, ver AGENTS.md: "Feature `data/` services wrap calls to the generated
client").

## Options considered

### Option A: mantener `MultipleClientsFromPathSegments` y adaptar `MuralsController`
- **Pros:** no toca configuración compartida.
- **Cons:** requeriría introducir un segmento de ruta artificial (p. ej. `/api/murals/create`) solo
  para satisfacer al generador — cambia el contrato HTTP real por una razón puramente de tooling, y
  ya está commiteado y verificado en producción (Block 4/6) el shape actual de las rutas.

### Option B: cambiar `operationGenerationMode` a `MultipleClientsFromFirstTagAndOperationId`
- **Pros:** agrupa por el tag de OpenAPI del controller (`Murals`, `Auth`) en vez de por segmento de
  ruta — separa `MuralsClient` correctamente sin tocar las rutas HTTP. Es un modo soportado
  nativamente por NSwag para exactamente este caso (controllers cuyas rutas no tienen un segmento de
  acción propio).
- **Cons:** los nombres de método generados dejan de ser semánticos por acción y pasan a construirse
  a partir del verbo HTTP + operationId, dando nombres como `muralsPOST`/`muralsGET` en vez de algo
  como `create`/`getById`.

## Decision

Se eligió la **Opción B**. Se verificó, regenerando el cliente con ambos modos:

- **Necesario:** sin el cambio, `MuralsController` nunca produce una clase `MuralsClient` separada
  — cae todo en el `ApiClient` genérico, incompatible con el patrón `inject(MuralsClient)` que ya usa
  `mural.service.ts`.
- **Inocuo para `AuthClient`:** el diff del bloque de `AuthClient` generado con el modo nuevo es
  byte a byte idéntico al generado con el modo anterior — el cambio no afecta a Block 5/6 (Auth),
  ya cerrado en FEAT-001a.

## Consequences

- `backend/src/Paretto.Api/nswag.json`: `operationGenerationMode` cambiado de
  `MultipleClientsFromPathSegments` a `MultipleClientsFromFirstTagAndOperationId`. Afecta a toda
  regeneración futura del cliente (`api-client.generated.ts`), no solo a `murals`.
  - **Nada más se revierte ni se toca**: no hace falta ningún cambio adicional en `AuthController`
    ni en `auth.service.ts` — el diff generado para `AuthClient` es idéntico.
- **Costo aceptado:** los métodos generados en `MuralsClient` tienen nombres poco semánticos
  (`muralsPOST`, `muralsGET`) en vez de nombres por acción. Se mitiga porque `MuralService`
  (`frontend/src/app/features/murals/data/mural.service.ts`) envuelve esos métodos crudos con
  `create()`/`getById()` — ningún componente ve los nombres generados directamente, per AGENTS.md
  ("components... always go through a feature service in `data/`").
- Si en el futuro se quiere un nombre de método generado más limpio, la vía correcta es anotar
  `OperationId` explícito por acción en `MuralsController` (p. ej. vía `[SwaggerOperation]` o el
  atributo equivalente), **no** volver a tocar `operationGenerationMode` en `nswag.json`.
