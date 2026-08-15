using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Paretto.Api.Tests;

/// <summary>
/// Validates F-SPEC-16: an unhandled exception thrown by an endpoint is translated to a 500
/// ProblemDetails response by ExceptionHandlingMiddleware, instead of leaking as a raw 500 or
/// crashing the pipeline. A test-only endpoint is injected via IStartupFilter so it runs
/// downstream of the production middleware pipeline (in particular, after ExceptionHandlingMiddleware),
/// without modifying Program.cs itself.
/// </summary>
public class ExceptionHandlingMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ExceptionHandlingMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter, ThrowingEndpointStartupFilter>();
            });
        });
    }

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetailsWith500()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/test/throws");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem!.Status);
    }

    private sealed class ThrowingEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Register the real Program.cs pipeline first (so ExceptionHandlingMiddleware is
            // already in place), then append a test-only endpoint downstream of it.
            next(app);

            app.Map("/test/throws", branch =>
                branch.Run(_ => throw new InvalidOperationException("Test exception for ExceptionHandlingMiddleware")));
        };
    }
}
