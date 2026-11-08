param(
    [string]$GatewayUrl = "http://localhost:8080",
    [string]$ServiceUrl = "http://localhost:5194"
)

$ErrorActionPreference = 'Stop'

function New-TestToken([string]$role, [string]$scope) {
    $claims = @{
        sub = [guid]::NewGuid().ToString()
        role = $role
        scope = $scope
    }
    $body = @{ claims = $claims } | ConvertTo-Json -Depth 5
    $result = Invoke-RestMethod -Method Post -Uri "$GatewayUrl/faucet" -ContentType "application/json" -Body $body
    return $result.value
}

function Get-Status([string]$url, [string]$token = '') {
    $headers = @{}
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    try {
        $response = Invoke-WebRequest -Method Get -Uri $url -Headers $headers -UseBasicParsing
        return [int]$response.StatusCode
    } catch {
        if ($_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

function Assert-Status([string]$name, [string]$url, [string]$token, [int]$expected) {
    $actual = Get-Status -url $url -token $token
    if ($actual -ne $expected) {
        throw "$name expected HTTP $expected but got $actual"
    }
    Write-Host "$name returned HTTP $expected"
}

$id = [guid]::NewGuid().ToString()
$gatewayPath = "$GatewayUrl/internal/user-profiles/$id"
$servicePath = "$ServiceUrl/internal/user-profiles/$id"
$allowedToken = New-TestToken 'internal-service' 'profile.internal.read'
$wrongRole = New-TestToken 'member' 'profile.internal.read'
$wrongScope = New-TestToken 'internal-service' 'profile.read'

Assert-Status 'Missing token at gateway' $gatewayPath '' 401
Assert-Status 'Wrong role at gateway' $gatewayPath $wrongRole 403
Assert-Status 'Wrong scope at gateway' $gatewayPath $wrongScope 403
Assert-Status 'Correct role and scope at gateway' $gatewayPath $allowedToken 404
Assert-Status 'Missing token directly at API' $servicePath '' 401
Assert-Status 'Wrong role directly at API' $servicePath $wrongRole 403
Assert-Status 'Allowed token directly at API' $servicePath $allowedToken 404

Write-Host 'Week 45 gateway and receiving service security checks passed.'
