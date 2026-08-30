# Threat Model — FIX-005: Resolver coordenadas reales de sugerencias `CALLEyPORTAL`

Referencia: `docs/daw/specs/fix-FIX-005.md`. Contexto previo: `docs/daw/security/threat-FEAT-011.md`
(las mitigaciones R1/R2/R4/R5/R6 de ese modelo siguen vigentes sin cambios para `search`/`reverse`;
este documento analiza específicamente el componente nuevo).

## Componente nuevo

`GET /api/addresses/resolve` (`AddressesController`, mismo `[Authorize]` +
`[EnableRateLimiting("addresses")]` heredado de la clase) → `ResolveAddressQuery`/Handler →
`IAddressProviderClient.ResolveAsync` → `IdeUruguayAddressProviderClient.ResolveAsync` →
`GET https://direcciones.ide.uy/api/v1/geocode/find?idcalle=&portal=&localidad=&type=` (mismo
`HttpClient` dedicado ya registrado vía `AddHttpClient` en `Program.cs`, sin `DelegatingHandler`
compartido con el resto de la API).

## Trust boundaries

1. **Frontend (no confiable) → backend propio** (`/api/addresses/resolve`): mismo boundary que
   `search`/`reverse`, protegido por sesión (`[Authorize]`) y rate limiting.
2. **Backend propio → proveedor externo** (`direcciones.ide.uy`): mismo boundary que `search`/
   `reverse`, mismo `HttpClient` dedicado (mitigación R4 de FEAT-011, sin fuga de sesión).

## STRIDE

| Categoría | Análisis |
|---|---|
| **Spoofing** | Sin cambios — `[Authorize]` exige sesión válida, igual que `search`/`reverse`. Sin superficie nueva. |
| **Tampering** | `localidad`/`type` son strings de query param controlados por el cliente, forwardeados al proveedor externo. Mitigado igual que `search`/`reverse` (R5 de threat-FEAT-011.md): siempre vía `Uri.EscapeDataString`, el host queda fijo por configuración — nunca derivado de input de usuario. |
| **Repudiation** | Mismo patrón de logging que `SearchAsync`/`ReverseGeocodeAsync`: `LogWarning` en fallos, sin datos sensibles (solo el mensaje de excepción). |
| **Information Disclosure** | `idCalle`/`portalNumber`/`localidad`/`type` son metadatos públicos de geocodificación (nombres de calle, ciudad, numeración), no PII — decisión ya tomada en PLAN de exponerlos también en `AddressSuggestionDto` de `/search`/`reverse`. Un usuario autenticado podría enumerar direcciones por `idCalle` arbitrario sin pasar antes por `/search` — mismo nivel de confianza que `search`/`reverse` ya tienen hoy (ambos aceptan cualquier query/coordenada de un usuario autenticado sin atarlo a una búsqueda previa); no es una superficie nueva, es el mismo modelo de confianza ya aceptado en FEAT-011. |
| **Denial of Service** | Mismo mitigante que `search`/`reverse` (R1 de threat-FEAT-011.md): policy `"addresses"` (20 req/min por IP) ya cubre el endpoint nuevo por ser `[EnableRateLimiting]` a nivel de clase, no por endpoint. `ResolveAsync` reutiliza el mismo `HttpClient` con timeout de 5s ya configurado — una llamada lenta al proveedor no puede colgar el hilo indefinidamente. |
| **Elevation of Privilege** | Ninguna — no toca ownership de murales ni roles, mismo nivel de autorización que `search`/`reverse` (sesión válida, sin chequeo de rol). |

## Riesgos identificados

| Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|
| R1 (heredado) — abuso saliente hacia el proveedor externo gratuito | D | Low | Medium | Ya mitigado: policy `"addresses"` compartida a nivel de clase (20 req/min por IP) cubre el endpoint nuevo automáticamente, sin cambios. |
| R2 — enumeración de direcciones por `idCalle` arbitrario sin pasar por `/search` | I | Low | Low | Aceptado por diseño: mismo nivel de confianza que `search`/`reverse` (datos públicos, sin PII, gateado por sesión + rate limit). No requiere mitigación adicional. |
| R3 — el proveedor externo cambia la forma de `/find` sin aviso (no versionado, no documentado en el swagger más allá de los parámetros) | T | Low | Low | `ResolveAsync` nunca propaga excepción (mismo contrato `AddressProviderResult`/`Unavailable` que `SearchAsync`/`ReverseGeocodeAsync`) — un cambio de forma cae en el catch genérico y se reporta como `Unavailable` (503), nunca un 500 ni un crash. |

**0 riesgos Critical/High.** R1 ya mitigado por diseño heredado; R2 y R3 aceptados sin mitigación
adicional — impacto Low en ambos casos, consistentes con decisiones ya tomadas en FEAT-011.

## Datos sensibles (F-TM-05)

Ninguno. `idCalle`, `portalNumber`, `localidad`, `type`, `address`, `lat`, `lng` son metadatos
públicos de geocodificación (calles, numeración, ciudades) — no PII, no credenciales, no datos
financieros.

## Veredicto

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 1 (endpoint nuevo, mismo       │
│    boundary que search/reverse)                              │
│  Trust boundaries declared: 2                                  │
│                                                                  │
│  Risks:                                                            │
│    🟢 LOW: R2 enumeración por idCalle arbitrario — aceptado,        │
│       datos públicos, mismo nivel que search/reverse                  │
│    🟢 LOW: R3 cambio de forma del proveedor sin aviso — mitigado        │
│       por el mismo contrato never-throw ya usado                          │
│                                                                              │
│  Mitigaciones a incorporar al spec: ninguna nueva — R1 ya cubierto por        │
│    la policy de clase existente, sin cambios de diseño requeridos.             │
│                                                                                    │
│  ─────────────────────────────────────────────────────                             │
│  Risks: C:0 H:0 M:0 L:2                                                              │
│  Report: docs/daw/security/threat-FIX-005.md                                          │
└─────────────────────────────────────────────────────────┘
```
