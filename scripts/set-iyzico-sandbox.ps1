param([Parameter(Mandatory=$true)][Guid]$TenantId)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskEnvPath = Join-Path $taskRoot '.env'
if (-not (Test-Path -LiteralPath $taskEnvPath)) { throw 'Create the local .env with scripts/init-env.ps1 first.' }
$taskApiSecure = Read-Host 'iyzico sandbox API Key' -AsSecureString
$taskSecretSecure = Read-Host 'iyzico sandbox Secret Key' -AsSecureString
$taskApiText = [System.Net.NetworkCredential]::new('', $taskApiSecure).Password
$taskSecretText = [System.Net.NetworkCredential]::new('', $taskSecretSecure).Password
try {
    if (-not $taskApiText.StartsWith('sandbox-') -or -not $taskSecretText.StartsWith('sandbox-') -or $taskApiText -match '[\r\n]' -or $taskSecretText -match '[\r\n]') { throw 'Only sandbox keys are accepted. No file was changed.' }
    $taskPrefix = 'IYZICO__MERCHANTS__' + $TenantId.ToString('N').ToUpperInvariant() + '__'
    $taskApiName = $taskPrefix + 'APIKEY'
    $taskSecretName = $taskPrefix + 'SECRETKEY'
    $taskLines = [System.IO.File]::ReadAllLines($taskEnvPath) | Where-Object { -not $_.StartsWith($taskApiName + '=') -and -not $_.StartsWith($taskSecretName + '=') }
    $taskUpdated = @($taskLines) + @(($taskApiName + '=' + $taskApiText), ($taskSecretName + '=' + $taskSecretText))
    [System.IO.File]::WriteAllLines($taskEnvPath, $taskUpdated, [System.Text.UTF8Encoding]::new($false))
    Write-Output 'Sandbox keys were saved to the ignored local .env. Demo payment mode was preserved. Restart the API to load the configuration.'
}
finally { $taskApiText = $null; $taskSecretText = $null; $taskApiSecure.Dispose(); $taskSecretSecure.Dispose() }
