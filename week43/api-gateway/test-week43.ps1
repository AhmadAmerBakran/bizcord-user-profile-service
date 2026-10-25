param(
    [string]$GatewayUrl = 'http://localhost:8080',
    [string]$ServiceUrl = 'http://localhost:5194'
)

$ErrorActionPreference = 'Stop'

function New-DemoToken {
    param([string]$Subject, [string]$Role, [string]$Scope)
    $json = @{ claims = @{ sub = $Subject; role = $Role; scope = $Scope } } | ConvertTo-Json -Depth 5 -Compress
    $result = Invoke-RestMethod -Method Post -Uri "$GatewayUrl/faucet" -ContentType 'application/json' -Body $json
    if (-not $result.value) { throw 'The token faucet did not return a token.' }
    return [string]$result.value
}

function Send-Http {
    param(
        [string]$Method,
        [string]$Url,
        [string]$Token = '',
        [string]$Json = '',
        [string]$SpoofUserId = ''
    )

    $file = [System.IO.Path]::GetTempFileName()
    $bodyFile = $null
    try {
        $arguments = @('-sS', '--output', $file, '--write-out', '%{http_code}', '--request', $Method)
        if ($Token) { $arguments += @('--header', "Authorization: Bearer $Token") }
        if ($SpoofUserId) { $arguments += @('--header', "X-UserId: $SpoofUserId") }
        if ($Json) {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            [System.IO.File]::WriteAllText($bodyFile, $Json, (New-Object System.Text.UTF8Encoding($false)))
            $arguments += @('--header', 'Content-Type: application/json', '--data-binary', ('@' + $bodyFile))
        }
        $output = & curl.exe @arguments $Url
        if ($LASTEXITCODE -ne 0 -or "$output" -notmatch '^\d{3}$') {
            throw "Could not reach $Url. curl output: $output"
        }
        $body = Get-Content -LiteralPath $file -Raw
        return @{ Status = [int]$output; Body = [string]$body }
    }
    finally {
        Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
        if ($bodyFile) { Remove-Item -LiteralPath $bodyFile -ErrorAction SilentlyContinue }
    }
}

function Expect-Status {
    param([string]$Name, [int]$Expected, [hashtable]$Result)
    if ($Result.Status -ne $Expected) {
        throw "$Name failed: expected HTTP $Expected, received $($Result.Status). Response: $($Result.Body)"
    }
    Write-Host "PASS $Name ($Expected)" -ForegroundColor Green
}

Write-Host 'Checking User Profile routing and authorization through Ocelot'
$profileId = ([Guid]::NewGuid()).ToString()
$otherId = ([Guid]::NewGuid()).ToString()
$adminId = ([Guid]::NewGuid()).ToString()
$adminRead = New-DemoToken -Subject $adminId -Role 'admin' -Scope 'profile.read'
$adminWrite = New-DemoToken -Subject $adminId -Role 'admin' -Scope 'profile.write'
$adminOtherScope = New-DemoToken -Subject $adminId -Role 'admin' -Scope 'other.read'
$memberRead = New-DemoToken -Subject $profileId -Role 'member' -Scope 'profile.read'
$memberOtherScope = New-DemoToken -Subject $profileId -Role 'member' -Scope 'other.read'

Expect-Status 'Direct service is accessible' 200 (Send-Http -Method GET -Url "$ServiceUrl/api/user-profiles")
Expect-Status 'No JWT is rejected' 401 (Send-Http -Method GET -Url "$GatewayUrl/user-profiles")
Expect-Status 'Invalid JWT is rejected' 401 (Send-Http -Method GET -Url "$GatewayUrl/user-profiles" -Token 'not-a-jwt')
Expect-Status 'Wrong read scope is rejected' 403 (Send-Http -Method GET -Url "$GatewayUrl/user-profiles" -Token $adminOtherScope)
Expect-Status 'Wrong role is rejected' 403 (Send-Http -Method GET -Url "$GatewayUrl/user-profiles" -Token $memberRead)
Expect-Status 'Wrong write scope is rejected' 403 (Send-Http -Method POST -Url "$GatewayUrl/user-profiles" -Token $adminRead -Json '{"userId":"00000000-0000-0000-0000-000000000001","displayName":"No","bio":"No"}')
Expect-Status 'Member with wrong scope is rejected' 403 (Send-Http -Method GET -Url "$GatewayUrl/user-profiles/me" -Token $memberOtherScope)

$createJson = @{ userId = $profileId; displayName = 'Week 43 profile'; bio = 'Created by the local gateway test' } | ConvertTo-Json -Compress
$created = $false
try {
    Expect-Status 'Admin can create a profile' 201 (Send-Http -Method POST -Url "$GatewayUrl/user-profiles" -Token $adminWrite -Json $createJson)
    $created = $true

    $direct = Send-Http -Method GET -Url "$ServiceUrl/api/user-profiles/$profileId"
    $viaGateway = Send-Http -Method GET -Url "$GatewayUrl/user-profiles/$profileId" -Token $adminRead
    Expect-Status 'Direct profile lookup' 200 $direct
    Expect-Status 'Gateway profile lookup' 200 $viaGateway
    if ($direct.Body -ne $viaGateway.Body) {
        throw 'Direct and gateway responses differ for the same profile.'
    }
    Write-Host 'PASS Direct and gateway responses match' -ForegroundColor Green

    $me = Send-Http -Method GET -Url "$GatewayUrl/user-profiles/me" -Token $memberRead -SpoofUserId $otherId
    Expect-Status 'Member can read their own profile' 200 $me
    $meProfile = $me.Body | ConvertFrom-Json
    if ([string]$meProfile.userId -ne $profileId) {
        throw 'The self-profile route did not use sub from the JWT.'
    }
    Write-Host 'PASS /me resolves the validated JWT subject, not the forged X-UserId' -ForegroundColor Green

    $updateJson = @{ displayName = 'Week 43 updated'; bio = 'Updated via Ocelot' } | ConvertTo-Json -Compress
    Expect-Status 'Admin can update a profile' 200 (Send-Http -Method PUT -Url "$GatewayUrl/user-profiles/$profileId" -Token $adminWrite -Json $updateJson)
    Expect-Status 'Admin can list profiles' 200 (Send-Http -Method GET -Url "$GatewayUrl/user-profiles" -Token $adminRead)
}
finally {
    if ($created) {
        $deleted = Send-Http -Method DELETE -Url "$GatewayUrl/user-profiles/$profileId" -Token $adminWrite
        Expect-Status 'Admin can delete a profile' 204 $deleted
    }
}

Write-Host 'All local HTTP checks passed.' -ForegroundColor Green
