using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// FEAT-012 — modo LAN: <c>app.UseHttpsRedirection()</c> se saltea condicionalmente cuando la
/// configuración <c>LanMode</c> está en <c>true</c> (spec: docs/daw/specs/spec-FEAT-012.md, Block
/// 1; riesgo aceptado R1: docs/daw/security/threat-FEAT-012.md).
/// </summary>
public class LanModeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public LanModeTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private const string DiscoveryEndpoint = "/api/discovery/nearby-murals?lat=-34.6037&lng=-58.3816&radiusKm=5";

    // Sustituye AppDbContext por InMemory y IBlobStorageService por un fake, igual que
    // CorsTests.cs:31-53 — necesario para que el endpoint de solo lectura usado como sonda
    // responda sin depender de SQL Server/Azure Storage reales.
    private WebApplicationFactory<Program> CreateFactory(string dbName, bool? lanMode)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            // "https_port" (docs: https://learn.microsoft.com/aspnet/core/security/enforcing-ssl)
            // is how UseHttpsRedirection() knows which port to redirect to when the server (here,
            // TestServer) has no real Kestrel HTTPS binding to auto-detect from — without it, the
            // middleware cannot compute a redirect URL and silently no-ops regardless of LanMode,
            // making the "still redirects" regression assertion meaningless.
            var configOverrides = new Dictionary<string, string?>
            {
                ["https_port"] = "7126",
            };
            if (lanMode.HasValue)
            {
                configOverrides["LanMode"] = lanMode.Value.ToString();
            }
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(configOverrides);
            });

            builder.ConfigureServices(services =>
            {
                var appDbContextDescriptors = services
                    .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext)))
                    .ToList();
                foreach (var descriptor in appDbContextDescriptors)
                {
                    services.Remove(descriptor);
                }
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));

                services.RemoveAll<IBlobStorageService>();
                services.AddScoped<IBlobStorageService>(_ => new FakeBlobStorageService());
            });
        });
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken ct) =>
            throw new NotSupportedException("Not needed for LanMode tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    [Fact]
    public async Task With_LanMode_true_a_plain_HTTP_request_is_not_redirected_to_HTTPS()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), lanMode: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

        var response = await client.GetAsync(DiscoveryEndpoint);

        Assert.NotEqual(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    [Fact]
    public async Task Without_LanMode_the_default_behavior_still_redirects_to_HTTPS()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString(), lanMode: null);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });

        var response = await client.GetAsync(DiscoveryEndpoint);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }
}
