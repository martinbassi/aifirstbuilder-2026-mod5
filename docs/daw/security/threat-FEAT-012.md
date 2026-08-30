# Threat Model — FEAT-012: Comando único para levantar frontend+backend visibles en la LAN

Referencia: `docs/daw/specs/spec-FEAT-012.md`.

## Componentes nuevos/modificados

1. Kestrel escuchando en `http://0.0.0.0:5267` (además de `https://localhost:7126`, que se
   preserva) cuando corre `scripts/dev-lan.sh`.
2. `Program.cs`: `app.UseHttpsRedirection()` saltea condicionalmente si `LAN_MODE=true`.
3. CORS: origen adicional agregado dinámicamente vía env var (`Cors__AllowedOrigins__1`).
4. CSP de desarrollo (`index.development.html`): `connect-src` extendido con `http://*:5267`.
5. `scripts/dev-lan.sh` (nuevo) — orquesta ambos procesos en la máquina del desarrollador.

## Trust boundaries

1. **Antes de este ticket**: el backend solo era alcanzable desde la propia máquina (Kestrel
   ligado a `localhost`) — el trust boundary era "este proceso confía en llamadas de esta misma
   máquina".
2. **Con este ticket, en modo LAN**: el backend pasa a ser alcanzable desde **cualquier
   dispositivo de la misma red local** — el trust boundary se amplía a "esta máquina confía en
   cualquier dispositivo de su LAN". Es un cambio real de superficie, acotado explícitamente a un
   comando opt-in (nunca el flujo por defecto, AC-03/AC-04) y a la propia red privada del
   desarrollador (NFR-01, sin port forwarding).

## STRIDE

| Categoría | Análisis |
|---|---|
| **Spoofing** | Un dispositivo de la LAN no gana ninguna capacidad de suplantar un usuario autenticado — `[Authorize]` y el token de sesión (FEAT-001a) siguen siendo la única forma de autenticarse, sin cambios. |
| **Tampering** | **Riesgo real (R1, ver abajo)**: HTTP plano dentro de la LAN permite que cualquier dispositivo con acceso a esa red observe/modifique tráfico en tránsito. |
| **Repudiation** | Sin cambios — mismo logging de siempre. |
| **Information Disclosure** | **Mismo riesgo que Tampering (R1)**: tokens de sesión, credenciales de login, fotos de murales y coordenadas viajan en texto plano dentro de la LAN mientras el modo LAN está activo. También **R2** (CSP `connect-src` con wildcard de host, ver abajo). |
| **Denial of Service** | Cualquier dispositivo de la LAN puede ahora golpear la API (antes solo el propio frontend del desarrollador podía). Mitigado por el rate limiting ya existente por IP (`"addresses"`, `"discovery"`, límites de auth) — sin cambios en esos límites, la superficie de *quién* puede alcanzarlos crece, pero el límite por IP sigue aplicando igual. |
| **Elevation of Privilege** | Ninguna — mismos roles/autorización de siempre, el modo LAN no toca la lógica de negocio. |

## Riesgos identificados

| Riesgo | STRIDE | Likelihood | Impact | Severidad | Mitigación |
|---|---|---|---|---|---|
| **R1** — HTTP plano en la LAN expone tokens de sesión, credenciales y datos (fotos, coordenadas) a cualquiera que pueda observar el tráfico de esa red | T, I | Medium (depende de qué tan confiable es la red del desarrollador) | High | **HIGH** | Sin mitigación técnica — es el trade-off elegido explícitamente en DEFINE (HTTP sin fricción vs. instalar el certificado de desarrollo en cada dispositivo). **Riesgo aceptado por el usuario** (ver abajo). |
| **R2** — CSP `connect-src http://*:5267` (wildcard de host) permite que, SI existiera un XSS en la página, un script inyectado exfiltre datos a cualquier host en el puerto 5267 (no solo al backend legítimo) | I | Low (requiere un XSS preexistente, ninguno conocido en el proyecto) | Medium | Low-Medium | Acotado a `index.development.html` exclusivamente (nunca `index.html` de producción, mismo patrón que FIX-002); acotado a un puerto específico, no `connect-src *`. No requiere aceptación formal — el riesgo residual es bajo y la superficie ya está minimizada por diseño. |
| **R3** — El backend pasa a ser alcanzable por cualquier dispositivo de la LAN, no solo `localhost` | D | Low | Low | Low | `[Authorize]` y el rate limiting por IP ya existentes no cambian — la superficie de *quién puede intentar* crece, pero los controles de *qué puede lograr* no se relajan. |
| **R4** — El origen CORS agregado dinámicamente (`Cors__AllowedOrigins__1`) amplía el whitelist más allá del `localhost:4200` fijo actual | S (débil) | Low | Low | Low | Coincidencia exacta de string (host:puerto), no un patrón/wildcard; gateado por `IsDevelopment()`, nunca en producción. |

## R1 — Riesgo aceptado (F-TM-04)

- **Quién lo acepta**: el usuario (dueño del proyecto), confirmado explícitamente en la sesión de
  PLAN de FEAT-012, reafirmando la decisión ya tomada en DEFINE.
- **Justificación**: el tráfico queda dentro de la LAN privada del desarrollador (no una red
  pública/compartida), exclusivamente para pruebas manuales de desarrollo — nunca el flujo por
  defecto (`dotnet run`/`ng serve` sin el script siguen usando HTTPS local, AC-03/AC-04) ni algo
  que llegue a producción. La alternativa (instalar el certificado de desarrollo en cada
  dispositivo de prueba) fue evaluada y rechazada en DEFINE por la fricción que introduce —
  contradice el objetivo del ticket ("un comando único").
- **Condiciones de revisión**: reevaluar si el modo LAN se llegara a usar alguna vez en una red no
  confiable (WiFi pública, coworking, redes compartidas con terceros) — en ese caso, HTTP plano no
  es aceptable y hay que volver a la alternativa de instalar el certificado. También reevaluar si
  el alcance de esta feature creciera más allá de pruebas manuales puntuales del desarrollador.

## Datos sensibles (F-TM-05)

Los mismos que ya viajan por la API hoy (tokens de sesión, credenciales de login, fotos de murales,
coordenadas de geolocalización) — este ticket no introduce datos sensibles nuevos, solo cambia el
canal de transporte (HTTP en vez de HTTPS) para el caso específico del modo LAN. Cifrado en tránsito
normalmente vía TLS (HTTPS) se mantiene para el flujo por defecto (AC-03/AC-04) y se pierde
deliberadamente solo dentro del modo LAN opt-in (R1, riesgo aceptado).

## Veredicto

```
┌─────────────────────────────────────────────────────────┐
│  /daw-threat-modeling — PASSED                            │
├─────────────────────────────────────────────────────────┤
│  Attack surfaces identified: 5 (Kestrel LAN binding,          │
│    UseHttpsRedirection condicional, CORS dinámico, CSP           │
│    connect-src, script nuevo)                                      │
│  Trust boundaries declared: 2 (antes/después del modo LAN)           │
│                                                                          │
│  Risks:                                                                    │
│    🟠 HIGH: R1 HTTP plano en LAN — sin mitigación técnica,                    │
│       ACEPTADO por el usuario (3 campos F-TM-04 completos)                       │
│    🟡 MEDIUM/LOW: R2 CSP wildcard de host — mitigado por scope                      │
│       (solo index.development.html, solo un puerto)                                   │
│    🟢 LOW: R3 superficie de alcance ampliada — controles de autorización                  │
│       y rate limiting sin cambios                                                            │
│    🟢 LOW: R4 CORS dinámico — coincidencia exacta, sin wildcard                                │
│                                                                                                    │
│  Mitigaciones a incorporar al spec: ninguna nueva de código — R1 es un                              │
│    riesgo aceptado por diseño (trade-off ya reflejado en el spec), R2-R4                              │
│    ya están acotados por el diseño existente.                                                            │
│                                                                                                              │
│  ─────────────────────────────────────────────────────                                                        │
│  Risks: C:0 H:1 (aceptado) M:0 L:3                                                                              │
│  Report: docs/daw/security/threat-FEAT-012.md                                                                     │
└─────────────────────────────────────────────────────────┘
```
