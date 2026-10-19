$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskEnvPath = Join-Path $taskRoot '.env'
if (Test-Path -LiteralPath $taskEnvPath) {
    Write-Host '.env zaten var; mevcut ayarlar korunuyor.'
} else {
    $taskDbPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $taskDemoPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
    Set-Content -LiteralPath $taskEnvPath -Value "POSTGRES_PASSWORD=$taskDbPassword`nDEMO_PASSWORD=$taskDemoPassword`nAPI_PORT=5087`nWEB_PORT=5187" -Encoding utf8
}
$taskEnvText = Get-Content -Raw -LiteralPath $taskEnvPath
foreach ($taskSecretName in @('PAYMENT_WEBHOOK_SECRET', 'TRACKING_ENCRYPTION_KEY')) {
    if ($taskEnvText -notmatch "(?m)^$taskSecretName=") {
        $taskSecretValue = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        Add-Content -LiteralPath $taskEnvPath -Value "$taskSecretName=$taskSecretValue" -Encoding utf8
    }
}
Write-Host 'Yerel .env hazır. Parola ve anahtarlar bu dosyadadır; Git tarafından yok sayılır.'
