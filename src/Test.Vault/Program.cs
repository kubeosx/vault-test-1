using Test.Vault;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

var builder = WebApplication.CreateBuilder(args);

// Note: Switch between Zipkin/Jaeger/OTLP/Console by setting UseTracingExporter in appsettings.json.
var tracingExporter = builder.Configuration.GetValue<string>("UseTracingExporter").ToLowerInvariant();

// Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
var metricsExporter = builder.Configuration.GetValue<string>("UseMetricsExporter").ToLowerInvariant();

// Note: Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
var logExporter = builder.Configuration.GetValue<string>("UseLogExporter").ToLowerInvariant();

// Build a resource configuration action to set service information.
Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: builder.Configuration.GetValue<string>("ServiceName"),
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);

// Create a service to expose ActivitySource, and Metric Instruments
// for manual instrumentation
builder.Services.AddSingleton<Instrumentation>();

// Configure OpenTelemetry tracing & metrics with auto-start using the
// AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
builder.Services.AddOpenTelemetry()
        .ConfigureResource(configureResource)
        .WithTracing(otbuilder =>
        {
            // Ensure the TracerProvider subscribes to any custom ActivitySources.
            otbuilder
                .AddSource(Instrumentation.ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();


            // Use IConfiguration binding for AspNetCore instrumentation options.
            builder.Services.Configure<AspNetCoreInstrumentationOptions>(builder.Configuration.GetSection("AspNetCoreInstrumentation"));

            switch (tracingExporter)
            {
                case "zipkin":
                    otbuilder.AddZipkinExporter();

                    otbuilder.ConfigureServices(services =>
                    {
                        // Use IConfiguration binding for Zipkin exporter options.
                        services.Configure<ZipkinExporterOptions>(builder.Configuration.GetSection("Zipkin"));
                    });
                    break;

                case "otlp":
                    otbuilder.AddOtlpExporter(otlpOptions =>
                    {
                        // Use IConfiguration directly for Otlp exporter endpoint option.
                        otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint"));
                    });
                    break;

                default:
                    otbuilder.AddConsoleExporter();
                    break;
            }

        })
        .WithMetrics(otbuilder =>
        {
            // Metrics
            var resource = ResourceBuilder.CreateDefault().AddService("dot8", "dot8");

            // Ensure the MeterProvider subscribes to any custom Meters.
            otbuilder
                .AddMeter(Instrumentation.MeterName)
                .SetResourceBuilder(resource)
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();

            switch (metricsExporter)
            {
                case "prometheus":
                   otbuilder.AddPrometheusExporter(options =>
                   {
                       options.ScrapeEndpointPath = "metrics";
                       // Use your endpoint and port here
                       //options.HttpListenerPrefixes = new string[] { $"http://localhost:{9090}/" };
                       options.ScrapeResponseCacheDurationMilliseconds = 0;
                   });
                   break;
                case "otlp":
                    otbuilder.AddOtlpExporter(otlpOptions =>
                    {
                        // Use IConfiguration directly for Otlp exporter endpoint option.
                        otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue<string>("Otlp:Endpoint"));
                    });
                    break;
                default:
                    otbuilder.AddConsoleExporter();
                    break;
            }
        });


// Required f
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = Int32.MaxValue; // if don't set default value is: 30 MB
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MemoryBufferThreshold = Int32.MaxValue;
    options.MultipartBoundaryLengthLimit = Int32.MaxValue;
    options.MultipartBodyLengthLimit = Int32.MaxValue;
    options.MultipartHeadersLengthLimit = Int32.MaxValue;
});


// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Test.Vault API",
        Description = "An ASP.NET Core Web API: Test.Vault",
        TermsOfService = new Uri("https://github.com/kubeosx/vault-test-1"),
        Contact = new OpenApiContact
        {
            Name = "Contact Info",
            Url = new Uri("https://github.com/kubeosx/vault-test-1")
        },
        License = new OpenApiLicense
        {
            Name = "License",
            Url = new Uri("https://github.com/kubeosx/vault-test-1")
        }
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseHealthChecks("/health");
app.UseHealthChecks("/healthz");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

