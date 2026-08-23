# Threat Model FIX-001: Configurar CORS para desarrollo local

| Campo | Valor |
|-------|-------|
| Ticket | FIX-001 |
| Fecha | 2026-08-23 |
| Diseño analizado | `backend/src/Paretto.Api/Program.cs` — nueva policy CORS `DevelopmentCors`, gateada por `IsDevelopment()`; nueva sección `Cors:AllowedOrigins` en `appsettings.Development.json` |

## Superficies de ataque identificadas

1. **Nueva policy CORS en el pipeline HTTP** (`Program.cs`): decide qué orígenes del navegador
   pueden leer las respuestas de la API vía JavaScript. Superficie nueva, activa solo cuando
   `IsDevelopment()` es verdadero.
2. **Nueva sección de configuración** (`appsettings.Development.json:Cors:AllowedOrigins`): una
   lista de orígenes de texto plano, sin datos sensibles.

## Trust boundary

**Navegador (origen no confiable, controla el header `Origin`) ↔ API (servidor).** CORS es
precisamente el mecanismo por el cual el navegador decide si permite que JavaScript de un origen lea
la respuesta de otro origen — es una protección del lado del navegador, no un mecanismo de
autenticación ni autorización del servidor. Esa distinción es la base de los riesgos de abajo:
declarar la boundary correctamente es lo que se está mitigando.

## Análisis STRIDE

| Categoría | Aplica | Análisis |
|---|---|---|
| **Spoofing** | Sí, riesgo bajo aceptado | El header `Origin` lo controla el propio navegador cuando el request sale de JS — no es spoofeable por JS malicioso en otro origen. Herramientas no-navegador (curl, Postman) pueden enviar cualquier `Origin`, pero CORS nunca fue una barrera contra ellas: no hay enforcement de CORS fuera del navegador. La autenticación real sigue siendo el header `Authorization: Bearer`, sin cambios por este fix. |
| **Tampering** | No aplica | CORS no modifica datos en tránsito ni en reposo. |
| **Repudiation** | No aplica | No se agrega ni se quita logging; el pipeline de auditoría existente no cambia. |
| **Information Disclosure** | Sí, ver R1 y R2 | Ver riesgos abajo. |
| **Denial of Service** | Sí, ver R1 | Ver riesgo abajo. |
| **Elevation of Privilege** | No aplica | CORS no otorga privilegios — la autorización sigue ocurriendo en `UseAuthentication()`/`UseAuthorization()`, después de `UseCors()` en el pipeline, sin cambios. |

## Datos sensibles (F-TM-05)

Ninguno nuevo. La única información que este fix agrega es una lista de URLs de origen en
`appsettings.Development.json` — no hay PII, credenciales ni datos financieros involucrados. No
aplica cifrado en reposo/tránsito adicional (F-TM-07).

## Riesgos

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| **R1** — Si `AddCors` se registrara sin gatear por entorno y `Cors:AllowedOrigins` falta en `appsettings.json` base (Production no tiene `appsettings.Production.json`), `WithOrigins(null)` lanza `ArgumentNullException` al arrancar — tumba el proceso en Production aunque `UseCors` nunca se invoque ahí (hallazgo del impact scan). | D (Denial of Service) | Media (error de diseño fácil de cometer) | Alta (crashea el arranque del proceso) | **Doble mitigación, ya incorporada al diseño antes de escribir el fix-plan:** (a) todo el bloque `AddCors(...)` se registra dentro de `if (builder.Environment.IsDevelopment())` — en Production nunca se ejecuta; (b) defensa en profundidad: la lectura de la sección usa `?? Array.Empty<string>()`, así que incluso si alguien mueve el registro fuera del gate en el futuro, nunca lanza — simplemente registra una policy sin orígenes permitidos (fail-safe: deniega todo en vez de crashear). |
| **R2** — Alguien copia este patrón a un futuro `appsettings.Production.json` sin las debidas restricciones (p. ej. agregando el dominio real de producción a la misma policy "DevelopmentCors", o usando `AllowAnyOrigin()`). | I (Information Disclosure) | Baja | Media | Comentario explícito en `Program.cs`, junto a la policy, dejando constancia de que `DevelopmentCors` es exclusiva de desarrollo local y que producción, cuando exista, necesita su propia policy con el dominio real — no reutilizar esta. |
| **R3** — La policy usa `AllowAnyMethod()`/`AllowAnyHeader()` en vez de una lista explícita de métodos/headers. | I (Information Disclosure) | Baja | Baja | **Riesgo aceptado.** La policy solo aplica en `Development` y está acotada por `WithOrigins` a un origen explícito (`http://localhost:4200`, el propio dev server del desarrollador) — el trust boundary real ya está cerrado por el origin check, no por los métodos/headers permitidos. Restringir más agregaría mantenimiento (mantener la lista de headers sincronizada con lo que el frontend realmente envía) sin reducir superficie de ataque real, dado que no hay `AllowCredentials()` de por medio. **Aceptado por:** Martin Bassi (usuario). **Justificación:** entorno local de un solo desarrollador, sin credentials en la policy, origen ya acotado. **Revisar cuando:** este patrón se extienda a un entorno compartido (staging/producción) o se agregue `AllowCredentials()` a cualquier policy CORS del proyecto. |

## Mitigaciones a incorporar al fix-plan

1. `AddCors(...)` completo dentro de `if (builder.Environment.IsDevelopment())`, con fallback
   `?? Array.Empty<string>()` al leer `Cors:AllowedOrigins` (R1).
2. Comentario en `Program.cs` junto a la policy, documentando que es exclusiva de desarrollo local
   (R2).
3. Sin `AllowCredentials()` en ningún momento — la auth usa `Authorization: Bearer`, no cookies
   (ya validado en el impact scan; documentado acá como decisión de diseño, no como mitigación
   pendiente).

---

**Total: 0 CRITICAL, 0 HIGH, 2 MEDIUM (R1 mitigado en el diseño, R2 mitigado con documentación),
1 LOW (R3, aceptado por el usuario).**

**Resultado: PASSED** — toda mitigación queda incorporada al fix-plan antes de escribirlo a disco.
