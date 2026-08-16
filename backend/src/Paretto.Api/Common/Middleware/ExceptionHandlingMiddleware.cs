using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Paretto.Api.Common.Exceptions;

namespace Paretto.Api.Common.Middleware;

/// <summary>
/// Translates exceptions into a ProblemDetails response, instead of letting them leak as a raw
/// error or crash the pipeline. In Development, the 500 fallback's exception detail is included to
/// help debugging; in other environments, it is omitted so internal details are never exposed.
///
/// Round 2 correction of Block 5: this middleware now also translates the two exception kinds that
/// AuthController used to catch by hand —
/// - `FluentValidation.ValidationException` (thrown by `ValidationBehavior`, Common/Behaviors) →
///   `422` with the same `ValidationProblemDetails` shape (errors grouped by property name) the
///   controller used to build itself.
/// - Any `AppException` (e.g. `DuplicateAccountException`) → its own `StatusCode`, with `Message`
///   as the `ProblemDetails.Title`. Chosen over one `catch` per concrete exception type so Block
///   6/7's future domain exceptions (invalid credentials, expired session, etc.) only need to
///   inherit `AppException` with their own status code — this middleware does not need touching
///   again for each one.
/// Anything else still falls through to the generic 500 handling below.
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
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var validationProblem = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "One or more validation errors occurred.",
                Instance = context.Request.Path,
            };

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;

            await context.Response.WriteAsJsonAsync(validationProblem);
        }
        catch (AppException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = ex.Message,
                Instance = context.Request.Path,
            };

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = ex.StatusCode;

            await context.Response.WriteAsJsonAsync(problemDetails);
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
