using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.EntraID.Bearer.Tools;

[McpServerToolType]
public class ValidateUserTool
{
    [McpServerTool(Name = "validate_user"), Description("Validates a user and returns their validation status.")]
    public static string ValidateUser(
        [Description("The username to validate.")] string username)
    {
        // Placeholder implementation - always returns validated
        return $"User '{username}' is validated.";
    }
}
