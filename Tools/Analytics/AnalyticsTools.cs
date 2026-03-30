using System.Text.Json;
using DIMCPServer.Configuration;
using DIMCPServer.Models.Analytics;
using DIMCPServer.Services.Analytics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace DIMCPServer.Tools.Analytics;

/// <summary>
/// Provides tools for querying analytics metrics, metric series, and industry benchmarks from the analytics service.
/// Supports retrieving metric definitions and executing metric queries with flexible filtering, grouping, and options.
/// </summary>
/// <remarks>This class is intended for use in server-side analytics scenarios where metric data needs to be
/// queried, aggregated, or benchmarked. Methods return results as JSON strings, which may include error information if
/// the underlying analytics service call fails. Results for metric definitions are cached for 24 hours to improve
/// performance. All methods are asynchronous and should be awaited. Thread safety is ensured for concurrent calls to
/// public methods.</remarks>
[McpServerToolType]
public sealed class AnalyticsTools : IAnalyticsTools
{
    private readonly IDiAnalyticsClient _analytics;
    private readonly IMemoryCache _cache;
    private readonly AppSettings _settings;
    private readonly ILogger<AnalyticsTools> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AnalyticsTools(IDiAnalyticsClient analytics, IMemoryCache cache, IOptions<AppSettings> settings, ILogger<AnalyticsTools> logger)
    {
        _analytics = analytics;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    #region TOOLS

    /// <summary>
    /// Retrieves the metric definitions for the specified subject as a JSON string.
    /// </summary>
    /// <remarks>Results are cached for 24 hours to improve performance. If the subject is not specified,
    /// metric definitions for the default subject are returned. The returned JSON may include an error message if the
    /// underlying API call fails.</remarks>
    /// <param name="subject">The subject for which to retrieve metric definitions. If null, the default subject "PRACTICE" is used.</param>
    /// <returns>A JSON string containing the metric definitions for the specified subject. If an error occurs, the JSON string
    /// contains an error message.</returns>
    public async Task<string> GetMetricDefinitions(string? subject)
    {
        var resolvedSubject = subject ?? "PRACTICE";
        _logger.LogDebug("get_metric_definitions subject={Subject}", resolvedSubject);

        try
        {
            var cacheKey = $"metricDefs:{resolvedSubject}";
            if (_cache.TryGetValue(cacheKey, out string? cached))
                return cached!;

            var result = await _analytics.GetMetricDefinitionsAsync(resolvedSubject);
            var json = JsonSerializer.Serialize(result, JsonOptions);

            _cache.Set(cacheKey, json, TimeSpan.FromHours(24));
            return json;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "get_metric_definitions failed");
            return JsonSerializer.Serialize(new { error = $"API error: {ex.Message}" }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "get_metric_definitions unexpected error");
            return JsonSerializer.Serialize(new
            {
                error = _settings.Debug
                    ? ex.Message
                    : "Failed to retrieve metric definitions."
            }, JsonOptions);
        }
    }

    /// <summary>
    /// Executes a metric query against the analytics service and returns the result as a JSON string.
    /// </summary>
    /// <remarks>If an error occurs during the query, the returned JSON string includes an error property with
    /// details. The structure of the result depends on the selected fields and query parameters.</remarks>
    /// <param name="select">A list of metric fields to include in the query result. Cannot be null or empty.</param>
    /// <param name="from">The data source or entity to query metrics from. Cannot be null or empty.</param>
    /// <param name="where">A list of conditions to filter the query results. Each condition specifies a filter to apply.</param>
    /// <param name="rootPracticeId">The identifier of the root practice context for the query. Cannot be null or empty.</param>
    /// <param name="trendBy">An optional value specifying how to group results by trend, such as by date or another dimension.</param>
    /// <param name="options">Optional query options that control aspects such as paging, sorting, or additional query behaviors.</param>
    /// <param name="groupBy">An optional field name to group the query results by.</param>
    /// <param name="clientCurrentDate">An optional client-supplied date string to use as the current date in the query context.</param>
    /// <param name="grantId">An optional grant identifier to further scope or filter the query.</param>
    /// <returns>A JSON string representing the query result. If the query fails, the JSON contains an error message.</returns>
    public async Task<string> QueryMetricResult(List<string> select, string from, List<WhereClause> where, string rootPracticeId, TrendBy? trendBy = null, QueryOptions? options = null, string? groupBy = null, string? clientCurrentDate = null, int? grantId = null)
    {
        _logger.LogDebug("query_metric_result from={From}, select={Select}",
            from, string.Join(",", select));

        try
        {
            var payload = BuildMetricQuery(select, from, where, rootPracticeId, trendBy, options, groupBy, clientCurrentDate, grantId);

            var result = await _analytics.QueryMetricResultAsync(payload);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "query_metric_result failed");
            return JsonSerializer.Serialize(new { error = $"API error: {ex.Message}" }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "query_metric_result unexpected error");
            return JsonSerializer.Serialize(new
            {
                error = _settings.Debug
                    ? ex.Message
                    : "Failed to execute metric query."
            }, JsonOptions);
        }
    }

    /// <summary>
    /// Executes a metric series query with the specified parameters and returns the result as a JSON string.
    /// </summary>
    /// <remarks>The returned JSON string can represent either a successful result or an error object,
    /// depending on the outcome of the query. Callers should parse the JSON and check for an error field to determine
    /// if the operation was successful.</remarks>
    /// <param name="select">A list of metric fields to include in the query result. Cannot be null or empty.</param>
    /// <param name="from">The data source or entity from which to query metric series. Cannot be null or empty.</param>
    /// <param name="where">A list of filter conditions to apply to the query. Each condition defines a constraint on the data returned.</param>
    /// <param name="rootPracticeId">The identifier of the root practice context for the query. Cannot be null or empty.</param>
    /// <param name="trendBy">An optional value specifying how to group results by trend, such as by time period or category.</param>
    /// <param name="options">Optional query options that control aspects such as paging, sorting, or additional query behaviors.</param>
    /// <param name="groupBy">An optional field name by which to group the query results.</param>
    /// <param name="clientCurrentDate">An optional client-supplied date string to use as the current date context for the query.</param>
    /// <param name="grantId">An optional grant identifier to further scope or filter the query results.</param>
    /// <returns>A JSON string representing the metric series query result. If the query fails, the JSON contains an error
    /// message.</returns>
    public async Task<string> QueryMetricSeriesResult(List<string> select, string from, List<WhereClause> where, string rootPracticeId, TrendBy? trendBy = null, QueryOptions? options = null, string? groupBy = null, string? clientCurrentDate = null, int? grantId = null)
    {
        _logger.LogDebug("query_metric_series_result from={From}, select={Select}",
            from, string.Join(",", select));

        try
        {
            var payload = BuildMetricQuery(select, from, where, rootPracticeId, trendBy, options, groupBy, clientCurrentDate, grantId);

            var result = await _analytics.QueryMetricSeriesResultAsync(payload);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "query_metric_series_result failed");
            return JsonSerializer.Serialize(new { error = $"API error: {ex.Message}" }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "query_metric_series_result unexpected error");
            return JsonSerializer.Serialize(new
            {
                error = _settings.Debug
                    ? ex.Message
                    : "Failed to execute series metric query."
            }, JsonOptions);
        }
    }

    /// <summary>
    /// Retrieves industry benchmark metrics based on the specified query parameters.
    /// </summary>
    /// <remarks>If an error occurs during the request, the returned JSON will contain an error property with
    /// details. The method serializes the results or error information as a JSON string for client
    /// consumption.</remarks>
    /// <param name="select">A list of metric field names to include in the results. Cannot be null or empty.</param>
    /// <param name="from">The data source or entity from which to retrieve benchmark metrics. Cannot be null or empty.</param>
    /// <param name="where">A list of conditions used to filter the benchmark data. Each condition defines a filter to apply to the query.</param>
    /// <param name="rootPracticeId">The identifier of the root practice context for the benchmark query. Cannot be null or empty.</param>
    /// <param name="options">Optional query options that control aspects such as paging, sorting, or additional query behaviors. May be null.</param>
    /// <param name="groupBy">An optional field name by which to group the benchmark results. If null, results are not grouped.</param>
    /// <param name="clientCurrentDate">An optional client-supplied date string to use as the current date context for the query. If null, the server's
    /// current date is used.</param>
    /// <param name="grantId">An optional grant identifier to further scope the benchmark data. If null, no grant filtering is applied.</param>
    /// <returns>A JSON-formatted string containing the industry benchmark results. If an error occurs, the returned JSON
    /// includes an error message.</returns>
    public async Task<string> GetIndustryBenchmarks(List<string> select, string from, List<WhereClause> where, string rootPracticeId, QueryOptions? options = null, string? groupBy = null, string? clientCurrentDate = null, int? grantId = null)
    {
        _logger.LogDebug("get_industry_benchmarks from={From}, select={Select}",
            from, string.Join(",", select));

        try
        {
            var payload = BuildMetricQuery(select, from, where, rootPracticeId, trendBy: null, options, groupBy, clientCurrentDate, grantId);

            var result = await _analytics.GetIndustryBenchmarksAsync(payload);
            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "get_industry_benchmarks failed");
            return JsonSerializer.Serialize(new { error = $"API error: {ex.Message}" }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "get_industry_benchmarks unexpected error");
            return JsonSerializer.Serialize(new
            {
                error = _settings.Debug
                    ? ex.Message
                    : "Failed to retrieve industry benchmarks."
            }, JsonOptions);
        }
    }

    #endregion

    #region Build Metric Query

    /// <summary>
    /// Builds a MetricQuery object using the specified selection, source, filters, grouping, and additional query
    /// options.
    /// </summary>
    /// <param name="select">A list of column names to include in the query result.</param>
    /// <param name="from">The name of the data source or table to query.</param>
    /// <param name="where">A list of where clauses that define the filtering conditions for the query.</param>
    /// <param name="rootPracticeId">The root practice identifier as a string. Must be a valid GUID or null if not applicable.</param>
    /// <param name="trendBy">An optional value specifying how to trend the results, such as by date or another dimension.</param>
    /// <param name="options">Optional query options that influence query execution, such as paging or sorting. If null, default options are
    /// used.</param>
    /// <param name="groupBy">An optional string specifying the grouping dimension for the query. Must match a valid MetricQueryGroupBy value
    /// if provided.</param>
    /// <param name="clientCurrentDate">An optional string representing the client's current date. Accepted formats are "MM-dd-yyyy", "yyyy-MM-dd", or
    /// any valid DateTime string.</param>
    /// <param name="grantId">An optional grant identifier to further filter the query results.</param>
    /// <returns>A MetricQuery object populated with the specified selection, filters, grouping, and options.</returns>
    private static MetricQuery BuildMetricQuery(List<string> select, string from, List<WhereClause> where, string rootPracticeId, TrendBy? trendBy, QueryOptions? options, string? groupBy, string? clientCurrentDate, int? grantId)
    {
        MetricQueryGroupBy? groupByEnum = null;
        if (!string.IsNullOrEmpty(groupBy) && Enum.TryParse<MetricQueryGroupBy>(groupBy, true, out var parsed))
        {
            groupByEnum = parsed;
        }

        DateTime? parsedDate = null;
        if (!string.IsNullOrEmpty(clientCurrentDate))
        {
            if (DateTime.TryParseExact(clientCurrentDate, "MM-dd-yyyy", null, System.Globalization.DateTimeStyles.None, out var date1))
                parsedDate = date1;
            else if (DateTime.TryParseExact(clientCurrentDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date2))
                parsedDate = date2;
            else if (DateTime.TryParse(clientCurrentDate, out var date3))
                parsedDate = date3;
        }

        return new MetricQuery
        {
            Select = select,
            From = from,
            Where = where,
            GroupBy = groupByEnum,
            RootPracticeId = Guid.TryParse(rootPracticeId, out var rootGuid) ? rootGuid : null,
            ClientCurrentDate = parsedDate,
            GrantId = grantId?.ToString(),
            TrendBy = trendBy,
            Options = options ?? new QueryOptions()
        };
    }

    #endregion
}
