# Get Access Token for MCP Server EntraID

$tenantId = "<your-tenant-id>"
$clientId = "<your-client-id>"
$clientSecret = "<your-client-secret>"
$scope = "<your-app-scope>"

$tokenUrl = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"

$body = @{
    client_id     = $clientId
    client_secret = $clientSecret
    scope         = $scope
    grant_type    = "client_credentials"
}

try {
    $response = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
    
    Write-Host "✅ Token obtained successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Access Token:" -ForegroundColor Yellow
    Write-Host $response.access_token
    Write-Host ""
    Write-Host "Expires in: $($response.expires_in) seconds" -ForegroundColor Cyan
    
    # Copy to clipboard if available
    if (Get-Command Set-Clipboard -ErrorAction SilentlyContinue) {
        $response.access_token | Set-Clipboard
        Write-Host "✅ Token copied to clipboard!" -ForegroundColor Green
    }
    
    return $response.access_token
}
catch {
    Write-Host "❌ Error obtaining token:" -ForegroundColor Red
    Write-Host $_.Exception.Message
}
