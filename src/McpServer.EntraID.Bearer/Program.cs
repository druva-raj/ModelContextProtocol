using System.Security.Claims;
using McpServer.EntraID.Bearer.Models;
using McpServer.EntraID.Bearer.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EntraIDOptions>(builder.Configuration.GetSection("EntraID"));

var serverUrl = builder.Configuration["ServerUrl"]!;
var entraIdOptions = builder.Configuration.GetSection("EntraID").Get<EntraIDOptions>()!;

Console.WriteLine($"EntraID authentication enabled for tenant: {entraIdOptions.TenantId}");

string[] validAudiences = new[] { entraIdOptions.Audience, entraIdOptions.ClientId };
string[] validIssuers = new[]
{
    $"https://sts.windows.net/{entraIdOptions.TenantId}/",
    $"{entraIdOptions.Instance.TrimEnd('/')}/{entraIdOptions.TenantId}/v2.0",
    entraIdOptions.Authority
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = entraIdOptions.Authority;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudiences = validAudiences,
        ValidIssuers = validIssuers,
        NameClaimType = "name",
        RoleClaimType = "roles",
    };
    
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var name = context.Principal?.Identity?.Name ?? "unknown";
            var email = context.Principal?.FindFirstValue("preferred_username") ?? 
                       context.Principal?.FindFirstValue("upn") ?? 
                       context.Principal?.FindFirstValue("email") ?? "unknown";
            var oid = context.Principal?.FindFirstValue("oid") ?? "unknown";
            
            Console.WriteLine($"Token validated for: {name} ({email}) [OID: {oid}]");
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine("Authentication challenge issued");
            return Task.CompletedTask;
        }
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = new Uri(serverUrl),
        ResourceDocumentation = new Uri("https://docs.microsoft.com/entra/identity/"),
        AuthorizationServers = { new Uri(entraIdOptions.Authority) },
        ScopesSupported = entraIdOptions.Scopes.Length > 0 
            ? entraIdOptions.Scopes.ToList() 
            : new List<string>()
    };
});

builder.Services.AddAuthorization();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<MultiplicationTool>()
    .WithTools<TemperatureConverterTool>()
    .WithTools<WeatherTools>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Log startup configuration
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"Server URL: {serverUrl}");
Console.WriteLine($"Authority: {entraIdOptions.Authority}");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Enable detailed errors in production for debugging deployment issues
    app.UseDeveloperExceptionPage();
}

app.UseCors();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization();

app.MapGet("/", () => new
{
    status = "running",
    server = "MCP Server with EntraID Authentication",
    authenticationEnabled = true,
    tenant = entraIdOptions.TenantId,
    audience = entraIdOptions.Audience
}).AllowAnonymous();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();
