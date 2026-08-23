# AGENTS.md — project context

---

## Language

**Always respond in the language the user writes in.** Write every artifact you produce — PRDs,
specs, ADRs, reports, commit messages, status lines — in that same language, regardless of the
language these instructions are written in.

> Working language: `Spanish`

---

## What this project is


**Reference PRD:** `docs/daw/prd/PRD.md`

---

## Stack


| Field | Value |
|-------|-------|
| Language | TypeScript 5.x (frontend) · C# 13 (backend, .NET Core 10) |
| Runtime | Node 20+ (frontend) · .NET 10 (backend) |
| Framework | Angular 21 (frontend, HTTP client auto-generated from backend OpenAPI via NSwag) · .NET Core 10 + CQRS/MediatR 12.5.0 (backend) |
| Database | SQL Server 2025 (localhost) + EF Core |
| Test runner | Vitest (frontend) · xUnit (backend) |
| Linter / formatter | ESLint + Prettier |
| Typecheck | `npx tsc --build --noEmit tsconfig.json` (frontend) — `tsconfig.json` uses project references (`"files": []`); `-p tsconfig.json` without `--build` checks nothing and always exits 0 |
| Package manager | npm (frontend) · NuGet (backend) |

---

## Architecture conventions


### Backend (.NET Core 10 · CQRS · MediatR)

- **Folder structure:** organized by feature (vertical slice), not by technical type:
  `src/Features/<Feature>/Commands/<Action>Command.cs`, `.../Queries/<Query>Query.cs`,
  `.../Validators/`, `.../Mappings/` (Mapster). Each Command/Query lives alongside its
  Handler, its Validator (FluentValidation), and its Response DTO in the same file or folder.
  `src/Domain/` for entities and pure business logic, with no dependency on EF Core or MediatR.
  `src/Infrastructure/` for EF Core (DbContext, migrations, repositories), Azure Storage, NsfwSpy, Azure AI Foundry.
- **Layer separation:** Controllers only dispatch to `IMediator` (`Send`), never contain business logic.
  Handlers are the single point that orchestrates domain + infrastructure. The domain never depends on EF Core
  directly (accessed via injected repository interfaces/`DbContext`). Input validation lives in FluentValidation
  (MediatR pipeline behavior), not inside the Handler.
- **Error handling:** business errors as `Result<T>` or domain-specific typed exceptions
  (never a generic `Exception`). An `IPipelineBehavior<TRequest, TResponse>` centralizes logging and translates
  exceptions into HTTP responses (`ProblemDetails`). Never an empty catch, or one that only logs and moves on.
- **Naming:** Commands end in `Command` (`CreateMuralCommand`), Queries in `Query` (`GetMuralByIdQuery`),
  Handlers in `Handler` (`CreateMuralCommandHandler`). Classes and methods in PascalCase, parameters and local
  variables in camelCase, interfaces prefixed with `I` (`IMuralRepository`). Files named the same as their public class.
- **Dependencies:** no new NuGet packages without justification in the spec (performance, security, or
  functionality not covered by the current stack: MediatR, FluentValidation, Mapster, EF Core).

### Frontend (Angular 21 · signals · ng-zorro)

- **Folder structure:** by feature: `src/app/features/<feature>/` with `ui/` (standalone components),
  `data/` (services that call the API, DTOs/interfaces), and optionally `state/` if the feature uses signals
  shared across components. `src/app/shared/` only for what's genuinely reusable across features (pipes,
  generic components, directives). `src/app/core/api-client/` holds the NSwag-generated HTTP client
  (**auto-generated, never edited by hand**; regenerate from the backend's OpenAPI spec whenever the
  contract changes). Feature `data/` services wrap calls to the generated client — they don't call it
  directly from components.
- **Layer separation:** components (`ui/`) never call `HttpClient` or the generated API client directly;
  they always go through a feature service in `data/`, which wraps the NSwag client. State is managed with
  signals (`signal`, `computed`, `effect`); avoid business logic inside the template or the component when
  it can live in the service instead.
- **Error handling:** services return typed errors (`ApiError` interfaces or similar) caught via a central
  `HttpInterceptor`; never a `catchError` that swallows the error without propagating or surfacing it to the UI.
- **Naming:** files in kebab-case (`mural-map.component.ts`), components/classes in PascalCase (`MuralMapComponent`),
  signals and inputs in camelCase. Component selectors prefixed with the project prefix (`app-mural-map`).
- **Templates:** every component uses `templateUrl` pointing to a separate `.component.html` file —
  never an inline `template:` string, regardless of how small the template is.
- **Dependencies:** no new libraries without justifying in the spec why ng-zorro or the current stack
  (Angular, Leaflet) doesn't already cover it. NSwag is part of the base stack (auto-generates the HTTP
  client from the backend's OpenAPI spec) and doesn't need to be justified case by case — regenerate the
  client whenever the backend contract changes, don't hand-write API calls.

---

## Code conventions

- All code (variable names, classes, methods, etc.) and API endpoints must be written in English, regardless of the documentation's language.

---

## What NOT to do in this project

- Do not expose `pending` murals in search results or on the map (RF-013).
- Do not skip NSFW validation before publishing (RF-015).
- Do not accept images > 10 MB on any endpoint (RNF-003).
- Do not add features outside the PRD; comments, likes, and profiles are excluded from the MVP.
- Do not hand-write or commit TypeScript classes/interfaces that represent API contracts, and do not edit `api-client.generated.ts`

---

## Domain glossary


---

> ℹ️ **What does NOT belong in this file, because DAW provides it:** the order work happens in, when
> the spec gets written, when tests run, when to commit, what it takes to move between phases. All
> of that lives in `.daw/` and applies on its own.

<!-- BEGIN DAW (managed by DAW — do not edit by hand) -->
# DAW — Dilux Agentic Workflow

This repo uses **DAW**: an agent-driven development pipeline with the phases
`CLASSIFY → DEFINE → PLAN → CODE → VERIFY → RELEASE`.

Before answering, read `.daw/orchestrator.md` and run its Boot Sequence. It is a strict state
machine: it decides what you are allowed to do based on the phase recorded in `.daw-state.json`.

The project's own context — stack, architecture, domain — is elsewhere in this file. It lives here,
in `AGENTS.md`, and not in any one tool's file, on purpose: it is tool-agnostic and comes along
unchanged when the pipeline is ported to another agent.
<!-- END DAW -->
