param(
  [Parameter(Mandatory)] [string]$SdfPath,
  [string]$OutDir = "C:\KI_BRAIN\sdf_converted"
)

$ErrorActionPreference = "Stop"
$dll = "C:\Program Files\Microsoft SQL Server Compact Edition\v4.0\Desktop\System.Data.SqlServerCe.dll"
Add-Type -Path $dll

# --- 1. Kopie in Temp, damit Post-Init nicht Original modifiziert ---
$tempDir = [System.IO.Path]::GetTempPath()
$workSdf = Join-Path $tempDir ("sdf_work_" + [Guid]::NewGuid().ToString("N") + ".sdf")
Copy-Item -LiteralPath $SdfPath -Destination $workSdf -Force

Write-Host "Quelle   : $SdfPath"
Write-Host "Arbeitskopie: $workSdf"

$conn = New-Object System.Data.SqlServerCe.SqlCeConnection "Data Source=$workSdf;"
$conn.Open()

# --- 2. Relevante WinCan-Tabellen fuer InspectionProfileExtractor ---
$tables = @(
  'SECTION','SECINSP','SECOBS','SECOBSMM','SECATTR','SECHIST','SECMES','SECMP','SECPX','SECREP','SECSOC','SECSTAT',
  'NODE','NODINSP','NODOBS','NODOBSMM','NODATTR','NODBLDG','NODENTRY','NODHIST','NODMES','NODMP','NODPART','NODPX','NODREP','NODSOC','NODSTAT',
  'PROJECT','PROJECTPX','PROJHIST','JOB','JOBREPORT','DOCU','DOCUX','POINT'
)

# --- 3. Pro Tabelle Schema + Daten als JSON exportieren ---
[System.IO.Directory]::CreateDirectory($OutDir) | Out-Null
$export = @{}
foreach ($t in $tables) {
  try {
    # Schema
    $cmdSchema = $conn.CreateCommand()
    $cmdSchema.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$t' ORDER BY ORDINAL_POSITION"
    $rdr = $cmdSchema.ExecuteReader()
    $cols = @()
    while ($rdr.Read()) {
      $cols += [pscustomobject]@{
        name = $rdr.GetString(0)
        type = $rdr.GetString(1)
        nullable = $rdr.GetString(2) -eq 'YES'
      }
    }
    $rdr.Close()
    if ($cols.Count -eq 0) { continue }

    # Daten
    $cmdData = $conn.CreateCommand()
    $colList = ($cols | ForEach-Object { "[$($_.name)]" }) -join ','
    $cmdData.CommandText = "SELECT $colList FROM [$t]"
    $rdr = $cmdData.ExecuteReader()
    $rows = New-Object System.Collections.ArrayList
    while ($rdr.Read()) {
      $row = @{}
      for ($i = 0; $i -lt $rdr.FieldCount; $i++) {
        if ($rdr.IsDBNull($i)) { $row[$cols[$i].name] = $null }
        else {
          $v = $rdr.GetValue($i)
          # Binary-Daten: als Base64
          if ($v -is [byte[]]) { $v = [Convert]::ToBase64String($v) + "::BLOB" }
          elseif ($v -is [datetime]) { $v = $v.ToString("o") }
          $row[$cols[$i].name] = $v
        }
      }
      [void]$rows.Add($row)
    }
    $rdr.Close()
    $export[$t] = @{ columns = $cols; rows = $rows }
    Write-Host ("  {0,-15} {1,6} rows" -f $t, $rows.Count)
  }
  catch {
    Write-Host "  $t : SKIP ($_)"
  }
}

# --- 4. JSON-Zwischenformat in Temp ---
$jsonPath = Join-Path $tempDir ("sdf_export_" + [Guid]::NewGuid().ToString("N") + ".json")
$export | ConvertTo-Json -Depth 10 -Compress | Set-Content -Path $jsonPath -Encoding UTF8
Write-Host ""
Write-Host "JSON-Export: $jsonPath"

$conn.Close()
Remove-Item $workSdf -Force -ErrorAction SilentlyContinue

# --- 5. Ausgabe fuer Python ---
$jsonPath
