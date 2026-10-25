param(
    [string]$GatewayPath = (Join-Path (Get-Location) 'api-gateway')
)

$ErrorActionPreference = 'Stop'
$repo = 'https://github.com/patrickstolc/api-gateway.git'

if (-not (Test-Path -LiteralPath $GatewayPath)) {
    git clone $repo $GatewayPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not clone the gateway repository.' }
}

if (-not (Test-Path -LiteralPath (Join-Path $GatewayPath '.git'))) {
    throw "Not a Git repository: $GatewayPath"
}

$source = Join-Path $PSScriptRoot 'src/Configuration/ocelot.user-profile.json'
$destination = Join-Path $GatewayPath 'src/Configuration'
if (-not (Test-Path -LiteralPath $destination)) {
    throw "Gateway Configuration directory not found: $destination"
}

Copy-Item -LiteralPath $source -Destination (Join-Path $destination 'ocelot.user-profile.json') -Force
$example = Join-Path $destination 'ocelot.example.json'
if (Test-Path -LiteralPath $example) {
    Remove-Item -LiteralPath $example
}

Write-Host "User Profile routes are installed in $destination" -ForegroundColor Green
Write-Host 'Start your User Profile service at http://localhost:5194, then run docker compose up --build from the gateway directory.'
Write-Host 'After testing, commit the local changes in the gateway clone. Do not push to the teaching repository.'
