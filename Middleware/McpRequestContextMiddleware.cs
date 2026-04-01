using DI.MCP.Server.Configuration;
using DI.MCP.Server.Services.Authentication;

namespace DI.MCP.Server.Middleware;

/// <summary>
/// Middleware that extracts X-Correlation-ID and Authorization: Bearer / X-Internal-Key headers
/// from incoming HTTP requests, validates the token (IDP → Firebase → Internal Key),
/// populates the scoped <see cref="McpRequestContext"/>, and passes the validated token
/// as-is for downstream API forwarding.
/// </summary>
public class McpRequestContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpRequestContextMiddleware> _logger;

    public McpRequestContextMiddleware(RequestDelegate next, ILogger<McpRequestContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var mcpContext = context.RequestServices.GetRequiredService<McpRequestContext>();

        // Use caller-provided Correlation ID or keep the auto-generated one
        if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
            mcpContext.CorrelationId = correlationId.ToString();

        // Extract Bearer token from standard Authorization header
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            mcpContext.BearerToken = authHeader["Bearer ".Length..].Trim();
        }

        // Extract tool category from route (e.g. /mcp/analytics → "analytics")
        mcpContext.ToolCategory = context.Request.RouteValues["toolCategory"]?.ToString()?.ToLower();

        // Echo CorrelationId back so the Orchestration Layer can correlate responses
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = mcpContext.CorrelationId;
            return Task.CompletedTask;
        });

        // ── Token validation for MCP endpoints ──
        if (context.Request.Path.StartsWithSegments("/mcp"))
        {
            var validationService = context.RequestServices.GetRequiredService<ITokenValidationService>();
            TokenValidationResult result;

            if (!string.IsNullOrEmpty(mcpContext.BearerToken))
            {
                // Path A: Bearer token present → validate via IDP userinfo / Firebase
                result = await validationService.ValidateBearerTokenAsync(mcpContext.BearerToken);
            }
            else if (context.Request.Headers.TryGetValue("X-Internal-Key", out var internalKey)
                     && !string.IsNullOrWhiteSpace(internalKey.ToString()))
            {
                // Path B: No Bearer token, but X-Internal-Key header provided
                result = validationService.ValidateInternalKey(internalKey.ToString());
            }
            else
            {
                // Path C: No authentication credentials at all
                result = TokenValidationResult.Failure("Authorization is missing or invalid");
            }

            if (!result.IsValid)
            {
                _logger.LogWarning(
                    "Authentication failed | Path={Path} | Reason={Reason}",
                    context.Request.Path, result.Error);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new { code = 401, message = result.Error ?? "The token is not valid or has expired" }
                });
                return;
            }

            // Store validated user info in context for downstream use
            mcpContext.AuthenticatedUserEmail = result.UserEmail;
            mcpContext.AuthMethod = result.AuthMethod;

            _logger.LogDebug(
                "Authentication succeeded | Method={AuthMethod} | User={User}",
                result.AuthMethod, result.UserEmail);
        }

        // Token is validated — pass it as-is to the downstream API via DiAuthToken
        await _next(context);
    }
}
