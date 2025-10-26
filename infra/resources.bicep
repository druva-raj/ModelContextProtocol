param location string
param tags object
param namingPrefix string
param instanceNumber string

@description('The SKU of App Service Plan.')
param sku string = 'S1'

// Naming pattern: {resourceType}-{exposure}-{namingPrefix}-{instance}
// Example: plan-ext-dev-eus2-mcp-01
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: 'plan-ext-${namingPrefix}-${instanceNumber}'
  location: location
  sku: {
    name: sku
    capacity: 1
  }
  properties: {
    reserved: false
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'app-ext-${namingPrefix}-${instanceNumber}'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: true
    siteConfig: {
      minTlsVersion: '1.2'
      http20Enabled: true
      alwaysOn: true
      windowsFxVersion: 'DOTNET|9.0'
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnet'
        }
      ]
    }
  }
  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      SCM_DO_BUILD_DURING_DEPLOYMENT: 'true'
      WEBSITE_HTTPLOGGING_RETENTION_DAYS: '3'
    }
  }
}

output WEB_URI string = 'https://${webApp.properties.defaultHostName}'
