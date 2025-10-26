targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param name string

@minLength(1)
@description('Location for all resources. This region must support Availability Zones.')
param location string

@description('Environment type (e.g., dev, stg, prd)')
param environmentType string = 'dev'

@description('Workload identifier (e.g., mcp, api, web)')
param workloadName string = 'mcp'

@description('Instance number for resource uniqueness')
param instanceNumber string = '01'

// Location abbreviations mapping
var locationAbbreviations = {
  eastus: 'eus'
  eastus2: 'eus2'
  westus: 'wus'
  westus2: 'wus2'
  westus3: 'wus3'
  centralus: 'cus'
  northcentralus: 'ncus'
  southcentralus: 'scus'
  westcentralus: 'wcus'
}

var locationAbbr = contains(locationAbbreviations, location) ? locationAbbreviations[location] : substring(location, 0, 4)
var tags = { 
  'azd-env-name': name
  'environment': environmentType
  'workload': workloadName
}

// Naming pattern: {resourceType}-{exposure}-{location}-{workload}-{environment}-{instance}
// Example: rg-ext-eus2-mcp-dev-01
var namingPrefix = '${environmentType}-${locationAbbr}-${workloadName}'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-ext-${namingPrefix}-${instanceNumber}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'resources'
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    namingPrefix: namingPrefix
    instanceNumber: instanceNumber
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = resourceGroup.name
