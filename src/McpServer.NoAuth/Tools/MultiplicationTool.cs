using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.NoAuth.Tools;

[McpServerToolType]
public sealed class MultiplicationTool
{
    [McpServerTool, Description("Multiplies two numbers and returns the result.")]
    public static double Multiply(double a, double b)
    {
        return a * b;
    }
}
