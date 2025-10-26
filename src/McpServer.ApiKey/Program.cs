using McpServer.ApiKey.Middleware;
using McpServer.ApiKey.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add MCP server services with HTTP transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<MultiplicationTool>()
    .WithTools<TemperatureConverterTool>()
    .WithTools<WeatherTools>();

// Add CORS for HTTP transport support in browsers
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Enable CORS
app.UseCors();

// Add API Key authentication middleware
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// Map MCP endpoints
app.MapMcp();

// Add a simple home page
app.MapGet("/status", () => "MCP Server with API Key Authentication - Ready for use with HTTP transport");

app.Run();

