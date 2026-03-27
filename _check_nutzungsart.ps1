# Use .NET JSON parser instead of PowerShell's ConvertFrom-Json to handle duplicate keys
Add-Type -AssemblyName System.Text.Json

$raw = [System.IO.File]::ReadAllText('E:\Zone 1.15\Altdorf_Zone_1.15.json')
$doc = [System.Text.Json.JsonDocument]::Parse($raw)
$root = $doc.RootElement

$data = $root.GetProperty("Data")
Write-Host "Gesamt Haltungen: $($data.GetArrayLength())"
Write-Host ""
Write-Host "=== NUTZUNGSART WERTE ==="

$usageMap = @{}
foreach($item in $data.EnumerateArray()) {
    $fields = $item.GetProperty("Fields")
    $haltung = ""
    $nutzung = ""
    if($fields.TryGetProperty("Haltungsname", [ref]$null)) { $haltung = $fields.GetProperty("Haltungsname").GetString() }
    if($fields.TryGetProperty("Nutzungsart", [ref]$null)) { $nutzung = $fields.GetProperty("Nutzungsart").GetString() }

    if(-not [string]::IsNullOrWhiteSpace($nutzung)) {
        $usageMap[$nutzung] = [int]($usageMap[$nutzung]) + 1
        # Show first 5 examples per value
        if([int]($usageMap[$nutzung]) -le 3) {
            Write-Host "  $haltung : '$nutzung'"
        }
    }
}

Write-Host ""
Write-Host "=== ZUSAMMENFASSUNG ==="
foreach($key in $usageMap.Keys | Sort-Object) {
    Write-Host "  '$key' : $($usageMap[$key])x"
}
