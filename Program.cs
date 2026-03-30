using DIMCPServer.Configuration;
using DIMCPServer.ExtensionMethods;
using DIMCPServer.Middleware;
using DIMCPServer.Prompts.Analytics;
using DIMCPServer.Prompts.Engagement;
using DIMCPServer.Resources;
using DIMCPServer.Services.Analytics;
using DIMCPServer.Tools.Analytics;
using DIMCPServer.Tools.Engagement;
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
//    .WithResources<ServerInfoResource>();
#endregion

var toolMethodMap = new ConcurrentDictionary<string, MethodInfo[]>();
toolMethodMap.PopulateToolMethodMap<IAnalyticsTools>(ToolCategories.Analytics);
toolMethodMap.PopulateToolMethodMap<IEngagementTools>(ToolCategories.Engagement);

var promptMethodMap = new ConcurrentDictionary<string, MethodInfo[]>();
promptMethodMap.PopulatePromptMethodMap<AnalyticsPrompts>(ToolCategories.Analytics);
promptMethodMap.PopulatePromptMethodMap<EngagementPrompts>(ToolCategories.Engagement);

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
        };
    })
    .WithResources<ServerInfoResource>();

builder.Services.AddHttpClient<IDiAnalyticsClient, DiAnalyticsClient>(
    (sp, client) =>
    {
        var baseUrl = settings.ApiBaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    });

builder.Services.AddScoped<McpRequestContext>();

builder.Services.AddMemoryCache();

builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

if (string.IsNullOrEmpty(settings.ApiBaseUrl))
    logger.LogWarning("Missing required setting: ApiBaseUrl");

if (settings.Debug)
    logger.LogWarning("Running in DEBUG mode — not suitable for production");

app.UseMiddleware<McpRequestContextMiddleware>();

app.MapGet("/health", () => new { status = StatusCodes.Status200OK, version = "1.0" });

app.MapMcp("/mcp/{toolCategory}");
//app.MapMcp("/mcp/analytics");

logger.LogInformation("DI MCP Server starting on port 8000 at /mcp/{Analytics} and /mcp/{Engagement}", ToolCategories.Analytics, ToolCategories.Engagement);

app.Run();