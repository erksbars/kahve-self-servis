$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskRuntime = Join-Path $taskRoot '.local/payment-tunnel'
$null = New-Item -ItemType Directory -Path $taskRuntime -Force
$taskBinary = Join-Path $taskRuntime 'cloudflared.exe'
$taskExpectedHash = '86AEE4017B26625CEE8484C113558F48EFFA4CD47F7AA05FCF425604E5D2B23C'
if (-not (Test-Path -LiteralPath $taskBinary)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/cloudflare/cloudflared/releases/download/2026.10.0/cloudflared-windows-amd64.exe' -OutFile $taskBinary
}
if ((Get-FileHash -LiteralPath $taskBinary -Algorithm SHA256).Hash -ne $taskExpectedHash) { throw 'cloudflared checksum mismatch. Nothing was started.' }
$taskStatePath = Join-Path $taskRuntime 'state.json'
if (Test-Path -LiteralPath $taskStatePath) {
    $taskPrevious = Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
    if (Get-Process -Id $taskPrevious.TunnelPid -ErrorAction SilentlyContinue) { throw 'An attached payment tunnel is already running. Use its saved URL or stop it first.' }
}
$taskProxyScript = Join-Path $taskRoot 'scripts/payment-callback-gateway.cjs'
$taskNode = (Get-Command node -ErrorAction Stop).Source
$taskProxy = Start-Process -FilePath $taskNode -ArgumentList ('"' + $taskProxyScript + '"') -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $taskRuntime 'gateway-error.log') -RedirectStandardOutput (Join-Path $taskRuntime 'gateway.log')
Start-Sleep -Milliseconds 700
if ($taskProxy.HasExited) { throw 'Callback gateway could not start. Check .local/payment-tunnel/gateway-error.log.' }
$taskHealth = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:5190/healthz' -TimeoutSec 3
if ($taskHealth.Content -notmatch 'Kahve payment callback gateway ready') { throw 'Callback gateway health check failed.' }
$taskLog = Join-Path $taskRuntime 'cloudflared.log'
$taskTunnel = Start-Process -FilePath $taskBinary -ArgumentList 'tunnel --url http://127.0.0.1:5190 --no-autoupdate --protocol http2' -WindowStyle Hidden -PassThru -RedirectStandardError $taskLog -RedirectStandardOutput (Join-Path $taskRuntime 'cloudflared-output.log')
$taskUrl = $null
for ($taskAttempt = 0; $taskAttempt -lt 45; $taskAttempt++) {
    Start-Sleep -Seconds 1
    if ($taskTunnel.HasExited) { throw 'cloudflared exited before creating a URL. Check the local tunnel log.' }
    $taskLogText = Get-Content -LiteralPath $taskLog -Raw -ErrorAction SilentlyContinue
    if ($taskLogText -match 'https://[a-z0-9-]+\.trycloudflare\.com') { $taskUrl = $Matches[0]; break }
}
if (-not $taskUrl) { throw 'Tunnel URL was not received. Check .local/payment-tunnel/cloudflared.log.' }
$taskState = [PSCustomObject]@{ Url = $taskUrl; GatewayPid = $taskProxy.Id; TunnelPid = $taskTunnel.Id; StartedAt = [DateTimeOffset]::UtcNow.ToString('o'); Port = 5190 }
$taskState | ConvertTo-Json | Set-Content -LiteralPath $taskStatePath -Encoding UTF8
$taskEnvPath = Join-Path $taskRoot '.env'
if (Test-Path -LiteralPath $taskEnvPath) {
    $taskEnvLines = [System.IO.File]::ReadAllLines($taskEnvPath) | Where-Object { -not $_.StartsWith('IYZICO__CALLBACKORIGIN=') }
    [System.IO.File]::WriteAllLines($taskEnvPath, (@($taskEnvLines) + @('IYZICO__CALLBACKORIGIN=' + $taskUrl)), [System.Text.UTF8Encoding]::new($false))
}
Write-Output $taskUrl
Write-Output 'Only the payment callback gateway is public. Checkout integration is not yet enabled.'
