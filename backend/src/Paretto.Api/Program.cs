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
using Paretto.Infrastructure.Security;

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
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> in the test project can bootstrap this host.
public partial class Program { }
