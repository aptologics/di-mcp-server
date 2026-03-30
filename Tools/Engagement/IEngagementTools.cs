using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DIMCPServer.Tools.Engagement
{
    public interface IEngagementTools
    {
        [McpServerTool, Description("Echoes the input back to the client.")]
        string Echo(string message);
    }
}
