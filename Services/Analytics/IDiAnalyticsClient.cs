using System.Text.Json;
using DIMCPServer.Models.Analytics;

namespace DIMCPServer.Services.Analytics;

public interface IDiAnalyticsClient
{
    /// <summary>
    /// GET /Analytics/Metrics/Definitions?subject={subject}
    /// Returns KPI metric definitions slimmed to: mnemonicKey, label, description, displayFormat, classification
    /// </summary>
    Task<JsonElement> GetMetricDefinitionsAsync(string subject);

    /// <summary>
    /// POST /Analytics/Metrics/Query
    /// Executes a metric query and returns rows of metric values, trends, goals, totals.
    /// </summary>
    Task<JsonElement> QueryMetricResultAsync(MetricQuery payload);

    /// <summary>
    /// POST /Analytics/Metrics/Query/Series
    /// Executes a time-series metric query. Returns columns, values (per practice × period),
    /// and seriesTotals (aggregate per period).
    /// </summary>
    Task<JsonElement> QueryMetricSeriesResultAsync(MetricQuery payload);

    /// <summary>
    /// POST /Analytics/Metrics/Benchmarks
    /// Retrieves industry benchmark values for selected metrics.
    /// Automatically sets options.includeBenchmarks = true before posting.
    /// </summary>
    Task<JsonElement> GetIndustryBenchmarksAsync(MetricQuery payload);
}
