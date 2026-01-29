using Azure.Identity;
using ModelContextProtocol.Client;

// Configuration - Update these values as needed
var mcpServerUrl = "https://app-ext-eus2-mcp-profx-01.azurewebsites.net/mcp";
var audience = "https://app-ext-eus2-mcp-profx-01.azurewebsites.net"; // Identifier URI from app registration
var tenantId = "978bbad2-037f-4859-8a78-385d36d264ee";

Console.WriteLine("=== MCP Client Test ===");
Console.WriteLine($"Server URL: {mcpServerUrl}");
Console.WriteLine($"Audience: {audience}");
Console.WriteLine($"Tenant: {tenantId}");
Console.WriteLine();

try
{
    // Step 1: Get a token using Azure CLI credentials
    Console.WriteLine("Step 1: Acquiring access token...");
    var credential = new AzureCliCredential(new AzureCliCredentialOptions
    {
        TenantId = tenantId
    });

    var tokenResult = await credential.GetTokenAsync(
        new Azure.Core.TokenRequestContext([$"{audience}/.default"]),
        CancellationToken.None);

    Console.WriteLine($"✅ Token acquired successfully!");
    Console.WriteLine($"   Token expires: {tokenResult.ExpiresOn:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"   Token length: {tokenResult.Token.Length} characters");
    Console.WriteLine();

    // Step 2: Connect to MCP server using HTTP transport with Bearer token
    Console.WriteLine("Step 2: Connecting to MCP server...");
    
    var transportOptions = new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        Name = "MCP Test Client",
        TransportMode = HttpTransportMode.AutoDetect, // Auto-detect the transport mode
        AdditionalHeaders = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {tokenResult.Token}" }
        }
    };

    await using var client = await McpClient.CreateAsync(
        new HttpClientTransport(transportOptions),
        new McpClientOptions
        {
            ClientInfo = new()
            {
                Name = "MCP Test Client",
                Version = "1.0.0"
            }
        });

    Console.WriteLine($"✅ Connected to MCP server!");
    Console.WriteLine($"   Server: {client.ServerInfo?.Name} v{client.ServerInfo?.Version}");
    Console.WriteLine();

    // Step 3: List available tools
    Console.WriteLine("Step 3: Listing available tools...");
    var tools = await client.ListToolsAsync();
    
    Console.WriteLine($"✅ Found {tools.Count} tools:");
    foreach (var tool in tools)
    {
        Console.WriteLine($"   - {tool.Name}: {tool.Description}");
    }
    Console.WriteLine();

    // Step 4: Try calling a simple tool
    if (tools.Any(t => t.Name == "multiply"))
    {
        Console.WriteLine("Step 4: Testing 'multiply' tool...");
        var result = await client.CallToolAsync(
            "multiply",
            new Dictionary<string, object?>
            {
                ["a"] = 7,
                ["b"] = 6
            });
        
        var textContent = result.Content.FirstOrDefault();
        Console.WriteLine($"✅ multiply(7, 6) = {textContent}");
        Console.WriteLine();
    }

    Console.WriteLine("=== All tests passed! ===");
    Console.WriteLine("The MCP server is working correctly.");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Full exception details:");
    Console.WriteLine(ex.ToString());
}
