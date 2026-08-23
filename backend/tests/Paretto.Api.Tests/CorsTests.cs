using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Paretto.Infrastructure.Data;
using Paretto.Infrastructure.Storage;

namespace Paretto.Api.Tests;

/// <summary>
/// FIX-001 — CORS para desarrollo local. Program.cs registra una policy "DevelopmentCors" gateada
/// por <c>IsDevelopment()</c> (RCA: docs/daw/specs/rca-FIX-001.md; diseño:
/// docs/daw/specs/fix-FIX-001.md).
/// </summary>
public class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public CorsTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private const string AllowedOrigin = "http://localhost:4200";
    private const string DiscoveryEndpoint = "/api/discovery/nearby-murals?lat=-34.6037&lng=-58.3816&radiusKm=5";

    // Sustituye AppDbContext por InMemory y IBlobStorageService por un fake, igual que
    // DiscoveryControllerTests — necesario para que el endpoint de solo lectura usado como sonda
    // responda sin depender de SQL Server/Azure Storage reales, en cualquier entorno.
    private WebApplicationFactory<Program> CreateFactory(string environment, string dbName)
    {
        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
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
            throw new NotSupportedException("Not needed for CORS tests.");

        public string GenerateReadSasUrl(string blobName, TimeSpan validity) => $"https://example.test/{blobName}?sas=fake";
    }

    [Fact]
    public async Task Request_with_Origin_localhost_4200_in_Development_receives_Access_Control_Allow_Origin_header()
    {
        var factory = CreateFactory("Development", Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryEndpoint);
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values),
            "Expected Access-Control-Allow-Origin header to be present.");
        Assert.Equal(AllowedOrigin, values!.Single());
    }

    [Fact]
    public async Task Request_with_a_different_Origin_does_not_receive_Access_Control_Allow_Origin_header()
    {
        var factory = CreateFactory("Development", Guid.NewGuid().ToString());
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryEndpoint);
        request.Headers.Add("Origin", "http://evil.example.com");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"),
            "Did not expect Access-Control-Allow-Origin for an origin outside the allow-list.");
    }

    [Fact]
    public async Task AddCors_is_not_registered_when_the_host_runs_outside_Development()
    {
        // Mitigación R1 (docs/daw/security/threat-FIX-001.md): appsettings.json (base/Production) no
        // declara Cors:AllowedOrigins. Si AddCors se registrara fuera del gate IsDevelopment(), un
        // WithOrigins(null) tumbaría el arranque del proceso en Production. Este test prueba que el
        // host arranca sin excepción con el environment en Production.
        var factory = CreateFactory("Production", Guid.NewGuid().ToString());

        var exception = await Record.ExceptionAsync(async () =>
        {
            var client = factory.CreateClient();
            await client.GetAsync(DiscoveryEndpoint);
        });

        Assert.Null(exception);
    }
}
