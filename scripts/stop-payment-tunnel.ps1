$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskStatePath = Join-Path $taskRoot '.local/payment-tunnel/state.json'
if (-not (Test-Path -LiteralPath $taskStatePath)) { Write-Output 'No saved payment tunnel.'; exit 0 }
$taskState = Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
$taskBinary = Join-Path $taskRoot '.local/payment-tunnel/cloudflared.exe'
$taskGateway = Join-Path $taskRoot 'scripts/payment-callback-gateway.cjs'
$taskTunnelProcess = Get-CimInstance Win32_Process -Filter ('ProcessId=' + [int]$taskState.TunnelPid)
if ($taskTunnelProcess) {
    if ($taskTunnelProcess.ExecutablePath -ne $taskBinary -or $taskTunnelProcess.CommandLine -notlike '*tunnel --url http://127.0.0.1:5190*') { throw 'Saved tunnel PID does not match this project. Nothing was stopped.' }
    Stop-Process -Id ([int]$taskState.TunnelPid)
}
$taskGatewayProcess = Get-CimInstance Win32_Process -Filter ('ProcessId=' + [int]$taskState.GatewayPid)
if ($taskGatewayProcess) {
    if ($taskGatewayProcess.Name -ne 'node.exe' -or -not $taskGatewayProcess.CommandLine.Contains($taskGateway)) { throw 'Saved gateway PID does not match this project. Gateway was not stopped.' }
    Stop-Process -Id ([int]$taskState.GatewayPid)
}
Write-Output 'This project payment tunnel and gateway were stopped. The temporary URL is no longer available.'
