using DIMCPServer.Configuration;

namespace DIMCPServer.Middleware;

/// <summary>
/// Middleware that extracts X-Correlation-ID and Di-Auth-Token headers
/// from incoming HTTP requests and populates the scoped <see cref="McpRequestContext"/>.
/// </summary>
public class McpRequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public McpRequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var mcpContext = context.RequestServices.GetRequiredService<McpRequestContext>();

        // Use caller-provided Correlation ID or keep the auto-generated one
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            mcpContext.CorrelationId = correlationId.ToString();

        if (context.Request.Headers.TryGetValue("Di-Auth-Token", out var authToken))
            mcpContext.DiAuthToken = authToken.ToString();

        // Extract tool category from route (e.g. /mcp/analytics → "analytics")
        mcpContext.ToolCategory = context.Request.RouteValues["toolCategory"]?.ToString()?.ToLower();

        // Echo CorrelationId back so the Orchestration Layer can correlate responses
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = mcpContext.CorrelationId;
            return Task.CompletedTask;
        });

        // Require Di-Auth-Token for MCP endpoints
        if (context.Request.Path.StartsWithSegments("/mcp") && string.IsNullOrEmpty(mcpContext.DiAuthToken))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing required header: Di-Auth-Token" });
            return;
        }

        await _next(context);
    }
}
