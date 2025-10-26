namespace McpServer.EntraID.Bearer.Models;

public class EntraIDOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();

    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}";
    
    public bool IsConfigured => 
        !string.IsNullOrWhiteSpace(TenantId) && 
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(Audience);
}
