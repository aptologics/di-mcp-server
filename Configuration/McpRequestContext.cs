namespace DI.MCP.Server.Configuration;

/// <summary>
/// Scoped context that holds per-request MCP header values
/// extracted from the incoming HTTP request.
/// </summary>
public class McpRequestContext
{
    /// <summary>
    /// Correlation ID for distributed tracing (X-Correlation-ID header).
    /// Auto-generated if not provided by the caller.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Bearer token extracted from the Authorization header, forwarded to downstream DI APIs.</summary>
    public string? BearerToken { get; set; }

    /// <summary>The MCP tool category extracted from the route (e.g. "analytics", "engagement").</summary>
    public string? ToolCategory { get; set; }

    /// <summary>Email of the authenticated user (populated after token validation).</summary>
    public string? AuthenticatedUserEmail { get; set; }

    /// <summary>Authentication method used (e.g. "IDP", "Firebase", "InternalKey").</summary>
    public string? AuthMethod { get; set; }
}
