# Threat model FEAT-007: Rehidratar sesión (rol de usuario) al recargar la página

| Field | Value |
|-------|-------|
| Ticket | FEAT-007 |
| Spec | docs/daw/specs/spec-FEAT-007.md |
| Date | 2026-08-26 |

## Componentes analizados

1. `GET /api/auth/session` (`AuthController.Session`, Block 1) — nuevo endpoint `[Authorize]`.
2. `GetCurrentSessionQueryHandler` (Block 1) — lee `ClaimTypes.NameIdentifier`/`ClaimTypes.Role` del
   `ClaimsPrincipal`, consulta `Users` por `Id` solo para `Username`.
3. `AuthService.getCurrentSession()` / `rehydrateSessionOnStartup()` / `provideAppInitializer`
   (Block 3) — llama al endpoint anterior en cada arranque de la app cuando hay un token
   persistido.

## Trust boundary

Cliente (navegador, no confiable) → API (`GET /api/auth/session`, confiable) vía
`Authorization: Bearer {token}` — el mismo boundary ya establecido para `POST /api/auth/login`,
`POST /api/auth/logout` y el resto de los endpoints `[Authorize]`. No se introduce un tipo de
boundary nuevo, solo un endpoint más sobre el mismo mecanismo (`SessionAuthenticationHandler`, sin
tocar).

## STRIDE

| Categoría | Análisis |
|---|---|
| **Spoofing** | Ninguna superficie nueva: el mismo `SessionAuthenticationHandler` (token opaco, hash SHA-256, lookup por `Session.TokenHash`) protege este endpoint que a cualquier otro `[Authorize]`. |
| **Tampering** | Sin cambios — HTTPS obligatorio (NFR-02) ya cubre el tránsito. |
| **Repudiation** | N/A — es una consulta de solo lectura, sin efecto de escritura que requiera auditoría (a diferencia de Login/Logout, que sí crean/borran una fila `Session`). |
| **Information Disclosure** | El endpoint devuelve `{username, role}` **del propio dueño del token**, nunca de otro usuario — mismo dato que ya devuelve `POST /api/auth/login` en su response. `Role` sale del claim ya resuelto por `SessionAuthenticationHandler` (nunca de algo que el cliente envía), no se reconsulta ni se expone nada adicional. Sin disclosure nuevo. |
| **Denial of Service** | Este endpoint se llama en cada arranque de la app (no solo en login), aumentando el volumen de requests por usuario activo. Es una consulta de una sola fila por clave primaria (`Users.Id`), sin costo relevante. Igual que el resto de los endpoints autenticados del proyecto (`CreateMural`, `Moderation`), no tiene rate limiting propio — solo lo tiene `GET /api/discovery/nearby-murals` por ser anónimo (superficie de scraping). Un usuario autenticado que fuerce refrescos repetidos genera la misma carga que repetir cualquier otra acción autenticada; no es una superficie nueva de abuso. |
| **Elevation of Privilege** | Sin riesgo: `Role` se lee del claim ya resuelto server-side (mismo valor, misma fuente, mismo momento de resolución que ya usa `[Authorize(Roles=...)]` en el resto de la API) — el cliente no puede influir en qué rol recibe. |

## Riesgos

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| Volumen adicional de requests por refresh repetido | D | Baja | Bajo | Consulta de una sola fila por PK; mismo perfil de costo que cualquier otro endpoint autenticado sin rate limit propio en este proyecto. Sin mitigación nueva necesaria. |
| El interceptor HTTP navega a `/login` durante el bootstrap de la app si el token resultó inválido, mientras el router todavía se está resolviendo | — (no es de seguridad, es de robustez) | Baja | Bajo | `router.navigate()` en Angular encola la navegación aunque el router no haya terminado de inicializar; se verifica explícitamente en CODE con un test que ejercite el camino "token inválido" end-to-end. |

Sin riesgos CRITICAL ni HIGH. Sin datos nuevos clasificables como PII/credenciales (F-TM-05): el
`username`/`role` ya se consideraron en el threat model de FEAT-001a; este ticket no cambia su
clasificación ni introduce almacenamiento nuevo. No aplica F-TM-07 (sin credenciales nuevas
persistidas).

## Mitigaciones a incorporar al spec

Ninguna mitigación de seguridad nueva — ambos riesgos identificados son de impacto Bajo y ya están
cubiertos por patrones existentes del proyecto. El único punto a verificar en CODE (robustez, no
seguridad) es que la navegación a `/login` disparada por el interceptor durante el bootstrap
funcione correctamente — cubierto por los tests de `session-rehydration.initializer.spec.ts` ya
listados en el spec.

---

Riesgos: C:0 H:0 M:0 L:2
Resultado: **PASSED**
