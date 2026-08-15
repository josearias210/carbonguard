using System.Diagnostics;
using System.Globalization;
using CarbonGuard.Api.Infrastructure;
using CarbonGuard.Core.Detection;
using CarbonGuard.Core.Models;

namespace CarbonGuard.Api.Endpoints;

public static class AnomalyDetectionEndpoints
{
    private const int MaximumBatchSize = 10_000;
    private const int MaximumBusinessEvents = 1_000;
    private const string MonthFormat = "yyyy-MM";

    public static IEndpointRouteBuilder MapAnomalyDetectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/anomaly-detection")
            .WithTags("Anomaly Detection");

        group.MapPost("/analyze", Analyze)
            .WithName("AnalyzeEmissionRecords")
            .WithSummary("Analyzes energy and CO2 records for explainable anomalies")
            .Produces<AnalysisResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static IResult Analyze(
        AnalysisRequest request,
        IAnomalyDetector detector,
        AnalysisTelemetry telemetry,
        HttpContext httpContext)
    {
        if (request.Records is null || request.Records.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["records"] = ["At least one emission record is required."],
            });
        }

        if (request.Records.Count > MaximumBatchSize)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Batch limit exceeded",
                detail: $"A single request can contain at most {MaximumBatchSize:N0} records.");
        }

        Dictionary<string, string[]> businessEventErrors = ValidateBusinessEvents(request.BusinessEvents);
        if (businessEventErrors.Count > 0)
        {
            return Results.ValidationProblem(businessEventErrors);
        }

        long startedAt = Stopwatch.GetTimestamp();
        AnalysisResponse response = detector.Analyze(request);
        telemetry.Record(response, Stopwatch.GetElapsedTime(startedAt), httpContext.TraceIdentifier);
        return Results.Ok(response);
    }

    private static Dictionary<string, string[]> ValidateBusinessEvents(IReadOnlyList<BusinessEvent>? businessEvents)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (businessEvents is null)
        {
            return errors;
        }

        if (businessEvents.Count > MaximumBusinessEvents)
        {
            errors["businessEvents"] = [$"At most {MaximumBusinessEvents:N0} business events are allowed."];
            return errors;
        }

        for (int index = 0; index < businessEvents.Count; index++)
        {
            BusinessEvent businessEvent = businessEvents[index];
            var eventErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(businessEvent.Site))
            {
                eventErrors.Add("Site is required.");
            }

            if (string.IsNullOrWhiteSpace(businessEvent.Type))
            {
                eventErrors.Add("Type is required.");
            }

            bool validFrom = TryParseMonth(businessEvent.EffectiveFrom, out DateOnly from);
            bool validTo = TryParseMonth(businessEvent.EffectiveTo, out DateOnly to);
            if (!validFrom || !validTo)
            {
                eventErrors.Add("EffectiveFrom and EffectiveTo must use the yyyy-MM format.");
            }
            else if (from > to)
            {
                eventErrors.Add("EffectiveFrom must not be later than EffectiveTo.");
            }

            if (businessEvent.ExpectedEnergyMultiplierMin <= 0m ||
                businessEvent.ExpectedEnergyMultiplierMax < businessEvent.ExpectedEnergyMultiplierMin)
            {
                eventErrors.Add("Expected energy multipliers must define a positive ordered range.");
            }

            if (businessEvent.ResetsBaseline && !businessEvent.Approved)
            {
                eventErrors.Add("Only an approved event may reset a baseline.");
            }

            if (eventErrors.Count > 0)
            {
                errors[$"businessEvents[{index}]"] = eventErrors.ToArray();
            }
        }

        return errors;
    }

    private static bool TryParseMonth(string? value, out DateOnly period) =>
        DateOnly.TryParseExact(value, MonthFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out period);
}
