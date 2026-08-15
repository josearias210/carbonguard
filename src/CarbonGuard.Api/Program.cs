using System.Text.Json.Serialization;
using CarbonGuard.Api.Endpoints;
using CarbonGuard.Api.Infrastructure;
using CarbonGuard.Core.Configuration;
using CarbonGuard.Core.Detection;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddMetrics();
builder.Services.AddSingleton<AnalysisTelemetry>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services
    .AddOptions<AnomalyDetectionOptions>()
    .BindConfiguration(AnomalyDetectionOptions.SectionName)
    .Validate(options => options.IsValid(), "Anomaly detection settings are invalid.")
    .ValidateOnStart();

builder.Services.AddSingleton<IAnomalyDetector>(serviceProvider =>
{
    AnomalyDetectionOptions options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<AnomalyDetectionOptions>>()
        .Value;
    return new AnomalyDetector(options);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CarbonGuard Anomaly Detection API",
        Version = "v1",
        Description = "Deterministic and explainable anomaly detection for energy and CO2 records.",
    });
});

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    HttpContext httpContext = statusCodeContext.HttpContext;
    await Results.Problem(
        statusCode: httpContext.Response.StatusCode,
        title: httpContext.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status405MethodNotAllowed => "The HTTP method is not allowed for this resource.",
            StatusCodes.Status413PayloadTooLarge => "The request payload is too large.",
            _ => "The request could not be completed.",
        },
        extensions: new Dictionary<string, object?>
        {
            ["traceId"] = httpContext.TraceIdentifier,
        }).ExecuteAsync(httpContext);
});
app.Use(async (httpContext, next) =>
{
    httpContext.Response.OnStarting(() =>
    {
        httpContext.Response.Headers.TryAdd("X-Trace-Id", httpContext.TraceIdentifier);
        httpContext.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        httpContext.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
        httpContext.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        return Task.CompletedTask;
    });

    await next(httpContext);
});

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "CarbonGuard API";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CarbonGuard API v1");
    });
}

app.MapGet("/", () => Results.Ok(new
{
    service = "CarbonGuard Anomaly Detection API",
    version = "v1",
    documentation = "/swagger",
    health = "/health/live",
}))
    .ExcludeFromDescription();

app.MapHealthChecks("/health/live").ExcludeFromDescription();
app.MapAnomalyDetectionEndpoints();

app.Run();

public partial class Program;
