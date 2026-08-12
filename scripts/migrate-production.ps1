[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Catalog)) {
    throw "Define ConnectionStrings__Catalog before running the production migration."
}

dotnet ef database update `
    --project src/IgnakeeAI.McpServer.Supplier.Infrastructure `
    --startup-project src/IgnakeeAI.McpServer.Supplier.Api `
    --configuration Release
