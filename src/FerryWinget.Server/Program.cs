using FerryWinget.Core.Configuration;
using FerryWinget.Core.Storage;
using FerryWinget.Server.Services;
using Microsoft.AspNetCore.ResponseCompression;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Load configuration — skip if already registered (e.g., in tests)
if (!builder.Services.Any(d => d.ServiceType == typeof(FerryConfig)))
{
    var configPath = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "config.yaml";
    FerryConfig config;
    if (File.Exists(configPath))
    {
        config = ConfigLoader.Load(configPath);
    }
    else
    {
        // Fallback for test environments
        config = new FerryConfig();
    }
    builder.Services.AddSingleton(config);
    builder.Services.AddSingleton(config.Server);

    var store = new FileSystemPackageStore(
        config.Storage.RootPath, config.Storage.PackagesDir);
    builder.Services.AddSingleton<IPackageStore>(store);
}

// Package index
builder.Services.AddSingleton<PackageIndexService>();
builder.Services.AddSingleton<SearchService>();

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Response compression
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});

// Response caching
builder.Services.AddResponseCaching();

// OpenTelemetry
var otelResource = ResourceBuilder.CreateDefault()
    .AddService("FerryWinget.Server", serviceVersion: "1.0.0");

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(otelResource)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(otelResource)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(otelResource);
    logging.AddOtlpExporter();
});

// Static files for Web UI
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Initialize package index
var indexService = app.Services.GetRequiredService<PackageIndexService>();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("Building package index...");
await indexService.RebuildIndexAsync();
logger.LogInformation("Package index built: {Count} packages", indexService.Index.Count);

app.UseResponseCompression();
app.UseResponseCaching();
app.UseStaticFiles();
app.MapControllers();

// Serve index.html at root
app.MapFallbackToFile("index.html");

var serverConfig = app.Services.GetRequiredService<ServerConfig>();
if (serverConfig.Port > 0)
    app.Urls.Add($"http://+:{serverConfig.Port}");

app.Run();
