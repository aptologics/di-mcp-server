using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DI.MCP.Server.Tools.Engagement
{
    public interface IEngagementTools
    {
        [McpServerTool, Description("Echoes the input back to the client.")]
        string Echo(string message);
    }
}
