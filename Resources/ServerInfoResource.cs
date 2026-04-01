using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DI.MCP.Server.Resources;

[McpServerResourceType]
public class ServerInfoResource
{
    //[McpServerResource(UriTemplate = "server://info"), Description("Server metadata — name, version, timestamp")]
    [McpServerResource, Description("Server metadata — name, version, timestamp")]
    public static string ServerInfo() => JsonSerializer.Serialize(new
    {
        message = "DI MCP Server",
        timestamp = DateTime.Now.ToString("o"),
        version = "1.0"
    });
}
