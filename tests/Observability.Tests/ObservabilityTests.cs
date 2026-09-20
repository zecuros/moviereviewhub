using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using MovieReviewHub.Observability;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Observability.Tests;

[Collection("Telemetry listeners")]
public class ObservabilityTests
{
    [Theory]
    [InlineData("AuthService")]
    [InlineData("MovieService")]
    [InlineData("ReviewService")]
    [InlineData("WatchlistService")]
    [InlineData("NotificationService")]
    [InlineData("ApiGateway")]
    public async Task HealthAndRequestLogging_PreserveServiceIdentityAndOmitSecrets(string service)
    {
        var logs = new CapturingLoggerProvider();
        var builder = CreateBuilder(service);
        builder.Logging.AddProvider(logs);
        await using var app = builder.Build();
        app.UseMovieReviewHubObservability(service);
        app.MapPost("/accepted", () => Results.StatusCode(202));
        await app.StartAsync();
        using var client = app.GetTestClient();
        var health = await client.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal(service, health.GetProperty("service").GetString());
        Assert.Equal("ok", health.GetProperty("status").GetString());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/accepted?token=query-secret");
        request.Headers.Add("Authorization", "Bearer header-secret");
        request.Content = new StringContent("body-secret");
        using var response = await client.SendAsync(request);
        Assert.Equal(202, (int)response.StatusCode);
        var entry = Assert.Single(logs.Entries.Where(e => e.Message.Contains("POST /accepted returned")));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("POST", entry.Properties["Method"]);
        Assert.Equal("/accepted", entry.Properties["Path"]?.ToString());
        Assert.Equal(202, entry.Properties["StatusCode"]);
        Assert.True((double)entry.Properties["ElapsedMs"]! >= 0);
        Assert.Contains(entry.Scopes, scope => scope.Contains($"[Service, {service}]"));
        Assert.DoesNotContain("secret", entry.Message);
        await app.StopAsync();
    }

    [Fact]
    public async Task FailedRequest_IsLoggedAndExceptionIsRethrown()
    {
        var logs = new CapturingLoggerProvider();
        var builder = CreateBuilder("FailureTest");
        builder.Logging.AddProvider(logs);
        await using var app = builder.Build();
        app.UseMovieReviewHubObservability("FailureTest");
        var failure = new InvalidOperationException("expected failure");
        app.MapGet("/failure", (HttpContext _) => Task.FromException(failure));
        await app.StartAsync();
        using var client = app.GetTestClient();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/failure"));
        Assert.Same(failure, actual);
        var entry = Assert.Single(logs.Entries.Where(e => e.Message.Contains("GET /failure failed")));
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        await app.StopAsync();
    }

    [Theory]
    [InlineData(null, null, "http://localhost:4317/", "http://localhost:4319/")]
    [InlineData("http://jaeger:4317", "http://otel-collector:4317", "http://jaeger:4317/", "http://otel-collector:4317/")]
    public async Task Telemetry_UsesConfiguredEndpointsResourceAndStructuredLogging(
        string? traces, string? metrics, string expectedTraces, string expectedMetrics)
    {
        var builder = CreateBuilder("ConfigurationTest", traces, metrics);
        await using var app = builder.Build();
        // Resolving the providers executes both registration pipelines, including instrumentation.
        var tracer = app.Services.GetRequiredService<TracerProvider>();
        var meter = app.Services.GetRequiredService<MeterProvider>();
        Assert.Contains(tracer.GetResource().Attributes, p => p.Key == "service.name" && (string)p.Value == "ConfigurationTest");
        Assert.Contains(meter.GetResource().Attributes, p => p.Key == "service.name" && (string)p.Value == "ConfigurationTest");
        var exporter = app.Services.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>();
        Assert.Equal(expectedTraces, exporter.Get("traces").Endpoint.AbsoluteUri);
        Assert.Equal(expectedMetrics, exporter.Get("metrics").Endpoint.AbsoluteUri);
        var console = app.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue;
        Assert.True(console.IncludeScopes);
        Assert.True(console.UseUtcTimestamp);
        Assert.Equal("yyyy-MM-ddTHH:mm:ss.fffZ", console.TimestampFormat);
        var logging = app.Services.GetRequiredService<IOptions<LoggerFactoryOptions>>().Value;
        Assert.Equal(ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId, logging.ActivityTrackingOptions);
    }

    [Fact]
    public async Task IncomingHttpTrace_IsParentOfMessagingSpan_AndHttpMetricsAreRecorded()
    {
        var spans = new ActivityExporter();
        var metrics = new MetricExporter();
        var builder = CreateBuilder("TraceTest");
        builder.Services.AddOpenTelemetry()
            .WithTracing(t => t.AddProcessor(new SimpleActivityExportProcessor(spans)))
            .WithMetrics(m => m.AddReader(new PeriodicExportingMetricReader(metrics, 60000)));
        await using var app = builder.Build();
        app.UseMovieReviewHubObservability("TraceTest");
        app.MapGet("/publish", () =>
        {
            using var activity = MessagingTrace.StartPublish();
            return Results.Ok();
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        var traceId = ActivityTraceId.CreateRandom().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/publish");
        request.Headers.Add("traceparent", $"00-{traceId}-0123456789abcdef-01");
        (await client.SendAsync(request)).EnsureSuccessStatusCode();
        var producer = Assert.Single(spans.Spans.Where(a => a.Kind == ActivityKind.Producer));
        var server = Assert.Single(spans.Spans.Where(a => a.Kind == ActivityKind.Server));
        Assert.Equal(traceId, server.TraceId.ToString());
        Assert.Equal(server.SpanId, producer.ParentSpanId);
        Assert.Equal(server.TraceId, producer.TraceId);
        app.Services.GetRequiredService<MeterProvider>().ForceFlush();
        Assert.Contains("http.server.request.duration", metrics.Names);
        await app.StopAsync();
    }

    private static WebApplicationBuilder CreateBuilder(string service, string? traces = null, string? metrics = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration["Observability:TracesEndpoint"] = traces;
        builder.Configuration["Observability:MetricsEndpoint"] = metrics;
        builder.AddMovieReviewHubObservability(service);
        // Real OTLP exporters remain registered; keep unavailable external endpoints from delaying unit tests.
        builder.Services.PostConfigureAll<OtlpExporterOptions>(options => options.TimeoutMilliseconds = 100);
        return builder;
    }

    private sealed class ActivityExporter : BaseExporter<Activity>
    {
        public ConcurrentBag<Activity> Spans { get; } = new();
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch) Spans.Add(activity);
            return ExportResult.Success;
        }
    }

    private sealed class MetricExporter : BaseExporter<Metric>
    {
        public ConcurrentBag<string> Names { get; } = new();
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch) Names.Add(metric.Name);
            return ExportResult.Success;
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
        public ConcurrentBag<Entry> Entries { get; } = new();
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);
        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                owner._scopes.ForEachScope((scope, result) =>
                    result.Add(scope is IEnumerable<KeyValuePair<string, object>> pairs
                        ? string.Join(",", pairs) : scope?.ToString() ?? ""), scopes);
                var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.ToDictionary(p => p.Key, p => p.Value) : new Dictionary<string, object?>();
                owner.Entries.Add(new Entry(logLevel, formatter(state, exception), exception, properties, scopes));
            }
        }
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception,
        Dictionary<string, object?> Properties, List<string> Scopes);
}
