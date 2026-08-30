# Threat Model FEAT-011: Autocompletar dirección en formulario de carga de mural

| Field | Value |
|-------|-------|
| Ticket | FEAT-011 |
| Spec | docs/daw/specs/spec-FEAT-011.md |
| Date | 2026-08-29 |

## Componentes nuevos/modificados

- `AddressesController` (nuevo) — `GET /api/addresses/search`, `GET /api/addresses/reverse`.
- `IAddressProviderClient` / `IdeUruguayAddressProviderClient` (nuevo) — integración saliente con
  `direcciones.ide.uy`.
- `SearchAddressesQuery` / `ReverseGeocodeQuery` + Handlers (nuevo).
- `address.service.ts` (nuevo, frontend).
- `create-mural-form.component` (modificado) — renderiza texto devuelto por el proveedor externo.

## Trust boundaries

| Boundary | Nivel de confianza | Ya existía |
|---|---|---|
| Browser (usuario autenticado) → `AddressesController` | Untrusted → Trusted, mediado por `[Authorize]` | Sí (mismo mecanismo que `MuralsController`) |
| Backend → `direcciones.ide.uy` (proveedor externo) | Trusted → **Untrusted/tercero** | **No — nueva** |

La segunda boundary es la novedad real de este ticket: es la primera vez que el backend hace una
llamada HTTP saliente hacia un servicio de un tercero fuera de Azure (Azure Storage y NsfwSpy corren
dentro del propio perímetro de infraestructura del proyecto).

## STRIDE por componente

### `AddressesController`

| Categoría | Análisis |
|---|---|
| Spoofing | Mitigado por `[Authorize]`, mismo mecanismo de sesión que el resto de la API. |
| Tampering | `q`/`lat`/`lng` son query params validados por FluentValidation (`NotEmpty`/`MaximumLength`, `InclusiveBetween`) antes de llegar al Handler. |
| Repudiation | Sin logging de auditoría dedicado — aceptable: son lecturas no mutantes (no crean/modifican datos), mismo criterio que `GetMuralByIdQuery`. |
| Information Disclosure | No expone datos de otros usuarios; la respuesta es pública de un servicio gubernamental de geocodificación. |
| Denial of Service | Ver riesgo R1 abajo. |
| Elevation of Privilege | Ninguno — solo requiere sesión válida, no un rol distinguido (igual que crear un mural). |

### `IdeUruguayAddressProviderClient` (integración con `direcciones.ide.uy`)

| Categoría | Análisis |
|---|---|
| Spoofing | N/A (es el backend llamando hacia afuera, no al revés). |
| Tampering | Ver riesgo R2 (MITM en tránsito). |
| Repudiation | N/A para una llamada saliente de solo lectura. |
| Information Disclosure | Ver riesgos R3 (texto de búsqueda hacia el tercero) y R4 (fuga de sesión). |
| Denial of Service | Ver riesgo R1 (timeout/rate limit) y R5 (SSRF descartado por diseño). |
| Elevation of Privilege | N/A. |

## Clasificación de datos sensibles (F-TM-05)

- `q` (texto de búsqueda) y `lat`/`lng`: **datos de ubicación**, no PII directa ni credenciales. Es
  la misma categoría de dato que `Mural.Latitude`/`Longitude`, ya aprobada y clasificada en el
  threat model de FEAT-001b — este ticket no introduce una categoría nueva de dato sensible, solo un
  nuevo destinatario (el proveedor externo) para datos que el frontend ya recolectaba.
- No hay credenciales, tokens de sesión ni PII directa (nombre, email) en el flujo hacia el
  proveedor externo (ver R4/mitigación).

## Riesgos identificados

| ID | Riesgo | STRIDE | Likelihood | Impact | Mitigación |
|---|---|---|---|---|---|
| R1 | El proveedor externo gratuito responde lento o el endpoint es invocado en ráfaga mientras el usuario escribe, agotando threads/conexiones salientes del backend. | D | Medium | Medium | `HttpClient.Timeout = 5s` (nunca bloquea indefinidamente) + policy de rate limiting `"addresses"` (20 req/min por IP, mismo esquema que `"discovery"`). Ambas ya en el spec (Block 1). |
| R2 | Un MITM en la red intercepta/modifica la respuesta del proveedor externo (tampering en tránsito). | T | Low | Medium | `AddressProvider:BaseUrl` fijo en `https://` — nunca `http://`. Ya en el spec (Block 1). |
| R3 | El texto que el usuario escribe (posible dirección de su casa) se envía a un tercero (`direcciones.ide.uy`) fuera de la infraestructura del proyecto. | I | High (es inherente al propósito de la feature) | Low | Es el comportamiento esperado y explícito del feature (aprobado en el PRD); el proveedor es un servicio público de geocodificación oficial de Uruguay (IDE), no un tercero arbitrario. No se envían más datos que `q`/`lat`/`lng` — nunca identidad del usuario (ver R4). Aceptado por diseño, no requiere mitigación adicional más allá de R4. |
| R4 | El `HttpClient` hacia el proveedor externo reutiliza por error algún handler de autenticación de la API propia, filtrando la cookie/token de sesión del usuario a un tercero. | I | Low | **High** | `AddHttpClient<IAddressProviderClient, IdeUruguayAddressProviderClient>` como cliente **dedicado**, sin compartir `DelegatingHandler`/`AuthenticationHandler` con el resto de la API — solo transporta `q`/`lat`/`lng`. Ya en el spec (Block 1). |
| R5 | SSRF: un atacante intenta que el backend haga una request a un host arbitrario a través de los parámetros de búsqueda/reverse. | T/I | Low | **High** | El host (`AddressProvider:BaseUrl`) es fijo por configuración, nunca derivado de `q`/`lat`/`lng` ni de ningún input del request — el Handler/Cliente solo interpola esos valores en la query string de una URL cuyo host ya está fijado. Descartado por diseño, no por un filtro runtime. Ya en el spec (Block 1). |
| R6 | XSS almacenado/reflejado: el proveedor externo (o un MITM que evadiera R2) devuelve un `address` con markup/script, que se renderiza sin escapar en el formulario. | T→I/E | Low | **High** | El spec (Block 3) prohíbe explícitamente `[innerHTML]`/`bypassSecurityTrustHtml` para el texto de dirección — se renderiza únicamente vía interpolación/binding de Angular (`{{ }}`, `[value]`, `[ngModel]`), que escapa por defecto. |

## Mitigaciones ya incorporadas al spec (no quedan pendientes)

1. Timeout de 5s + rate limiting policy `"addresses"` (R1) — Block 1.
2. `BaseUrl` fijo en `https://` (R2) — Block 1.
3. `HttpClient` dedicado sin handlers de autenticación compartidos (R4) — Block 1.
4. Host fijo por configuración, nunca derivado de input (R5) — Block 1.
5. Prohibición explícita de `[innerHTML]`/`bypassSecurityTrustHtml` para el texto de dirección (R6) — Block 3.

Ningún riesgo queda como "accepted risk" sin mitigar — los de impacto alto (R4, R5, R6) tienen
mitigación de diseño concreta, no un riesgo aceptado a la espera de una decisión del usuario.
