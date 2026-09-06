$ErrorActionPreference = 'Continue'
$auditRoot = 'C:\Sewer-Studio_KI_4.5\.tmp\programmaudit-2026-09-06'
$env:TEMP = Join-Path $auditRoot 'runtime-temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
$auditRuns = @()
foreach ($auditName in @('AuswertungPro.Next.Infrastructure.Tests', 'AuswertungPro.Next.Pipeline.Tests', 'AuswertungPro.Next.UI.Tests', 'ProjectModernizer.Tests')) {
    $auditStart = Get-Date
    $auditLog = Join-Path $auditRoot ($auditName + '.log')
    & dotnet test "tests/$auditName/$auditName.csproj" -c Release --no-build --no-restore --artifacts-path "$auditRoot/artifacts" '--collect:Code Coverage;Format=cobertura' --results-directory "$auditRoot/coverage/$auditName" --logger "trx;LogFileName=$auditName.trx" *> $auditLog
    $auditCode = $LASTEXITCODE
    $auditRuns += [pscustomobject]@{ Name = $auditName; ExitCode = $auditCode; Start = $auditStart.ToString('o'); Seconds = ((Get-Date)-$auditStart).TotalSeconds; Log = $auditLog }
    $auditRuns | ConvertTo-Json | Set-Content -LiteralPath "$auditRoot/dotnet-test-runs.json" -Encoding UTF8
    Get-Content -LiteralPath $auditLog -Tail 12
}
if (@($auditRuns | Where-Object ExitCode -ne 0).Count -gt 0) { exit 1 }
exit 0
