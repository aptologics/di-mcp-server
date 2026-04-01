using System.Text;
using System.Text.Json;
using DI.MCP.Server.Configuration;
using DI.MCP.Server.Models.Analytics;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Net.Http.Headers;

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

        // Forward validated Bearer token to downstream API
        if (!string.IsNullOrEmpty(requestContext.BearerToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", requestContext.BearerToken);
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

    private static readonly JsonSerializerSettings NewtonsoftSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new StringEnumConverter() },
        DateFormatString = "yyyy-MM-dd"
    };

    /// <summary>
    /// POST request to the downstream API.
    /// Serializes with Newtonsoft.Json to match the API's serialization library.
    /// </summary>
    private async Task<JsonElement> PostAsync(string path, object body)
    {
        _logger.LogDebug("POST {Path}", path);

        var json = JsonConvert.SerializeObject(body, NewtonsoftSettings);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(path, content);
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
        return System.Text.Json.JsonSerializer.SerializeToElement(result);
    }

    /// <summary>
    /// POST /Analytics/Metrics/Query
    /// Executes a metric query and returns rows of metric values, trends, goals, totals.
    /// </summary>
    public async Task<JsonElement> QueryMetricResultAsync(AnalyticsMetricInputQuery payload)
    {
        return await PostAsync("Analytics/Metrics/Query", payload);
    }

    /// <summary>
    /// POST /Analytics/Metrics/Query/Series
    /// Executes a time-series metric query.
    /// </summary>
    public async Task<JsonElement> QueryMetricSeriesResultAsync(AnalyticsMetricInputQuery payload)
    {
        return await PostAsync("Analytics/Metrics/Query/Series", payload);
    }

    /// <summary>
    /// POST /Analytics/Metrics/Benchmarks
    /// Retrieves industry benchmark values for selected metrics.
    /// </summary>
    public async Task<JsonElement> GetIndustryBenchmarksAsync(AnalyticsMetricInputQuery payload)
    {
        return await PostAsync("Analytics/Metrics/Benchmarks", payload);
    }

}
