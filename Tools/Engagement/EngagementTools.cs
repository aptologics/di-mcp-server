using ModelContextProtocol.Server;

namespace DIMCPServer.Tools.Engagement;

[McpServerToolType]
public sealed class EngagementTools : IEngagementTools
{
    private readonly ILogger<EngagementTools> _logger;

    public EngagementTools(ILogger<EngagementTools> logger)
    {
        _logger = logger;
    }

    public string Echo(string message)
    {
        _logger.LogInformation(message);
        return "hello " + message;
    }
}
