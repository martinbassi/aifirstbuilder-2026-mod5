using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Paretto.Api.Common.Middleware;

/// <summary>
/// Translates unhandled exceptions into a ProblemDetails response (500), instead of letting them
/// leak as a raw error or crash the pipeline. In Development, the exception detail is included to
/// help debugging; in other environments, it is omitted so internal details are never exposed.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = _environment.IsDevelopment() ? ex.ToString() : null,
                Instance = context.Request.Path,
            };

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
