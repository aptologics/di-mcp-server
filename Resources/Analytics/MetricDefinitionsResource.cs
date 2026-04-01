using System.ComponentModel;
using System.Text.Json;
using DI.MCP.Server.Configuration;
using DI.MCP.Server.Prompts.Analytics;
using DI.MCP.Server.Services.Analytics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace DI.MCP.Server.Resources.Analytics;

/// <summary>
/// MCP Direct Resource that exposes the full metric definitions catalog as readable context.
/// Listed in <c>resources/list</c> so clients discover it without needing a tool call.
///
/// Fetches definitions for the default subject (PRACTICE) — the most common use case.
/// Shares the same <see cref="IMemoryCache"/> and cache key as the
/// <c>get_metric_definitions</c> tool, so whichever is called first warms the cache
/// for the other.
/// </summary>
[McpServerResourceType]
public class MetricDefinitionsResource
{
    private readonly IDiAnalyticsClient _analytics;
    private readonly IMemoryCache _cache;
    private readonly AppSettings _settings;
    private readonly ILogger<MetricDefinitionsResource> _logger;

    private const string DefaultSubject = "PRACTICE";
    private const string CacheKey = $"metricDefs:{DefaultSubject}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MetricDefinitionsResource(
        IDiAnalyticsClient analytics,
        IMemoryCache cache,
        IOptions<AppSettings> settings,
        ILogger<MetricDefinitionsResource> logger)
    {
        _analytics = analytics;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns the full metric definitions catalog (PRACTICE subject).
    /// URI: <c>metrics://definitions</c>
    /// </summary>
    [McpServerResource(
        UriTemplate = "metrics://definitions",
        Name = "metric-definitions",
        MimeType = "application/json"),
     Description(AnalyticsToolsDescriptionProvider.MetricDefinitionsResource)]
    public async Task<string> GetDefinitions()
    {
        _logger.LogDebug("Resource read metrics://definitions");

        try
        {
            if (_cache.TryGetValue(CacheKey, out string? cached))
            {
                _logger.LogInformation("Cache HIT | Key={CacheKey} | Source=Resource", CacheKey);
                return cached!;
            }

            _logger.LogInformation("Cache MISS | Key={CacheKey} | Source=Resource", CacheKey);

            var result = await _analytics.GetMetricDefinitionsAsync(DefaultSubject);
            var json = JsonSerializer.Serialize(result, JsonOptions);

            var ttl = TimeSpan.FromMinutes(_settings.CacheTtlMinutes);
            _cache.Set(CacheKey, json, ttl);
            _logger.LogDebug("Cache SET | Key={CacheKey} | TTL={TtlMinutes}min | Source=Resource", CacheKey, _settings.CacheTtlMinutes);

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resource read metrics://definitions failed");
            return JsonSerializer.Serialize(new
            {
                error = _settings.Debug
                    ? ex.Message
                    : "Failed to retrieve metric definitions."
            }, JsonOptions);
        }
    }
}
