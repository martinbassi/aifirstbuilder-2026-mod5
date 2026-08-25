using System.Globalization;
using System.Threading.RateLimiting;
using FluentValidation;
using MapsterMapper;
using Mapster;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Paretto.Api.Common.Behaviors;
using Paretto.Api.Common.Middleware;
using Paretto.Infrastructure.Auth;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Moderation;
using Paretto.Infrastructure.Security;
using Paretto.Infrastructure.Storage;
using Paretto.Infrastructure.Common;

// ADR-003 (supersedes ADR-002): fija la cultura por defecto del proceso a Invariant en vez de
// deshabilitar la globalización entera (InvariantGlobalization=true). Ese flag resultó incompatible
// con Microsoft.Data.SqlClient, que lanza NotSupportedException al abrir la conexión a SQL Server
// bajo modo invariant — se descubrió en el closeout de CODE de FEAT-001b, corriendo la suite
// completa contra una instancia real de SQL Server. Esto cubre el mismo caso que motivó ADR-002
// (parseo de `double`/`decimal`/`DateTime` en cualquier endpoint, sin depender de LANG/LC_ALL del
// SO) sin deshabilitar ICU: solo cambia la cultura por defecto de los threads nuevos, incluidos los
// que ASP.NET Core usa para atender requests. Debe ejecutarse ANTES de `WebApplication.CreateBuilder`
// para cubrir toda inicialización posterior, y como está en el código de nivel superior de `Program`,
// también aplica dentro de `WebApplicationFactory<Program>` (los tests hostean este mismo Program).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    // Order: Logging outermost, Validation innermost (still ahead of the Handler). LoggingBehavior
    // wraps everything in a try/catch that logs any exception with its elapsed time before
    // rethrowing (see LoggingBehavior) — keeping it outermost means a rejected (invalid) request
    // still gets that same observability, instead of failing validation silently before logging
    // ever runs. Round 2 correction of Block 5: ValidationBehavior replaces the manual
    // `IValidator<T>.ValidateAsync` call AuthController used to make itself.
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

var mapsterConfig = TypeAdapterConfig.GlobalSettings;
mapsterConfig.Scan(typeof(Program).Assembly);
builder.Services.AddSingleton(mapsterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Block 4 created IPasswordHasher/PasswordHasher but did not register them in DI (its own tests
// instantiate PasswordHasher directly, see PasswordHasherTests.cs) — Block 5 is the first consumer
// via the MediatR pipeline, which needs the container to resolve it, so the registration is added
// here. Minimal, additive one-liner; nothing else in this file's existing wiring changes.
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// Block 6's first consumer of ISessionTokenGenerator via the MediatR pipeline (LoginCommandHandler)
// needs the container to resolve it — Block 4 created the service but did not register it, exactly
// the same situation Block 5 documented above for IPasswordHasher.
builder.Services.AddScoped<ISessionTokenGenerator, SessionTokenGenerator>();

// Block 2 (FEAT-001b) creates IBlobStorageService/AzureBlobStorageService but has no consumer yet —
// its first real consumer is CreateMuralCommandHandler in Block 4 (FEAT-001b), not implemented at
// the time this registration is added. Same situation already documented above for
// IPasswordHasher/ISessionTokenGenerator: register now so the container can resolve it once that
// Handler exists, rather than leaving a registration gap for that later block to remember.
builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();

// Block 3 (FEAT-001b): NsfwSpyContentScanner needs its own INsfwClassifier abstraction over the
// underlying NsfwSpy model — no per-request state in either type (NsfwSpy caches its ML.NET model
// in a static field internally), so Scoped here mirrors the same lifetime already used above for
// IBlobStorageService/IPasswordHasher, not a hard requirement of either type. First real consumer
// is CreateMuralCommandHandler in Block 4 (FEAT-001b), not implemented at the time this
// registration is added — same situation already documented above for IBlobStorageService.
builder.Services.AddScoped<INsfwClassifier, NsfwSpyClassifier>();
builder.Services.AddScoped<INsfwContentScanner, NsfwSpyContentScanner>();

// Block 7 (LogoutCommandHandler) needs IHttpContextAccessor to read the raw token off the current
// request's Authorization header. Contrary to a common misconception, ASP.NET Core does NOT
// register IHttpContextAccessor by default just from AddControllers() — it is opt-in and must be
// added explicitly.
builder.Services.AddHttpContextAccessor();

// Block 6: real session-based authentication scheme (opaque token -> Sessions row lookup, not JWT
// — PLAN decision, see spec Block 6 and docs/daw/security/threat-FEAT-001a.md), replacing Block 1's
// placeholder AddAuthentication()/AddAuthorization().
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<SessionAuthenticationSchemeOptions, SessionAuthenticationHandler>(
        SessionAuthenticationHandler.SchemeName, options => { });
builder.Services.AddAuthorization();

// Mitigation R3 (threat model): basic rate limiting so /login and /register are not left
// unlimited while a dedicated throttling ticket does not exist yet. Endpoint-specific policies
// are applied where those endpoints are defined (Block 5/Block 6); this registers the global
// rejection behavior.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Block 3 (FEAT-001d), mitigation R3 of the discovery threat model
    // (docs/daw/security/threat-FEAT-001d.md): a stricter, endpoint-specific policy for the public,
    // unauthenticated `GET /api/discovery/nearby-murals` — 20 req/min per IP, on top of the
    // GlobalLimiter above (100 req/min). Both apply on the same endpoint; the stricter one (20) is
    // the one that limits in practice.
    options.AddPolicy("discovery", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// FIX-001: el frontend (ng serve, http://localhost:4200) y la API (dotnet run,
// https://localhost:7126) son orígenes distintos en desarrollo local — sin CORS el navegador
// bloquea toda request cross-origin. Exclusiva de desarrollo local: producción, cuando exista,
// necesita su propia policy con el dominio real; no reutilizar "DevelopmentCors" (mitigación R2,
// docs/daw/security/threat-FIX-001.md). El bloque completo vive dentro de IsDevelopment() para que
// Production nunca lo registre; el `?? Array.Empty<string>()` es una segunda defensa en
// profundidad (mitigación R1) por si el bloque se moviera fuera del gate en el futuro.
if (builder.Environment.IsDevelopment())
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentCors", policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader());
    });
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonDateTimeUtcConverter());
});


var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseMiddleware<ExceptionHandlingMiddleware>();

// SAST finding (CODE closeout, FEAT-001a): Swagger/OpenAPI UI must not be reachable outside
// Development — exposing the API surface (routes, DTOs, auth requirements) to an unauthenticated
// caller in Production is a security misconfiguration (OWASP A05:2021), independent of whether any
// endpoint documented there has its own vulnerability.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevelopmentCors");
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> in the test project can bootstrap this host.
public partial class Program { }
