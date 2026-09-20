using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;

namespace Observability.Tests;

// Like the E2E suite, these tests need the Compose PostgreSQL/RabbitMQ dependencies.
// Hosting the real entry points in-process also makes startup wiring measurable by Coverlet.
[Collection("Telemetry listeners")]
public class ServiceStartupTests
{
    [Fact]
    public Task Gateway() => VerifyStartup<global::Program>("ApiGateway");

    [Fact]
    public Task Auth() => VerifyStartup<AuthService.Controllers.AuthController>("AuthService", "AuthDb");

    [Fact]
    public Task Movie() => VerifyStartup<MovieService.Controllers.MovieController>("MovieService", "MovieDb");

    [Fact]
    public Task Review() => VerifyStartup<ReviewService.Controllers.ReviewController>("ReviewService", "ReviewDb");

    [Fact]
    public Task Watchlist() => VerifyStartup<WatchlistService.Controllers.WatchlistController>("WatchlistService", "WatchlistDb");

    [Fact]
    public Task Notification() => VerifyStartup<NotificationService.Controllers.NotificationController>("NotificationService", "NotificationDb");

    private static async Task VerifyStartup<TEntryPoint>(string service, string? database = null)
        where TEntryPoint : class
    {
        await using var factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", Convert.ToHexString(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("RabbitMq:Host", "localhost");
            builder.UseSetting("RabbitMq:Username", "guest");
            builder.UseSetting("RabbitMq:Password", "guest");
            if (database is not null)
                builder.UseSetting("ConnectionStrings:DefaultConnection",
                    $"Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres");
            builder.ConfigureServices(services =>
                services.PostConfigureAll<OtlpExporterOptions>(options => options.TimeoutMilliseconds = 100));
        });
        using var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal(service, health.GetProperty("service").GetString());
        Assert.Equal("ok", health.GetProperty("status").GetString());
        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        swagger.EnsureSuccessStatusCode();
    }
}
