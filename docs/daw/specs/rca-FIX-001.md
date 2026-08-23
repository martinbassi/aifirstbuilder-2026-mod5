# RCA FIX-001: Configurar CORS para desarrollo local

| Campo | Valor |
|-------|-------|
| Ticket | FIX-001 |
| Fecha | 2026-08-23 |
| Reportado por | Usuario, al correr la app localmente desde VS Code |

## Síntoma

Al registrarse desde el frontend (`http://localhost:4200`) contra la API (`https://localhost:7126`),
el navegador bloquea la request con:

```
Access to XMLHttpRequest at 'https://localhost:7126/api/auth/register' from origin
'http://localhost:4200' has been blocked by CORS policy: Response to preflight request doesn't
pass access control check: No 'Access-Control-Allow-Origin' header is present on the requested
resource.
```

## Root cause

`backend/src/Paretto.Api/Program.cs` nunca registró `builder.Services.AddCors(...)` ni invocó
`app.UseCors(...)` en el pipeline de middleware. El pipeline actual pasa directo de
`app.UseRateLimiter()` a `app.UseAuthentication()` / `app.UseAuthorization()` /
`app.MapControllers()` (líneas 151-158), sin ningún paso de CORS.

El frontend (`ng serve`, `http://localhost:4200`) y la API (`dotnet run`, perfil `https`,
`https://localhost:7126`) son dos orígenes distintos por diseño: no hay `proxy.conf.json` de
Angular que enrute las llamadas a través del mismo origen del dev server. Con orígenes distintos
(protocolo y puerto difieren), cualquier request desde el navegador dispara un preflight `OPTIONS`,
y sin `Access-Control-Allow-Origin` en la respuesta el navegador bloquea la request real antes de
que llegue al controller.

## Componente afectado

`backend/src/Paretto.Api/Program.cs` — configuración transversal del pipeline, afecta a **todos**
los controllers (no es específico de `/api/auth/register`; cualquier endpoint expuesto vía navegador
desde el frontend en desarrollo tiene el mismo problema).

## Por qué no se detectó en FEAT-001a/b/c/d

Los tests de integración de las 4 features previas usan `WebApplicationFactory<Program>`, que
invoca los controllers directamente sobre el mismo proceso in-memory — nunca pasa por un navegador
real, nunca envía un header `Origin` cross-origin y nunca dispara un preflight `OPTIONS`. El gap es
invisible a cualquier gate automatizado del pipeline (tests, SAST, arch-audit): SAST no evalúa
ausencia de configuración de runtime (no hay patrón de vulnerabilidad que buscar), y arch-audit no
tiene ninguna convención declarada en `AGENTS.md` sobre CORS. Solo se manifiesta corriendo frontend y
API por separado contra un navegador real — que es exactamente lo que reveló el síntoma.

## PRD relacionado

Ninguno. Se revisó `docs/daw/prd/` completo — ningún PRD de FEAT-001a/b/c/d cubre configuración de
CORS ni infraestructura de desarrollo local. No hay gap de PRD que resolver.

## Confirmación

Confirmado por el usuario el 2026-08-23.
