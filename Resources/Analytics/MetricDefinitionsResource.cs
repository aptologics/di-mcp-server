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
/// MCP Resource Template that exposes metric definitions as readable context.
/// Claude can load this as reference data at conversation start — no tool call needed
/// to discover available metrics.
/// 
/// Shares the same <see cref="IMemoryCache"/> and cache keys as the
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
    /// Returns the metric definitions catalog for the given subject.
    /// URI: <c>metrics://definitions/{subject}</c>
    /// </summary>
    [McpServerResource(
        UriTemplate = "metrics://definitions/{subject}",
        Name = "metric-definitions",
        MimeType = "application/json"),
     Description(AnalyticsToolsDescriptionProvider.MetricDefinitionsResource)]
    public async Task<string> GetDefinitions(
        [Description("The analytics subject: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF")]
        string subject)
    {
        var resolvedSubject = string.IsNullOrWhiteSpace(subject) ? "PRACTICE" : subject.ToUpperInvariant();

        _logger.LogDebug("Resource read metrics://definitions/{Subject}", resolvedSubject);

        try
        {
            // Shared cache key with get_metric_definitions tool
            var cacheKey = $"metricDefs:{resolvedSubject}";
            if (_cache.TryGetValue(cacheKey, out string? cached))
            {
                _logger.LogInformation("Cache HIT | Key={CacheKey} | Source=Resource", cacheKey);
                return cached!;
            }

            _logger.LogInformation("Cache MISS | Key={CacheKey} | Source=Resource", cacheKey);

            var result = await _analytics.GetMetricDefinitionsAsync(resolvedSubject);
            var json = JsonSerializer.Serialize(result, JsonOptions);

            var ttl = TimeSpan.FromMinutes(_settings.CacheTtlMinutes);
            _cache.Set(cacheKey, json, ttl);
            _logger.LogDebug("Cache SET | Key={CacheKey} | TTL={TtlMinutes}min | Source=Resource", cacheKey, _settings.CacheTtlMinutes);

            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resource read metrics://definitions/{Subject} failed", resolvedSubject);
            return JsonSerializer.Serialize(new
            {
                error = _settings.Debug
                    ? ex.Message
                    : "Failed to retrieve metric definitions."
            }, JsonOptions);
        }
    }
}
