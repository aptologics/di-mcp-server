using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DIMCPServer.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for MCP Server request monitoring.
/// Uses .NET 8 built-in <see cref="System.Diagnostics.Metrics"/> API — no external packages required.
/// Azure Monitor, Prometheus, Grafana, and any OTLP-compatible collector can scrape these automatically.
/// </summary>
public sealed class McpServerMetrics
{
    public const string MeterName = "DIMCPServer";

    private readonly Counter<long> _requestCount;
    private readonly Counter<long> _errorCount;
    private readonly Histogram<double> _requestDuration;

    public McpServerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _requestCount = meter.CreateCounter<long>(
            "mcp.server.requests",
            unit: "{request}",
            description: "Total number of MCP requests received");

        _errorCount = meter.CreateCounter<long>(
            "mcp.server.errors",
            unit: "{error}",
            description: "Total number of MCP requests that resulted in an error");

        _requestDuration = meter.CreateHistogram<double>(
            "mcp.server.request.duration",
            unit: "ms",
            description: "Duration of MCP requests in milliseconds");
    }

    /// <summary>
    /// Records a completed MCP request with its duration and dimensions.
    /// </summary>
    public void RecordRequest(string toolCategory, int statusCode, double durationMs)
    {
        var tags = new TagList
        {
            { "mcp.tool.category", toolCategory },
            { "http.response.status_code", statusCode }
        };

        _requestCount.Add(1, tags);
        _requestDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records a failed MCP request with error classification.
    /// </summary>
    public void RecordError(string toolCategory, string errorType)
    {
        var tags = new TagList
        {
            { "mcp.tool.category", toolCategory },
            { "error.type", errorType }
        };

        _errorCount.Add(1, tags);
    }
}
