param([string]$Stand = 'hauptbaum-bereinigt')
$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath 'C:\Sewer-Studio_KI_4.5'
$novaArtifacts = 'C:\Sewer-Studio_KI_4.5\.tmp\nova-abschluss\artifacts-hauptbaum'
dotnet build AuswertungPro.sln -c Release --no-restore --artifacts-path $novaArtifacts *> ".tmp/nova-abschluss/build-$Stand.log"
if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath ".tmp/nova-abschluss/build-$Stand.log" -Tail 20; exit 1 }
$novaJobs = foreach ($novaProject in @('AuswertungPro.Next.Infrastructure.Tests','AuswertungPro.Next.Pipeline.Tests','AuswertungPro.Next.UI.Tests','ProjectModernizer.Tests')) {
    Start-Job -ArgumentList $novaProject,$novaArtifacts,$Stand -ScriptBlock {
        param($name,$artifacts,$stand)
        Set-Location -LiteralPath 'C:\Sewer-Studio_KI_4.5'
        dotnet test "tests/$name/$name.csproj" -c Release --no-build --no-restore --artifacts-path $artifacts --logger "trx;LogFileName=$name.trx" --results-directory ".tmp/nova-abschluss/results-$stand" *> ".tmp/nova-abschluss/$name-$stand.log"
        [PSCustomObject]@{Project=$name;ExitCode=$LASTEXITCODE}
    }
}
$novaResults = $novaJobs | Wait-Job | Receive-Job
$novaResults | Select-Object Project,ExitCode | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath ".tmp/nova-abschluss/release-$Stand.json"
$novaResults | Select-Object Project,ExitCode
if (@($novaResults | Where-Object ExitCode -ne 0).Count) { exit 1 }
exit 0
