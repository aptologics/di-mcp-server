namespace DIMCPServer.Configuration;

/// <summary>
/// Scoped context that holds per-request MCP header values
/// extracted from the incoming HTTP request.
/// </summary>
public class McpRequestContext
{
    /// <summary>Correlation ID for distributed tracing (X-Correlation-ID header).</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Auth token forwarded to downstream DI APIs (Di-Auth-Token header).</summary>
    public string? DiAuthToken { get; set; }

    /// <summary>The MCP tool category extracted from the route (e.g. "analytics", "engagement").</summary>
    public string? ToolCategory { get; set; }
}
