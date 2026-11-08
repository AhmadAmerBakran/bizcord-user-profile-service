param(
    [string]$GatewayPath = (Join-Path (Get-Location) 'api-gateway')
)

$ErrorActionPreference = 'Stop'
$repository = 'https://github.com/patrickstolc/api-gateway.git'

if (-not (Test-Path -LiteralPath $GatewayPath)) {
    git clone $repository $GatewayPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not clone the gateway.' }
}

$destination = Join-Path $GatewayPath 'src/Configuration'
if (-not (Test-Path -LiteralPath (Join-Path $GatewayPath '.git')) -or -not (Test-Path -LiteralPath $destination)) {
    throw "Gateway clone not found at $GatewayPath"
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ocelot.user-profile.json') -Destination (Join-Path $destination 'ocelot.user-profile.json') -Force

Write-Host 'The week 45 User Profile routes were copied to the teaching gateway.'
Write-Host 'Do not also install the week 43 route file: this file replaces it.'
Write-Host 'Run the gateway and service with the same JWT signing key supplied securely at runtime.'
Write-Host 'Keep the standalone gateway changes local until they have been tested.'
