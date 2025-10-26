using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.EntraID.Bearer.Tools;

[McpServerToolType]
public class TemperatureConverterTool
{
    [McpServerTool, Description("Convert temperature from Celsius to Fahrenheit.")]
    public static double CelsiusToFahrenheit(
        [Description("Temperature in Celsius.")] double celsius)
    {
        return (celsius * 9 / 5) + 32;
    }

    [McpServerTool, Description("Convert temperature from Fahrenheit to Celsius.")]
    public static double FahrenheitToCelsius(
        [Description("Temperature in Fahrenheit.")] double fahrenheit)
    {
        return (fahrenheit - 32) * 5 / 9;
    }
}
