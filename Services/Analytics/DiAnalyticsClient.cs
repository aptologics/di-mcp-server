using DI.MCP.Server.Configuration;
using DI.MCP.Server.Models.Analytics;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DI.MCP.Server.Services.Analytics;

public class DiAnalyticsClient : IDiAnalyticsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DiAnalyticsClient> _logger;

    private static readonly HashSet<string> MetricDefinitionFields = new()
    {
        "mnemonicKey", "label", "description", "displayFormat", "classification"
    };

    public DiAnalyticsClient(
        HttpClient httpClient,
        IOptions<AppSettings> settings,
        ILogger<DiAnalyticsClient> logger,
        McpRequestContext requestContext)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Use Di-Auth-Token from the incoming MCP request
        if (!string.IsNullOrEmpty(requestContext.DiAuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", requestContext.DiAuthToken);
        }

        // Forward Correlation ID for end-to-end distributed tracing
        _httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", requestContext.CorrelationId);
    }

    /// <summary>
    /// GET request to the downstream API.
    /// </summary>
    private async Task<JsonElement> GetAsync(string path)
    {
        _logger.LogDebug("GET {Path}", path);
        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// POST request to the downstream API.
    /// </summary>
    private async Task<JsonElement> PostAsync(string path, object body)
    {
        _logger.LogDebug("POST {Path}", path);

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var response = await _httpClient.PostAsJsonAsync(path, body, jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// GET /Analytics/Metrics/Definitions?subject={subject}
    /// Filters to KPI metrics and slims to only the needed fields.
    /// </summary>
    public async Task<JsonElement> GetMetricDefinitionsAsync(string subject)
    {
        var raw = await GetAsync($"Analytics/Metrics/Definitions?subject={subject}");

        var dataArray = raw.TryGetProperty("data", out var data)
            ? data.EnumerateArray()
            : Enumerable.Empty<JsonElement>();

        // Filter to KPI metrics and slim to only the needed fields
        var slimmed = dataArray
            .Where(item =>
                item.TryGetProperty("propertyCategory", out var cat) &&
                cat.GetString() == "KPI")
            .Select(item =>
            {
                var dict = new Dictionary<string, JsonElement>();
                foreach (var field in MetricDefinitionFields)
                {
                    if (item.TryGetProperty(field, out var val))
                        dict[field] = val;
                }
                return dict;
            })
            .ToList();

        var pageInfo = raw.TryGetProperty("pageInfo", out var pi)
            ? pi
            : JsonDocument.Parse("{}").RootElement;

        var result = new { data = slimmed, pageInfo };
        return JsonSerializer.SerializeToElement(result);
    }

    /// <summary>
    /// POST /Analytics/Metrics/Query
    /// Executes a metric query and returns rows of metric values, trends, goals, totals.
    /// </summary>
    public async Task<JsonElement> QueryMetricResultAsync(MetricQuery payload)
    {
        return await PostAsync("Analytics/Metrics/Query", payload);
    }

    /// <summary>
    /// POST /Analytics/Metrics/Query/Series
    /// Executes a time-series metric query.
    /// </summary>
    public async Task<JsonElement> QueryMetricSeriesResultAsync(MetricQuery payload)
    {
        return await PostAsync("Analytics/Metrics/Query/Series", payload);
    }

    /// <summary>
    /// POST /Analytics/Metrics/Benchmarks
    /// Retrieves industry benchmark values. Automatically sets options.includeBenchmarks = true.
    /// </summary>
    public async Task<JsonElement> GetIndustryBenchmarksAsync(MetricQuery payload)
    {
        payload.Options ??= new QueryOptions();
        payload.Options.IncludeBenchmarks = true;
        return await PostAsync("Analytics/Metrics/Benchmarks", payload);
    }

}
