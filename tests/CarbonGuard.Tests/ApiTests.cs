using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarbonGuard.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarbonGuard.Tests;

public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> application)
    {
        _client = application.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/live", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeEndpoint_ReturnsStructuredResult()
    {
        var request = new AnalysisRequest(
        [
            new EmissionRecord(1, "Madrid", "2026-01", 12_000, 2_800),
        ]);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/anomaly-detection/analyze",
            request,
            CancellationToken.None);
        AnalysisResponse? payload = await response.Content.ReadFromJsonAsync<AnalysisResponse>(
            JsonOptions,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.Summary.TotalRecords);
        Assert.True(response.Headers.Contains("X-Trace-Id"));
    }

    [Fact]
    public async Task AnalyzeEndpoint_RejectsEmptyBatch()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/anomaly-detection/analyze",
            new AnalysisRequest([]),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeEndpoint_ReturnsProblemDetailsForMalformedJson()
    {
        using var content = new StringContent("{ bad json", System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/anomaly-detection/analyze",
            content,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AnalyzeEndpoint_RejectsInvalidBusinessContext()
    {
        var request = new AnalysisRequest(
            [new EmissionRecord(1, "Madrid", "2026-01", 12_000, 2_800)],
            [new BusinessEvent("Madrid", "invalid", "2026-01", "CapacityExpansion", 2m, 1m, false, true, null)]);

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/v1/anomaly-detection/analyze",
            request,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
