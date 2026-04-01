using DI.MCP.Server.Configuration;
using DI.MCP.Server.Diagnostics;
using DI.MCP.Server.ExtensionMethods;
using DI.MCP.Server.Middleware;
using DI.MCP.Server.Prompts.Analytics;
using DI.MCP.Server.Prompts.Engagement;
using DI.MCP.Server.Resources;
using DI.MCP.Server.Resources.Analytics;
using DI.MCP.Server.Services.Analytics;
using DI.MCP.Server.Tools.Analytics;
using DI.MCP.Server.Tools.Engagement;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));
var settings = builder.Configuration
    .GetSection("AppSettings").Get<AppSettings>()!;

#region Commented: Only Analytics Tools
//builder.Services.AddMcpServer()
//    .WithHttpTransport()
//    .WithTools<AnalyticsTools>()
//    .WithPrompts<AnalyticsPrompts>()
//    .WithResources<ServerInfoResource>();
#endregion

var toolMethodMap = new ConcurrentDictionary<string, MethodInfo[]>();
toolMethodMap.PopulateToolMethodMap<IAnalyticsTools>(ToolCategories.Analytics);
toolMethodMap.PopulateToolMethodMap<IEngagementTools>(ToolCategories.Engagement);

var promptMethodMap = new ConcurrentDictionary<string, MethodInfo[]>();
promptMethodMap.PopulatePromptMethodMap<AnalyticsPrompts>(ToolCategories.Analytics);
promptMethodMap.PopulatePromptMethodMap<EngagementPrompts>(ToolCategories.Engagement);

//var resourceMethodMap = new ConcurrentDictionary<string, MethodInfo[]>();
//resourceMethodMap.PopulateResourceMethodMap<MetricDefinitionsResource>(ToolCategories.Analytics);
//resourceMethodMap.PopulateResourceMethodMap<EngagementResources>(ToolCategories.Engagement);

// Register tool types in DI
builder.Services.AddScoped<IAnalyticsTools, AnalyticsTools>();
builder.Services.AddScoped<IEngagementTools, EngagementTools>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
        options.ConfigureSessionOptions = async (httpContext, mcpOptions, cancellationToken) =>
        {
            var toolCategory = httpContext.Request.RouteValues["toolCategory"]?.ToString()?.ToLower() ?? ToolCategories.Analytics;

            // Configure tools for the requested category
            if (toolMethodMap.TryGetValue(toolCategory, out var methods))
            {
                mcpOptions.Capabilities ??= new();
                mcpOptions.Capabilities.Tools = new();
                var toolCollection = mcpOptions.ToolCollection = [];

                foreach (var method in methods)
                {
                    var target = httpContext.RequestServices.GetRequiredService(method.DeclaringType!);
                    var tool = McpServerTool.Create(method, target, new McpServerToolCreateOptions
                    {
                        Services = httpContext.RequestServices
                    });
                    toolCollection.Add(tool);
                }
            }

            // Configure prompts for the requested category
            if (promptMethodMap.TryGetValue(toolCategory, out var promptMethods))
            {
                mcpOptions.Capabilities ??= new();
                mcpOptions.Capabilities.Prompts = new();
                var promptCollection = mcpOptions.PromptCollection = [];

                foreach (var method in promptMethods)
                {
                    var prompt = McpServerPrompt.Create(method);
                    promptCollection.Add(prompt);
                }
            }

            //// Configure prompts for the requested category
            //if (resourceMethodMap.TryGetValue(toolCategory, out var resourceMethods))
            //{
            //    mcpOptions.Capabilities ??= new();
            //    mcpOptions.Capabilities.Resources = new();
            //    var resourceCollection = mcpOptions.ResourceCollection = [];

            //    foreach (var method in resourceMethods)
            //    {
            //        var resource = McpServerResource.Create(method);
            //        resourceCollection.Add(resource);
            //    }
            //}
        };
    })
    .WithResources<ServerInfoResource>()
    .WithResources<MetricDefinitionsResource>();

builder.Services.AddHttpClient<IDiAnalyticsClient, DiAnalyticsClient>(
    (sp, client) =>
    {
        var baseUrl = settings.ApiBaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    })
    .AddStandardResilienceHandler(options =>
    {
        // Retry: 2 retries with exponential backoff for transient failures (5xx, timeouts)
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        options.Retry.UseJitter = true;

        // Circuit breaker: open after sustained failures, half-open after 30s
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        // Per-attempt timeout (individual request)
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);

        // Total timeout across all attempts (retries included)
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds);
    });

builder.Services.AddScoped<McpRequestContext>();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<McpServerMetrics>();
//builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("DI.MCP.Server"));

builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

if (string.IsNullOrEmpty(settings.ApiBaseUrl))
    logger.LogWarning("Missing required setting: ApiBaseUrl");

if (settings.Debug)
    logger.LogWarning("Running in DEBUG mode — not suitable for production");

app.UseMiddleware<McpRequestContextMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapGet("/health", () => new { status = StatusCodes.Status200OK, version = "1.0" });

app.MapMcp("/mcp/{toolCategory}");
//app.MapMcp("/mcp/analytics");

logger.LogInformation("DI MCP Server starting on port 8000 at /mcp/{Analytics} and /mcp/{Engagement}", ToolCategories.Analytics, ToolCategories.Engagement);

app.Run();