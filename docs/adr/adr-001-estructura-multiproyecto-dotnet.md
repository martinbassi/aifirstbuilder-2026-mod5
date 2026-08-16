# ADR-001: Estructura multi-proyecto .NET por capas (Domain/Infrastructure/Api)

| Field | Value |
|-------|-------|
| Date | 2026-08-15 |
| Ticket | FEAT-001a |
| Status | Accepted |

## Context

FEAT-001a es el primer ticket de código del proyecto: no existe todavía ningún workspace de backend.
`AGENTS.md` describe el árbol conceptual del backend como carpetas — `src/Features/`, `src/Domain/`,
`src/Infrastructure/` — sin especificar si viven dentro de un único proyecto `.csproj` o en proyectos
`.csproj` separados y referenciados entre sí. Como es la primera vez que se materializa esa
estructura, la decisión que se tome acá condiciona todos los tickets de backend posteriores.

## Options considered

### Option 1: Un único proyecto Web API, carpetas por capa
- **Pros:** menos overhead de build y de referencias entre proyectos; onboarding más simple para un
  MVP de un solo ticket fundacional.
- **Cons:** "el dominio nunca depende de EF Core directamente" (regla de `AGENTS.md`) queda librado a
  disciplina de equipo — nada impide técnicamente que alguien agregue un `using
  Microsoft.EntityFrameworkCore;` dentro de `src/Domain/` por error, y el compilador no lo va a
  marcar.

### Option 2: Proyectos `.csproj` separados por capa (Domain / Infrastructure / Api)
- **Pros:** la regla "Domain no referencia EF Core" pasa a estar impuesta por el compilador —
  `Paretto.Domain.csproj` no tiene ni puede tener una referencia a `Microsoft.EntityFrameworkCore` sin
  que el build falle. La dirección de dependencias (Api → Infrastructure → Domain) es explícita y
  verificable, no solo documentada.
- **Cons:** más ceremonia de entrada (tres `.csproj`, referencias de proyecto a mantener); cualquier
  reorganización de capas requiere tocar referencias de proyecto, no solo mover carpetas.

## Decision

Se elige la **Option 2**: tres proyectos referenciados entre sí.

- `Paretto.Domain.csproj` (class library) — Entities/, Enums/. Sin dependencias de infraestructura.
- `Paretto.Infrastructure.csproj` (class library) — referencia `Paretto.Domain`; EF Core (`AppDbContext`,
  migraciones), `Security/` (hashing, tokens de sesión), `Auth/` (esquema de autenticación custom).
- `Paretto.Api.csproj` (ASP.NET Core Web API) — referencia `Paretto.Domain` y `Paretto.Infrastructure`;
  contiene `Features/<Feature>/` (Commands, Handlers, Validators, Mappings vía MediatR/FluentValidation/
  Mapster) y `Api/Controllers/`. `AGENTS.md` no exige un proyecto `Application` separado, así que
  `Features/` vive dentro de `Paretto.Api` en vez de en un cuarto proyecto.

Razón concreta: es el primer ticket de código del repo, y esta es exactamente la clase de decisión
que conviene que el compilador imponga en vez de que quede como una convención a vigilar en cada
revisión de arquitectura futura.

## Consequences

- Todo ticket de backend posterior (FEAT-001b, c, d y los que sigan) sigue esta estructura de 3
  proyectos; nuevas features agregan carpetas dentro de `Paretto.Api/Features/`, no nuevos proyectos.
- Mover código entre capas (p. ej. si algo de `Infrastructure` debiera bajar a `Domain`) requiere
  actualizar la referencia de proyecto correspondiente, no solo mover un archivo.
- `Paretto.Domain.csproj` queda como el punto de verificación mecánico de la regla "el dominio nunca
  depende de EF Core directamente" de `AGENTS.md` — un build roto ahí es la señal, no una revisión
  manual.
- **Relajación puntual documentada (FEAT-001a Block 6):** `Paretto.Infrastructure.csproj` agrega
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />` para poder implementar
  `SessionAuthenticationHandler : AuthenticationHandler<TOptions>`. El paquete NuGet original que
  cubría esa superficie (`Microsoft.AspNetCore.Authentication.Abstractions`) está descontinuado
  desde ASP.NET Core 3.0 (movido al shared framework), así que un `FrameworkReference` es la única
  vía soportada por el SDK para una class library. Esto expone toda la superficie de
  `Microsoft.AspNetCore.App` a `Paretto.Infrastructure` — no solo el subconjunto de Authentication
  que necesita — aflojando el límite mecánico que esta ADR busca imponer entre capas. Se acepta
  como costo necesario del diseño actual (no hay forma de referenciar un subconjunto más chico) y
  queda anotado acá para que no se re-audite como sorpresa en tickets futuros de backend.
  Efecto colateral relacionado: al quedar `Microsoft.AspNetCore.App` referenciado, el SDK detecta
  que `Microsoft.Extensions.Identity.Core` (agregado en Block 4 para `PasswordHasher<TUser>`) se
  superpone con la superficie de Identity ya incluida en el shared framework — se verificó que
  `PasswordHasher<TUser>` sigue disponible transitivamente sin el paquete explícito, así que el
  `PackageReference` se eliminó en Block 6 (sin cambios de comportamiento, tests de
  `PasswordHasherTests` en verde).
