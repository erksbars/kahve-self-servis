$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskValues = @{}
Get-Content -LiteralPath (Join-Path $taskRoot '.env') | ForEach-Object {
    if ($_ -match '^([A-Z0-9_]+)=(.*)$') { $taskValues[$Matches[1]] = $Matches[2].Trim() }
}
foreach ($taskConfigName in $taskValues.Keys) {
    if ($taskConfigName.StartsWith('IYZICO__MERCHANTS__')) { [Environment]::SetEnvironmentVariable($taskConfigName, $taskValues[$taskConfigName], 'Process') }
}
$env:ConnectionStrings__Database = "Host=localhost;Port=55432;Database=kahve;Username=kahve;Password=$($taskValues['POSTGRES_PASSWORD'])"
$env:Database__MigrateOnStartup = 'true'
$env:Demo__Enabled = 'true'
$env:Demo__Password = $taskValues['DEMO_PASSWORD']
$env:Sandbox__Enabled = 'true'
$env:Payments__Provider = if ($taskValues['PAYMENTS_PROVIDER']) { $taskValues['PAYMENTS_PROVIDER'] } else { 'sandbox' }
$env:Payments__WebhookSecret = $taskValues['PAYMENT_WEBHOOK_SECRET']
$env:Tracking__EncryptionKey = $taskValues['TRACKING_ENCRYPTION_KEY']
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$taskApiPort = if ($taskValues['API_PORT']) { $taskValues['API_PORT'] } else { '5087' }
dotnet run --project (Join-Path $taskRoot 'services/api') --no-launch-profile --urls "http://127.0.0.1:$taskApiPort"
exit $LASTEXITCODE
