using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.EntraID.Bearer.Tools;

[McpServerToolType]
public class MultiplicationTool
{
    [McpServerTool, Description("Multiply two numbers together.")]
    public static int Multiply(
        [Description("The first number to multiply.")] int a,
        [Description("The second number to multiply.")] int b)
    {
        return a * b;
    }
}
