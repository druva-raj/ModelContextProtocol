# ModelContextProtocol

A collection of Model Context Protocol (MCP) server implementations in .NET with different authentication strategies.

## Projects

### 1. McpServer.NoAuth
Basic MCP server without authentication - ideal for development and testing.

**Location:** `src/McpServer.NoAuth`

**Features:**
- No authentication required
- HTTP transport
- Sample tools: Multiplication, Temperature Conversion, Weather

### 2. McpServer.ApiKey
MCP server with API Key authentication using custom middleware.

**Location:** `src/McpServer.ApiKey`

**Features:**
- API Key authentication via `X-API-Key` header
- Configurable API key via appsettings
- HTTP transport
- Sample tools: Multiplication, Temperature Conversion, Weather

### 3. McpServer.EntraID (NEW)
MCP server with Microsoft EntraID (Azure AD) authentication supporting both Client Credentials flow and Bearer token authentication.

**Location:** `src/McpServer.EntraID`

**Features:**
- ✅ Microsoft EntraID (Azure AD) authentication
- ✅ JWT Bearer token validation
- ✅ Client Credentials flow support
- ✅ Supports both v1.0 and v2.0 tokens
- ✅ Falls back to no-auth mode if not configured
- ✅ PowerShell scripts for token generation and testing
- ✅ HTTP transport
- ✅ Sample tools: Multiplication, Temperature Conversion, Weather

**Quick Start:**
```bash
cd src/McpServer.EntraID
dotnet run
```

See [McpServer.EntraID/README.md](src/McpServer.EntraID/README.md) for detailed setup instructions.

## Getting Started

### Prerequisites
- .NET 9.0 SDK or later
- Azure subscription (for EntraID authentication)

### Running a Server

```bash
# No authentication
cd src/McpServer.NoAuth
dotnet run

# API Key authentication
cd src/McpServer.ApiKey
dotnet run

# EntraID authentication
cd src/McpServer.EntraID
dotnet run
```

## Building All Projects

```bash
dotnet build
```

## Testing

Each project includes its own testing approach:

- **NoAuth/ApiKey**: Direct HTTP calls with curl or PowerShell
- **EntraID**: Use provided PowerShell scripts in `Scripts/` folder
  - `Get-Token.ps1` - Obtain Bearer token
  - `Test-McpServer.ps1` - Run comprehensive tests

## License

See [LICENSE](LICENSE) file for details.

## References

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Microsoft EntraID Documentation](https://docs.microsoft.com/azure/active-directory/)
- [.NET MCP Template Reference](https://github.com/mitch-b/dotnet-mcp-template)