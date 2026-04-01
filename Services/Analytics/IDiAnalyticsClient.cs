using System.Text.Json;
using DI.MCP.Server.Models.Analytics;

namespace DI.MCP.Server.Services.Analytics;

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
    Task<JsonElement> QueryMetricResultAsync(AnalyticsMetricInputQuery payload);

    /// <summary>
    /// POST /Analytics/Metrics/Query/Series
    /// Executes a time-series metric query. Returns columns, values (per practice × period),
    /// and seriesTotals (aggregate per period).
    /// </summary>
    Task<JsonElement> QueryMetricSeriesResultAsync(AnalyticsMetricInputQuery payload);

    /// <summary>
    /// POST /Analytics/Metrics/Benchmarks
    /// Retrieves industry benchmark values for selected metrics.
    /// </summary>
    Task<JsonElement> GetIndustryBenchmarksAsync(AnalyticsMetricInputQuery payload);
}
