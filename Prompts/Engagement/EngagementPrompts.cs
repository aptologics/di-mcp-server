using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DIMCPServer.Prompts.Engagement;

/// <summary>
/// MCP Prompts for Engagement — user-controlled templates for engagement workflows.
/// Discoverable via prompts/list, retrievable via prompts/get.
/// </summary>
[McpServerPromptType]
public sealed class EngagementPrompts
{
    [McpServerPrompt(Name = "greet-user"),
     Description("Send a personalized greeting. Guides the LLM to use the Echo tool to confirm connectivity and greet the user.")]
    public static IList<PromptMessage> GreetUser(
        [Description("The user's name or identifier")]
        string name)
    {
        return
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Greet {name} and confirm the engagement service is working."
                }
            },
            new PromptMessage
            {
                Role = Role.Assistant,
                Content = new TextContentBlock
                {
                    Text = $"""
                        I'll use the `Echo` tool to confirm connectivity, then greet {name}.

                        Let me send a test message now.
                        """
                }
            }
        ];
    }
}
