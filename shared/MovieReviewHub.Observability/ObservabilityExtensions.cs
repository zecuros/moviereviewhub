using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MovieReviewHub.Observability;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddMovieReviewHubObservability(
        this WebApplicationBuilder builder, string serviceName)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        builder.Logging.Configure(options => options.ActivityTrackingOptions =
            ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(MessagingTrace.SourceName)
                .AddOtlpExporter("traces", options => options.Endpoint = new Uri(
                    builder.Configuration["Observability:TracesEndpoint"] ?? "http://localhost:4317")))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter("metrics", options => options.Endpoint = new Uri(
                    builder.Configuration["Observability:MetricsEndpoint"] ?? "http://localhost:4319")));
        return builder;
    }

    public static WebApplication UseMovieReviewHubObservability(
        this WebApplication app, string serviceName)
    {
        // Log route paths, never request bodies, authorization headers or query strings.
        app.Use(async (context, next) =>
        {
            using var scope = app.Logger.BeginScope(new Dictionary<string, object>
            {
                ["Service"] = serviceName
            });
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next(context);
                app.Logger.LogInformation("HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "HTTP {Method} {Path} failed in {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                throw;
            }
        });
        app.MapGet("/health", () => Results.Ok(new { service = serviceName, status = "ok" }));
        return app;
    }
}
