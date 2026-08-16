# SAST — FEAT-001a (Autenticación básica)

**Fecha:** 2026-08-16
**Alcance:** diff completo del ticket contra `main` (94 archivos) — `backend/src/Paretto.Domain`,
`backend/src/Paretto.Infrastructure`, `backend/src/Paretto.Api` (Auth, Security, middleware,
`Program.cs`) y `frontend/src/app` (`features/auth`, `core/interceptors`, `core/api-client`,
`app.config.ts`, `app.routes.ts`, `index.html`).

```
┌─────────────────────────────────────────────────────────────┐
│  /daw-security-sast — PASSED                                 │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Secrets:                                                    │
│    ✅ F-SAST-01: sin secretos hardcodeados. Los únicos       │
│       literales `password = "..."` están exclusivamente en   │
│       archivos de test (*Tests.cs, *.spec.ts) — confirmado   │
│       con grep restringido a esos archivos. appsettings.json │
│       / appsettings.Development.json y el fallback de        │
│       AppDbContextFactory.cs usan Trusted_Connection=True /  │
│       placeholders sin credenciales; las credenciales reales │
│       de SQL Server viven en dotnet user-secrets, fuera del  │
│       repo.                                                  │
│                                                              │
│  Injection:                                                  │
│    ✅ F-SAST-02: sin FromSqlRaw/ExecuteSqlRaw/SqlQuery en     │
│       ningún archivo del diff — todo el acceso a datos pasa  │
│       por LINQ/EF Core parametrizado (ej.                    │
│       RegisterUserCommand.cs: Handle(), AnyAsync con lambda).│
│    ✅ F-SAST-03: sin Process.Start/exec/child_process en el   │
│       diff.                                                  │
│    ✅ F-SAST-05: no aplica — el ticket no maneja paths de     │
│       archivo con input de usuario.                          │
│                                                              │
│  XSS y funciones inseguras:                                  │
│    ✅ F-SAST-06: sin innerHTML/dangerouslySetInnerHTML/       │
│       bypassSecurityTrust en frontend/src/app. Angular        │
│       escapa por defecto vía interpolación; CSP presente en   │
│       index.html (script-src 'self', sin unsafe-eval/         │
│       unsafe-inline).                                         │
│    ✅ F-SAST-04/F-SAST-17: sin eval()/new Function()/          │
│       deserialización insegura en el diff.                    │
│    ✅ F-SAST-08: PasswordHasher.cs usa                         │
│       Microsoft.AspNetCore.Identity.PasswordHasher&lt;User&gt;      │
│       (PBKDF2 + salt aleatoria por password, comportamiento    │
│       por defecto de la librería). SessionTokenGenerator.cs    │
│       usa SHA-256 sobre un token opaco de 256 bits generado    │
│       con RandomNumberGenerator — correcto para este caso:      │
│       no es una contraseña de baja entropía, es un secreto      │
│       de alta entropía que solo necesita un hash rápido para    │
│       el lookup en DB (el token crudo nunca se persiste).       │
│                                                              │
│  Configuración / exposición:                                  │
│    ❌→✅ F-SAST-09 (High, encontrado y corregido en este         │
│       cierre): Program.cs llamaba `app.UseSwagger()` /          │
│       `app.UseSwaggerUI()` incondicionalmente — el explorador    │
│       OpenAPI (rutas, DTOs, requisitos de auth) quedaba          │
│       accesible sin autenticar en cualquier entorno, incluido    │
│       uno de producción hipotético (OWASP A05:2021 - Security    │
│       Misconfiguration). Corregido: ambas llamadas ahora          │
│       están detrás de `if (app.Environment.IsDevelopment())`.    │
│       Re-verificado: `HealthCheckTests.SwaggerJson_ReturnsOk`     │
│       sigue en verde (WebApplicationFactory usa Development       │
│       por defecto), 26/26 tests de backend sin regresión.         │
│    ✅ F-SAST-10: LoggingBehavior.cs solo loguea el nombre del     │
│       tipo de Request y la duración — nunca el contenido de       │
│       los campos (username/password/token nunca llegan al         │
│       logger).                                                     │
│    ✅ F-SAST-11: no aplica — el ticket no implementa upload de     │
│       archivos.                                                    │
│    ✅ F-SAST-12: no aplica — la sesión viaja por header             │
│       `Authorization: Bearer`, nunca por cookie; CSRF ataca         │
│       específicamente credenciales que el navegador adjunta         │
│       automáticamente (cookies), lo que no ocurre acá (ya            │
│       evaluado como no aplicable en el threat model, R5).            │
│    ✅ F-SAST-14: FluentValidation cubre tipo/longitud/formato en     │
│       RegisterUserCommandValidator y LoginCommand (Username ≤ 50,    │
│       Email formato válido ≤ 256, Password 8-128 con letra+dígito).  │
│       RegisterUserCommand no expone un campo Role bindeable           │
│       (mitigación R1 del threat model contra elevación de              │
│       privilegios vía payload).                                        │
│    ✅ F-SAST-15: ExceptionHandlingMiddleware.cs solo incluye           │
│       `ex.ToString()` en el 500 cuando `_environment.IsDevelopment()`  │
│       — en cualquier otro entorno el detalle interno nunca se          │
│       expone al cliente.                                                │
│                                                              │
│  Dependencies:                                                │
│    ✅ F-SAST-13/16: `npm audit` (frontend) → 0 vulnerabilidades.  │
│       `dotnet list package --vulnerable --include-transitive`     │
│       (los 4 proyectos del backend) → sin paquetes vulnerables.    │
│                                                              │
│  Suppressions: 0 (no hizo falta suprimir nada — el único          │
│    hallazgo Alto se corrigió en el momento, no se documentó como   │
│    riesgo aceptado)                                                 │
│                                                              │
│  ────────────────────────────────────────────────────────────│
│  Total: 16 categorías limpias, 1 hallazgo High (corregido)        │
│  Report: docs/daw/security/sast-FEAT-001a.md                      │
│  Next: gates.sast = true → transición CODE → VERIFY                │
└─────────────────────────────────────────────────────────────┘
```

## Nota — hallazgo de origen (interceptor) ya resuelto antes de este scan

Durante el cierre del Bloque 8, `daw-arch-auditor` detectó que `auth.interceptor.ts` filtraba el
destino de las requests con `req.url.startsWith(apiBaseUrl)` (vulnerable a spoofing de prefijo de
string / userinfo, ej. `https://localhost:7126@evil.com`). Se corrigió antes de este scan de SAST
reemplazándolo por una comparación real de `origin` vía `new URL()`, con `try/catch` fail-closed.
Se documenta acá para que quede en el registro de seguridad del ticket, no porque este scan lo haya
encontrado — SAST no re-detectó el problema porque el código ya estaba corregido al momento de
correr.

## Verificación post-fix

- `dotnet test Paretto.sln` (con `ConnectionStrings__DefaultConnection` real): 26/26 ✅
- `npx ng test --watch=false`: 20/20 ✅
- `npx ng lint`: sin hallazgos ✅
- `dotnet build Paretto.sln`: 0 advertencias, 0 errores ✅
