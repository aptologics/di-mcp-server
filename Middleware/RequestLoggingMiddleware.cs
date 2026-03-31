using DIMCPServer.Configuration;
using DIMCPServer.Diagnostics;
using System.Diagnostics;

namespace DIMCPServer.Middleware;

/// <summary>
/// Middleware that wraps each request in a structured log scope containing
/// CorrelationId and ToolCategory, logs request duration on completion,
/// and records quantitative metrics (counters, histograms) via <see cref="McpServerMetrics"/>.
/// Should be registered after <see cref="McpRequestContextMiddleware"/> so
/// that the <see cref="McpRequestContext"/> is already populated.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly McpServerMetrics _metrics;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, McpServerMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip non-MCP endpoints (e.g. /health)
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await _next(context);
            return;
        }

        var mcpContext = context.RequestServices.GetRequiredService<McpRequestContext>();
        var toolCategory = mcpContext.ToolCategory ?? "unknown";

        // Push CorrelationId and ToolCategory into every log entry within this scope
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = mcpContext.CorrelationId,
            ["ToolCategory"] = toolCategory
        }))
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "MCP request started | Method={Method} | Path={Path}",
                context.Request.Method,
                context.Request.Path);

            try
            {
                await _next(context);
                stopwatch.Stop();

                var durationMs = stopwatch.Elapsed.TotalMilliseconds;
                var statusCode = context.Response.StatusCode;

                _logger.LogInformation(
                    "MCP request completed | StatusCode={StatusCode} | Duration={Duration}ms",
                    statusCode,
                    stopwatch.ElapsedMilliseconds);

                _metrics.RecordRequest(toolCategory, statusCode, durationMs);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                var durationMs = stopwatch.Elapsed.TotalMilliseconds;
                var statusCode = context.Response.HasStarted ? context.Response.StatusCode : 500;

                _logger.LogError(ex,
                    "MCP request failed | StatusCode={StatusCode} | Duration={Duration}ms",
                    statusCode,
                    stopwatch.ElapsedMilliseconds);

                _metrics.RecordRequest(toolCategory, statusCode, durationMs);
                _metrics.RecordError(toolCategory, ex.GetType().Name);

                throw;
            }
        }
    }
}
