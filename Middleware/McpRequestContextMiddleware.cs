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

        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            mcpContext.CorrelationId = correlationId.ToString();

        if (context.Request.Headers.TryGetValue("Di-Auth-Token", out var authToken))
            mcpContext.DiAuthToken = authToken.ToString();

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
