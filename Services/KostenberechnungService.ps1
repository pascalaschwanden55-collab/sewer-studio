
<#
KostenberechnungService.ps1
- Kostenberechnung Dialog + Massnahmen-Baukasten (Templates)
- Templates bearbeitbar und gespeichert unter %APPDATA%\AuswertungPro\calc_templates.json
- Doppelklick auf Massnahme uebernimmt NUR die Massnahmen (ApplyMode = "measures")
- Rechtsklick auf Massnahmenliste + Buttons: Neu/Bearbeiten/Duplizieren/Loeschen(Ausblenden)/Reset

Hinweis:
- Dieses Modul ist bewusst ASCII (ue/ae/oe), um Encoding-Probleme zu vermeiden.
- Fuer Preis-Auto-Fill wird (optional) LeistungskatalogService.ps1 verwendet.
#>

Set-StrictMode -Version Latest

# -----------------------------
# AppData
# -----------------------------
function Get-AwpCalcAppDataDir {
  if (Get-Command -Name Get-AwpAppDataDir -ErrorAction SilentlyContinue) {
    return (Get-AwpAppDataDir)
  }
  $dir = Join-Path $env:APPDATA "AuswertungPro"
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
  return $dir
}

$script:CalcTemplatesUserPath = Join-Path (Get-AwpCalcAppDataDir) "calc_templates.json"

# -----------------------------
# Default-Templates (Baukasten)
# qtyExpr: Zahl oder Ausdruck mit dn/len (z.B. "1", "len", "len/5")
# -----------------------------
$script:AwpCalcTemplatesDefault = @{
  "Nadelfilz Schlauchliner" = @(
    @{ enabled=$true;  group="Installation"; position="Installation HI-Anlage"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Installation"; position="Installation Manuelle Gruppe"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Installation"; position="Installation Kanalreinigungsfahrzeug"; unit="pl"; qtyExpr="1" }

    @{ enabled=$true;  group="Vorarbeiten"; position="TV Vorkontrolle"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Vorarbeiten"; position="Fraesen"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Vorarbeiten"; position="Haltung Einmessen"; unit="pl"; qtyExpr="1" }

    @{ enabled=$false; group="Liner"; position="Nadelfilz Schlauchliner bis 5m (pauschal)"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Liner"; position="Nadelfilz Schlauchliner (liefern/impraegnieren/einbauen)"; unit="m"; qtyExpr="len" }

    @{ enabled=$true;  group="Qualitaet"; position="TV Abnahme"; unit="h"; qtyExpr="0" }
    @{ enabled=$true;  group="Qualitaet"; position="Dichtheitspruefung"; unit="St"; qtyExpr="0" }
    @{ enabled=$true;  group="Qualitaet"; position="Dokumentation/Datentraeger"; unit="St"; qtyExpr="1" }
  )

  # OPEN END: vollwertige Vorlage (wie Nadelfilz Schlauchliner)
  "Open End Nadelfilz Schlauchliner" = @(
    @{ enabled=$true;  group="Installation"; position="Installation HI-Anlage"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Installation"; position="Installation Manuelle Gruppe"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Installation"; position="Installation Kanalreinigungsfahrzeug"; unit="pl"; qtyExpr="1" }

    @{ enabled=$true;  group="Vorarbeiten"; position="TV Vorkontrolle"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Vorarbeiten"; position="Fraesen"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Vorarbeiten"; position="Haltung Einmessen"; unit="pl"; qtyExpr="1" }

    @{ enabled=$false; group="Liner"; position="Nadelfilz Schlauchliner bis 5m (pauschal)"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Liner"; position="Nadelfilz Schlauchliner (liefern/impraegnieren/einbauen)"; unit="m"; qtyExpr="len" }

    @{ enabled=$true;  group="Qualitaet"; position="TV Abnahme"; unit="h"; qtyExpr="0" }
    @{ enabled=$true;  group="Qualitaet"; position="Dichtheitspruefung"; unit="St"; qtyExpr="0" }
    @{ enabled=$true;  group="Qualitaet"; position="Dokumentation/Datentraeger"; unit="St"; qtyExpr="1" }
  )

  "GFK Schlauchliner (UV)" = @(
    @{ enabled=$true;  group="Installation"; position="Installation HI-Anlage"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Vorarbeiten"; position="TV Vorkontrolle"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Vorarbeiten"; position="Fraesen"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true;  group="Liner"; position="GFK Schlauchliner UV (liefern/einbauen)"; unit="m"; qtyExpr="len" }
    @{ enabled=$true;  group="Qualitaet"; position="TV Abnahme"; unit="h"; qtyExpr="0" }
    @{ enabled=$true;  group="Qualitaet"; position="Dichtheitspruefung"; unit="St"; qtyExpr="0" }
    @{ enabled=$true;  group="Qualitaet"; position="Dokumentation/Datentraeger"; unit="St"; qtyExpr="1" }
  )

  "Manschette" = @(
    @{ enabled=$true; group="Vorarbeiten"; position="TV Vorkontrolle"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true; group="Vorarbeiten"; position="Fraesen"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true; group="Reparatur"; position="Manschette liefern/einbauen"; unit="St"; qtyExpr="1" }
    @{ enabled=$true; group="Qualitaet"; position="TV Abnahme"; unit="h"; qtyExpr="0" }
    @{ enabled=$true; group="Qualitaet"; position="Dokumentation/Datentraeger"; unit="St"; qtyExpr="1" }
  )

  "Kurzliner (Partliner/Pointliner)" = @(
    @{ enabled=$true; group="Vorarbeiten"; position="TV Vorkontrolle"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true; group="Vorarbeiten"; position="Fraesen"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true; group="Reparatur"; position="Kurzliner liefern/einbauen"; unit="St"; qtyExpr="1" }
    @{ enabled=$true; group="Qualitaet"; position="TV Abnahme"; unit="h"; qtyExpr="0" }
    @{ enabled=$true; group="Qualitaet"; position="Dokumentation/Datentraeger"; unit="St"; qtyExpr="1" }
  )

  "Anschluss verpressen/einbinden" = @(
    @{ enabled=$true; group="Vorarbeiten"; position="TV Vorkontrolle"; unit="pl"; qtyExpr="1" }
    @{ enabled=$true; group="Reparatur"; position="Anschluss verpressen/einbinden"; unit="St"; qtyExpr="1" }
    @{ enabled=$true; group="Qualitaet"; position="Dokumentation/Datentraeger"; unit="St"; qtyExpr="1" }
  )
}

# -----------------------------
# User-Templates laden/speichern
# Format:
# {
#   version: 1,
#   templates: {
#     "Name": { disabled: false, items: [ {enabled,group,position,unit,qtyExpr}, ... ] }
#   }
# }
# -----------------------------
function Load-AwpUserCalcTemplates {
  if (-not (Test-Path $script:CalcTemplatesUserPath)) { return @{} }
  try {
    $raw = Get-Content $script:CalcTemplatesUserPath -Raw -Encoding UTF8
    $obj = $raw | ConvertFrom-Json
    $map = @{}
    if ($null -eq $obj.templates) { return @{} }

    foreach ($p in $obj.templates.PSObject.Properties) {
      $name = [string]$p.Name
      $val  = $p.Value

      # Backward compat: templates[name] = [array]
      if ($val -is [System.Collections.IEnumerable] -and -not ($val -is [pscustomobject])) {
        $map[$name] = @{ disabled=$false; items=@($val) }
      } else {
        $disabled = $false
        if ($null -ne $val.disabled) { $disabled = [bool]$val.disabled }
        $items = @()
        if ($null -ne $val.items) { $items = @($val.items) }
        $map[$name] = @{ disabled=$disabled; items=$items }
      }
    }
    return $map
  } catch {
    return @{}
  }
}

function Save-AwpUserCalcTemplates {
  param([Parameter(Mandatory)][hashtable]$templateMap)

  $payload = [pscustomobject]@{
    version   = 1
    templates = [pscustomobject]@{}
  }

  foreach ($k in ($templateMap.Keys | Sort-Object)) {
    $entry = $templateMap[$k]
    $disabled = $false
    if ($entry.ContainsKey('disabled')) { $disabled = [bool]$entry.disabled }
    $items = @()
    if ($entry.ContainsKey('items')) { $items = @($entry.items) }

    $itemsOut = @($items | ForEach-Object {
      [pscustomobject]@{
        enabled = [bool]$_.enabled
        group   = [string]$_.group
        position= [string]$_.position
        unit    = [string]$_.unit
        qtyExpr = [string]$_.qtyExpr
      }
    })

    $payload.templates | Add-Member -NotePropertyName $k -NotePropertyValue ([pscustomobject]@{
      disabled = $disabled
      items    = $itemsOut
    }) -Force
  }

  ($payload | ConvertTo-Json -Depth 10) | Set-Content -Path $script:CalcTemplatesUserPath -Encoding UTF8
}

function Clean-AwpCalcTemplateName {
  param([Parameter(Mandatory)][string]$name)
  return ($name -replace '\s*\(deaktiviert\)\s*$','').Trim()
}

function Get-AwpEffectiveCalcTemplateMap {
  $map = @{}

  foreach ($k in $script:AwpCalcTemplatesDefault.Keys) {
    $map[$k] = @{ disabled=$false; items=@($script:AwpCalcTemplatesDefault[$k]) }
  }

  $user = Load-AwpUserCalcTemplates
  foreach ($k in $user.Keys) {
    $entry = $user[$k]

    # Disabled: keep items if user provided, otherwise keep default items
    if ($entry.disabled) {
      if ($map.ContainsKey($k)) {
        $map[$k].disabled = $true
        if ($entry.items -and $entry.items.Count -gt 0) {
          $map[$k].items = @($entry.items)
        }
      } else {
        $map[$k] = @{ disabled=$true; items=@($entry.items) }
      }
      continue
    }

    # Enabled override
    $map[$k] = @{ disabled=$false; items=@($entry.items) }
  }

  return $map
}

function Test-AwpCalcTemplateDisabled {
  param([Parameter(Mandatory)][string]$name)
  $clean = Clean-AwpCalcTemplateName $name
  $map = Get-AwpEffectiveCalcTemplateMap
  if (-not $map.ContainsKey($clean)) { return $false }
  return [bool]$map[$clean].disabled
}

function Get-AwpCalcTemplateNames {
  $map = Get-AwpEffectiveCalcTemplateMap

  $active = @()
  $disabled = @()

  foreach ($k in $map.Keys) {
    if ([bool]$map[$k].disabled) {
      $disabled += ("{0} (deaktiviert)" -f $k)
    } else {
      $active += $k
    }
  }

  $active = @($active | Sort-Object)
  $disabled = @($disabled | Sort-Object)

  return @($active + $disabled)
}

function Get-AwpCalcTemplateItems {
  param(
    [Parameter(Mandatory)][string]$name,
    [switch]$IncludeDisabled
  )

  $map = Get-AwpEffectiveCalcTemplateMap
  $clean = Clean-AwpCalcTemplateName $name
  if (-not $map.ContainsKey($clean)) { return @() }

  if (-not $IncludeDisabled -and [bool]$map[$clean].disabled) {
    return @()
  }
  return @($map[$clean].items)
}

function Set-AwpCalcTemplate {
  param(
    [Parameter(Mandatory)][string]$name,
    [Parameter(Mandatory)][array]$items
  )

  $clean = Clean-AwpCalcTemplateName $name
  $user = Load-AwpUserCalcTemplates
  $user[$clean] = @{ disabled=$false; items=@($items) }
  Save-AwpUserCalcTemplates -templateMap $user
}

function Disable-AwpCalcTemplate {
  param([Parameter(Mandatory)][string]$name)

  $clean = Clean-AwpCalcTemplateName $name
  $map = Get-AwpEffectiveCalcTemplateMap
  $items = @()
  if ($map.ContainsKey($clean)) {
    $items = @($map[$clean].items)
  }

  $user = Load-AwpUserCalcTemplates
  $user[$clean] = @{ disabled=$true; items=@($items) }
  Save-AwpUserCalcTemplates -templateMap $user
}

function Remove-AwpCalcTemplateOverride {
  param([Parameter(Mandatory)][string]$name)

  $clean = Clean-AwpCalcTemplateName $name
  $user = Load-AwpUserCalcTemplates
  if ($user.ContainsKey($clean)) {
    $user.Remove($clean) | Out-Null
    Save-AwpUserCalcTemplates -templateMap $user
  }
}

# -----------------------------
# Hilfsfunktionen
# -----------------------------
function Try-Parse-AwpDouble2([string]$s, [ref]$out) {
  $out.Value = 0.0
  if ([string]::IsNullOrWhiteSpace($s)) { return $false }
  $t = $s.Trim() -replace ",","."
  return [double]::TryParse($t, [ref]$out.Value)
}

function Format-AwpMoney([double]$v) {
  return ($v.ToString("N2", [System.Globalization.CultureInfo]::InvariantCulture))
}

function Normalize-AwpKeyLocal([string]$s) {
  if (Get-Command -Name Normalize-AwpKey -ErrorAction SilentlyContinue) {
    return (Normalize-AwpKey $s)
  }
  if ([string]::IsNullOrWhiteSpace($s)) { return "" }
  $t = $s.Trim().ToLowerInvariant()
  $t = $t -replace "ä","ae" -replace "ö","oe" -replace "ü","ue" -replace "ß","ss"
  $t = $t -replace "[^a-z0-9]+","_"
  return $t.Trim("_")
}

function Get-AwpCatalogUnitPrice([string]$positionText) {
  if (-not (Get-Command -Name Find-AwpCatalogEntry -ErrorAction SilentlyContinue)) {
    return $null
  }
  $e = Find-AwpCatalogEntry -positionText $positionText
  if ($null -eq $e) { return $null }
  return [double]$e.unitPriceCHF
}

function New-AwpCalcDataTable {
  $dt = New-Object System.Data.DataTable

  $colEnabled = New-Object System.Data.DataColumn "Enabled", ([bool])
  $colEnabled.DefaultValue = $true
  $dt.Columns.Add($colEnabled) | Out-Null

  $dt.Columns.Add("Massnahme", [string]) | Out-Null
  $dt.Columns.Add("Gruppe", [string]) | Out-Null
  $dt.Columns.Add("Position", [string]) | Out-Null
  $dt.Columns.Add("Einheit", [string]) | Out-Null

  $cQty = New-Object System.Data.DataColumn "Menge", ([double])
  $cQty.DefaultValue = 0.0
  $dt.Columns.Add($cQty) | Out-Null

  $cPrice = New-Object System.Data.DataColumn "Preis", ([double])
  $cPrice.DefaultValue = 0.0
  $dt.Columns.Add($cPrice) | Out-Null

  $cCost = New-Object System.Data.DataColumn "Kosten", ([double])
  $cCost.Expression = "IIF(Enabled, Menge * Preis, 0)"
  $dt.Columns.Add($cCost) | Out-Null

  # hidden technical columns
  $dt.Columns.Add("QtyAuto", [bool])   | Out-Null
  $dt.Columns.Add("PriceAuto", [bool]) | Out-Null
  $dt.Columns.Add("QtyExpr", [string]) | Out-Null

  return $dt
}

function Eval-AwpQtyExpr {
  param(
    [Parameter(Mandatory)][string]$expr,
    [double]$dn,
    [double]$len
  )

  $e = ""
  if ($null -ne $expr) { $e = [string]$expr }
  $e = $e.Trim()
  if (-not $e) { return 0.0 }

  [double]$v=0
  if (Try-Parse-AwpDouble2 $e ([ref]$v)) { return $v }

  # allow only digits, operators, whitespace, parentheses, commas/dots, and dn/len tokens
  if ($e -notmatch '^[0-9\s\+\-\*\/\(\)\.,dDnNlLeE]+$') {
    return 0.0
  }

  $calc = $e.ToLowerInvariant() -replace ",","."
  $calc = $calc -replace "\bdn\b", ([string]([double]$dn).ToString([System.Globalization.CultureInfo]::InvariantCulture))
  $calc = $calc -replace "\blen\b", ([string]([double]$len).ToString([System.Globalization.CultureInfo]::InvariantCulture))

  try {
    $dt = New-Object System.Data.DataTable
    $o = $dt.Compute($calc, "")
    if ($o -eq $null -or $o -eq [DBNull]::Value) { return 0.0 }
    return [double]$o
  } catch {
    return 0.0
  }
}

function Update-AwpQuantitiesFromInputs {
  param(
    [Parameter(Mandatory)][System.Data.DataTable]$dt,
    [double]$dn,
    [double]$len
  )
  foreach ($r in $dt.Rows) {
    if (-not $r.QtyAuto) { continue }
    $expr = [string]$r.QtyExpr
    $r.Menge = [double](Eval-AwpQtyExpr -expr $expr -dn $dn -len $len)
  }
}

function Update-AwpPricesAndMissing {
  param(
    [Parameter(Mandatory)][System.Data.DataTable]$dt,
    [Parameter(Mandatory)][ref]$missingList
  )

  $missing = New-Object System.Collections.Generic.List[string]

  foreach ($r in $dt.Rows) {
    if (-not $r.PriceAuto) { continue }
    $pos = [string]$r.Position
    if ([string]::IsNullOrWhiteSpace($pos)) { continue }

    $p = Get-AwpCatalogUnitPrice -positionText $pos
    if ($null -eq $p) {
      $r.Preis = 0.0
      if (-not $missing.Contains($pos)) { $missing.Add($pos) }
    } else {
      $r.Preis = [double]$p
    }
  }

  $missingList.Value = $missing
}

function Build-AwpCostTableFromSelection {
  param(
    [string[]]$selectedMeasures,
    [double]$dn,
    [double]$len
  )

  $dt = New-AwpCalcDataTable

  foreach ($m in $selectedMeasures) {
    $items = Get-AwpCalcTemplateItems -name $m
    foreach ($li in $items) {
      $row = $dt.NewRow()
      $row.Enabled   = [bool]$li.enabled
      $row.Massnahme = [string]$m
      $row.Gruppe    = [string]$li.group
      $row.Position  = [string]$li.position
      $row.Einheit   = [string]$li.unit

      $row.QtyAuto   = $true
      $row.PriceAuto = $true
      $row.QtyExpr   = [string]$li.qtyExpr

      $dt.Rows.Add($row) | Out-Null
    }
  }

  Update-AwpQuantitiesFromInputs -dt $dt -dn $dn -len $len
  return $dt
}

function Merge-AwpCatalogPricesFromGrid {
  param([Parameter(Mandatory)][System.Data.DataTable]$dt)

  if (-not (Get-Command -Name Load-AwpUserCatalogItems -ErrorAction SilentlyContinue)) {
    return
  }

  $userItems = Load-AwpUserCatalogItems

  foreach ($r in $dt.Rows) {
    $pos = [string]$r.Position
    if ([string]::IsNullOrWhiteSpace($pos)) { continue }

    $price = [double]$r.Preis
    if ($price -le 0) { continue }

    $key = Normalize-AwpKeyLocal $pos
    $existing = $userItems | Where-Object { (Normalize-AwpKeyLocal $_.key) -eq $key } | Select-Object -First 1
    if ($null -eq $existing) {
      $existing = [pscustomobject]@{
        key=$key; name=$pos; category=""; unit=""; unitPriceCHF=$price; minCHF=0; roundToCHF=0
        measures=@(); aliases=@(); active=$true
      }
      $userItems += $existing
    } else {
      $existing.name = $pos
      $existing.unitPriceCHF = $price
      if (-not $existing.unit) { $existing.unit = [string]$r.Einheit }
    }
  }

  Save-AwpUserCatalogItems -items $userItems
}

function Get-AwpCalcTotalCHF {
  param([Parameter(Mandatory)][System.Data.DataTable]$dt)
  try {
    $o = $dt.Compute("Sum(Kosten)", "")
    if ($o -eq $null -or $o -eq [DBNull]::Value) { return 0.0 }
    return [double]$o
  } catch {
    return 0.0
  }
}

function Get-AwpSelectedActiveMeasuresFromListBox {
  param([Parameter(Mandatory)]$lbMeasures)

  $sel = @($lbMeasures.SelectedItems | ForEach-Object { [string]$_ })
  $clean = @($sel | ForEach-Object { Clean-AwpCalcTemplateName $_ })

  # filter disabled
  $active = @()
  foreach ($n in $clean) {
    if (-not (Test-AwpCalcTemplateDisabled -name $n)) {
      $active += $n
    }
  }

  # distinct keep order
  $seen = @{}
  $out = @()
  foreach ($n in $active) {
    if (-not $seen.ContainsKey($n)) {
      $seen[$n] = $true
      $out += $n
    }
  }
  return $out
}

# -----------------------------
# Template Editor (Baukasten)
# -----------------------------
function Show-AwpCalcTemplateEditorDialog {
  param(
    [Parameter(Mandatory)][string]$TemplateName,
    [Parameter(Mandatory)][array]$InitialItems
  )

  Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase | Out-Null

  $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Massnahme definieren" Height="520" Width="920"
        WindowStartupLocation="CenterOwner">
  <Grid Margin="12">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <DockPanel Grid.Row="0" LastChildFill="True" Margin="0,0,0,10">
      <TextBlock Name="TbTitle" DockPanel.Dock="Left" VerticalAlignment="Center" FontSize="16" FontWeight="SemiBold"
                 Text=""/>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
        <Button Name="BtnAdd" Content="Hinzufuegen" Width="110" Margin="0,0,8,0"/>
        <Button Name="BtnRemove" Content="Entfernen" Width="110" Margin="0,0,8,0"/>
        <Button Name="BtnUp" Content="Hoch" Width="80" Margin="0,0,8,0"/>
        <Button Name="BtnDown" Content="Runter" Width="80"/>
      </StackPanel>
    </DockPanel>

    <DataGrid Grid.Row="1" Name="Grid" AutoGenerateColumns="False" CanUserAddRows="False"
              IsReadOnly="False" SelectionMode="Single" SelectionUnit="FullRow">
      <DataGrid.Columns>
        <DataGridCheckBoxColumn Header="%" Binding="{Binding Enabled}" Width="40"/>
        <DataGridTextColumn Header="Gruppe" Binding="{Binding Gruppe}" Width="160"/>
        <DataGridTextColumn Header="Position" Binding="{Binding Position}" Width="360"/>
        <DataGridTextColumn Header="Einheit" Binding="{Binding Einheit}" Width="80"/>
        <DataGridTextColumn Header="Menge-Formel" Binding="{Binding QtyExpr}" Width="140"/>
      </DataGrid.Columns>
    </DataGrid>

    <DockPanel Grid.Row="2" Margin="0,10,0,0" LastChildFill="False">
      <TextBlock DockPanel.Dock="Left" VerticalAlignment="Center" Foreground="Gray"
                 Text="Menge-Formel: Zahl oder Ausdruck mit dn/len (z.B. 1, len, len/5)"/>
      <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
        <Button Name="BtnSave" Content="Speichern" Width="120" Margin="0,0,8,0"/>
        <Button Name="BtnCancel" Content="Abbrechen" Width="120"/>
      </StackPanel>
    </DockPanel>

  </Grid>
</Window>
"@

  $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml)
  $win = [Windows.Markup.XamlReader]::Load($reader)
  $grid = $win.FindName("Grid")
  $tbTitle = $win.FindName("TbTitle")

  $tbTitle.Text = "Massnahme: $TemplateName"

  # DataTable as binding source (easy reorder)
  $dt = New-Object System.Data.DataTable
  $dt.Columns.Add("Enabled", [bool])  | Out-Null
  $dt.Columns.Add("Gruppe", [string]) | Out-Null
  $dt.Columns.Add("Position", [string]) | Out-Null
  $dt.Columns.Add("Einheit", [string]) | Out-Null
  $dt.Columns.Add("QtyExpr", [string]) | Out-Null

  foreach ($it in $InitialItems) {
    $r = $dt.NewRow()
    $r.Enabled = [bool]$it.enabled
    $r.Gruppe  = [string]$it.group
    $r.Position= [string]$it.position
    $r.Einheit = [string]$it.unit
    $r.QtyExpr = [string]$it.qtyExpr
    $dt.Rows.Add($r) | Out-Null
  }

  $grid.ItemsSource = $dt.DefaultView

  $btnAdd = $win.FindName("BtnAdd")
  $btnRemove = $win.FindName("BtnRemove")
  $btnUp = $win.FindName("BtnUp")
  $btnDown = $win.FindName("BtnDown")
  $btnSave = $win.FindName("BtnSave")
  $btnCancel = $win.FindName("BtnCancel")

  $btnAdd.Add_Click({
    $r = $dt.NewRow()
    $r.Enabled = $true
    $r.Gruppe = ""
    $r.Position = ""
    $r.Einheit = "pl"
    $r.QtyExpr = "1"
    $dt.Rows.Add($r) | Out-Null
    $grid.SelectedIndex = $dt.Rows.Count - 1
    $grid.ScrollIntoView($grid.SelectedItem)
  })

  $btnRemove.Add_Click({
    if ($grid.SelectedItem -eq $null) { return }
    $drv = [System.Data.DataRowView]$grid.SelectedItem
    $dt.Rows.Remove($drv.Row)
  })

  $btnUp.Add_Click({
    if ($grid.SelectedItem -eq $null) { return }
    $idx = $grid.SelectedIndex
    if ($idx -le 0) { return }
    $row = $dt.Rows[$idx]
    $copy = $row.ItemArray
    $dt.Rows.RemoveAt($idx)
    $newRow = $dt.NewRow(); $newRow.ItemArray = $copy
    $dt.Rows.InsertAt($newRow, $idx-1)
    $grid.SelectedIndex = $idx-1
  })

  $btnDown.Add_Click({
    if ($grid.SelectedItem -eq $null) { return }
    $idx = $grid.SelectedIndex
    if ($idx -lt 0 -or $idx -ge ($dt.Rows.Count-1)) { return }
    $row = $dt.Rows[$idx]
    $copy = $row.ItemArray
    $dt.Rows.RemoveAt($idx)
    $newRow = $dt.NewRow(); $newRow.ItemArray = $copy
    $dt.Rows.InsertAt($newRow, $idx+1)
    $grid.SelectedIndex = $idx+1
  })

  $result = [pscustomobject]@{ Saved=$false; Items=@() }

  $btnSave.Add_Click({
    # validate
    foreach ($rr in $dt.Rows) {
      if ([string]::IsNullOrWhiteSpace([string]$rr.Position)) {
        [System.Windows.MessageBox]::Show("Eine Position ist leer. Bitte ausfuellen oder Zeile entfernen.", "Validierung") | Out-Null
        return
      }
      if ([string]::IsNullOrWhiteSpace([string]$rr.Gruppe)) {
        $rr.Gruppe = "Allgemein"
      }
      if ([string]::IsNullOrWhiteSpace([string]$rr.Einheit)) {
        $rr.Einheit = "pl"
      }
      if ([string]::IsNullOrWhiteSpace([string]$rr.QtyExpr)) {
        $rr.QtyExpr = "1"
      }
    }

    $out = @()
    foreach ($rr in $dt.Rows) {
      $out += [pscustomobject]@{
        enabled = [bool]$rr.Enabled
        group   = [string]$rr.Gruppe
        position= [string]$rr.Position
        unit    = [string]$rr.Einheit
        qtyExpr = [string]$rr.QtyExpr
      }
    }

    $result.Saved = $true
    $result.Items = $out
    $win.DialogResult = $true
    $win.Close()
  })

  $btnCancel.Add_Click({
    $win.DialogResult = $false
    $win.Close()
  })

  $win.ShowDialog() | Out-Null
  return $result
}

# -----------------------------
# Kostenberechnung Dialog
# -----------------------------
function Show-AwpKostenberechnungDialog {
  param(
    [string]$TitleSuffix = "",
    [string[]]$InitialSelectedMeasures = @(),
    [double]$InitialDN = 0.0,
    [double]$InitialLinerLength = 0.0
  )

  Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase | Out-Null
  Add-Type -AssemblyName Microsoft.VisualBasic | Out-Null

  $winTitle = "Kostenberechnung"
  if ($TitleSuffix) { $winTitle = "Kostenberechnung - $TitleSuffix" }

  $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="$winTitle" Height="720" Width="1180"
        WindowStartupLocation="CenterOwner">
  <Grid Margin="12">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="320"/>
      <ColumnDefinition Width="12"/>
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- LEFT: Massnahmen -->
    <GroupBox Grid.Column="0" Header="Empfohlene Massnahmen (Mehrfachauswahl)">
      <DockPanel>
        <StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="6,4,6,6">
          <Button Name="BtnTplNew" Content="Neu" Width="70" Margin="0,0,6,0"/>
          <Button Name="BtnTplEdit" Content="Bearbeiten" Width="100" Margin="0,0,6,0"/>
          <Button Name="BtnTplDup" Content="Duplizieren" Width="100" Margin="0,0,6,0"/>
          <Button Name="BtnTplDel" Content="Loeschen" Width="90" Margin="0,0,6,0"/>
          <Button Name="BtnTplReset" Content="Reset" Width="70"/>
        </StackPanel>
        <ListBox Name="LbMeasures" SelectionMode="Extended" Margin="6"/>
      </DockPanel>
    </GroupBox>

    <!-- RIGHT: Calculation -->
    <Grid Grid.Column="2">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>

      <TextBlock Grid.Row="0" Name="TbHeader" FontSize="18" FontWeight="SemiBold" Margin="0,0,0,6"/>

      <DockPanel Grid.Row="1" Margin="0,0,0,8" LastChildFill="True">
        <StackPanel Orientation="Horizontal" DockPanel.Dock="Left" VerticalAlignment="Center">
          <TextBlock Text="DN:" VerticalAlignment="Center" Margin="0,0,6,0"/>
          <TextBox Name="TbDN" Width="90" Margin="0,0,18,0"/>
          <TextBlock Text="Linerlaenge (m):" VerticalAlignment="Center" Margin="0,0,6,0"/>
          <TextBox Name="TbLen" Width="110"/>
        </StackPanel>
        <TextBlock Name="TbMissing" Foreground="Red" TextWrapping="Wrap"/>
      </DockPanel>

      <DataGrid Grid.Row="2" Name="Grid" AutoGenerateColumns="False" CanUserAddRows="False"
                IsReadOnly="False" SelectionMode="Single" SelectionUnit="FullRow">
        <DataGrid.Columns>
          <DataGridCheckBoxColumn Header="%" Binding="{Binding Enabled}" Width="40"/>
          <DataGridTextColumn Header="Gruppe" Binding="{Binding Gruppe}" Width="170" IsReadOnly="False"/>
          <DataGridTextColumn Header="Position" Binding="{Binding Position}" Width="420" IsReadOnly="False"/>
          <DataGridTextColumn Header="Einheit" Binding="{Binding Einheit}" Width="70" IsReadOnly="False"/>
          <DataGridTextColumn Header="Menge" Binding="{Binding Menge}" Width="90"/>
          <DataGridTextColumn Header="Preis" Binding="{Binding Preis}" Width="90"/>
          <DataGridTextColumn Header="Kosten" Binding="{Binding Kosten}" Width="110" IsReadOnly="True"/>
        </DataGrid.Columns>
      </DataGrid>

      <DockPanel Grid.Row="3" Margin="0,10,0,0" LastChildFill="False">
        <TextBlock Text="Total:" FontWeight="SemiBold"/>
        <TextBlock Name="TbTotal" FontWeight="SemiBold" Margin="8,0,0,0"/>
      </DockPanel>

      <DockPanel Grid.Row="4" Margin="0,14,0,0" LastChildFill="False">
        <StackPanel DockPanel.Dock="Left" Orientation="Horizontal">
          <Button Name="BtnApplyMeasures" Content="Massnahmen uebernehmen" Height="48" Width="260" Margin="0,0,10,0"/>
        </StackPanel>
        <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
          <Button Name="BtnSave" Content="Speichern" Height="48" Width="180" Margin="0,0,10,0"/>
          <Button Name="BtnClose" Content="Schliessen" Height="48" Width="180"/>
        </StackPanel>
      </DockPanel>

    </Grid>

  </Grid>
</Window>
"@

  $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml)
  $win = [Windows.Markup.XamlReader]::Load($reader)

  $lbMeasures = $win.FindName("LbMeasures")
  $tbHeader   = $win.FindName("TbHeader")
  $tbDN       = $win.FindName("TbDN")
  $tbLen      = $win.FindName("TbLen")
  $tbMissing  = $win.FindName("TbMissing")
  $grid       = $win.FindName("Grid")
  $tbTotal    = $win.FindName("TbTotal")

  $btnApplyMeasures = $win.FindName("BtnApplyMeasures")
  $btnSave          = $win.FindName("BtnSave")
  $btnClose         = $win.FindName("BtnClose")
  $btnTplNew        = $win.FindName("BtnTplNew")
  $btnTplEdit       = $win.FindName("BtnTplEdit")
  $btnTplDup        = $win.FindName("BtnTplDup")
  $btnTplDel        = $win.FindName("BtnTplDel")
  $btnTplReset      = $win.FindName("BtnTplReset")

  # Header content: show first selected measure name
  $tbHeader.Text = ""

  function Refresh-MeasureList {
    param([string[]]$reselect = @())
    $namesNow = Get-AwpCalcTemplateNames
    $lbMeasures.ItemsSource = $namesNow

    if ($reselect -and $reselect.Count -gt 0) {
      foreach ($m in $reselect) {
        $idx = [Array]::IndexOf($namesNow, $m)
        if ($idx -lt 0) {
          $idx = [Array]::IndexOf($namesNow, ("{0} (deaktiviert)" -f $m))
        }
        if ($idx -ge 0) {
          $lbMeasures.SelectedItems.Add($namesNow[$idx]) | Out-Null
        }
      }
    }
  }

  Refresh-MeasureList

  # Preselect
  if ($InitialSelectedMeasures -and $InitialSelectedMeasures.Count -gt 0) {
    Refresh-MeasureList -reselect @($InitialSelectedMeasures | ForEach-Object { Clean-AwpCalcTemplateName $_ })
  }

  $tbDN.Text  = if ($InitialDN -gt 0) { [string]$InitialDN } else { "" }
  $tbLen.Text = if ($InitialLinerLength -gt 0) { [string]$InitialLinerLength } else { "" }

  $script:__awpCalc_dt = $null

  function Refresh-AwpCalcFromUI {
    # selected measures (active only)
    $sel = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures

    $tbHeader.Text = if ($sel.Count -gt 0) { $sel[0] } else { "" }

    [double]$dn=0; [double]$len=0
    Try-Parse-AwpDouble2 $tbDN.Text ([ref]$dn) | Out-Null
    Try-Parse-AwpDouble2 $tbLen.Text ([ref]$len) | Out-Null

    $dt = Build-AwpCostTableFromSelection -selectedMeasures $sel -dn $dn -len $len

    $missing = $null
    Update-AwpPricesAndMissing -dt $dt -missingList ([ref]$missing)

    if ($missing -and $missing.Count -gt 0) {
      $tbMissing.Text = "Preis nicht gefunden fuer: " + ($missing -join ", ")
    } else {
      $tbMissing.Text = ""
    }

    $script:__awpCalc_dt = $dt
    $grid.ItemsSource = $dt.DefaultView

    $tot = Get-AwpCalcTotalCHF -dt $dt
    $tbTotal.Text = (Format-AwpMoney $tot)
  }

  function Update-AwpTotalsOnly {
    if ($null -eq $script:__awpCalc_dt) { $tbTotal.Text = (Format-AwpMoney 0); return }
    $tot = Get-AwpCalcTotalCHF -dt $script:__awpCalc_dt
    $tbTotal.Text = (Format-AwpMoney $tot)
  }

  # --- Grid context menu (optional but very helpful) ---
  $cmGrid = New-Object System.Windows.Controls.ContextMenu

  $miAddRow = New-Object System.Windows.Controls.MenuItem
  $miAddRow.Header = "Zusaetzliche Position hinzufuegen..."
  $miAddRow.Add_Click({
    if ($null -eq $script:__awpCalc_dt) { return }

    $pos = [Microsoft.VisualBasic.Interaction]::InputBox("Position (Text) eingeben", "Position hinzufuegen", "")
    if ([string]::IsNullOrWhiteSpace($pos)) { return }

    $grp = [Microsoft.VisualBasic.Interaction]::InputBox("Gruppe (optional)", "Position hinzufuegen", "Allgemein")
    if ([string]::IsNullOrWhiteSpace($grp)) { $grp = "Allgemein" }

    $unit = [Microsoft.VisualBasic.Interaction]::InputBox("Einheit (pl/m/h/St)", "Position hinzufuegen", "pl")
    if ([string]::IsNullOrWhiteSpace($unit)) { $unit = "pl" }

    $qtyS = [Microsoft.VisualBasic.Interaction]::InputBox("Menge", "Position hinzufuegen", "1")
    [double]$qty=1; Try-Parse-AwpDouble2 $qtyS ([ref]$qty) | Out-Null

    $row = $script:__awpCalc_dt.NewRow()
    $row.Enabled = $true
    $row.Massnahme = "(manuell)"
    $row.Gruppe = $grp
    $row.Position = $pos
    $row.Einheit = $unit
    $row.Menge = $qty
    $row.QtyAuto = $false

    $p = Get-AwpCatalogUnitPrice -positionText $pos
    if ($null -eq $p) {
      $row.Preis = 0.0
      $row.PriceAuto = $false
      $tbMissing.Text = "Preis nicht gefunden fuer: $pos"
    } else {
      $row.Preis = [double]$p
      $row.PriceAuto = $true
    }

    $row.QtyExpr = ""

    $script:__awpCalc_dt.Rows.Add($row) | Out-Null
    Update-AwpTotalsOnly
  })
  $cmGrid.Items.Add($miAddRow) | Out-Null

  $miRemoveRow = New-Object System.Windows.Controls.MenuItem
  $miRemoveRow.Header = "Ausgewaehlte Position entfernen"
  $miRemoveRow.Add_Click({
    if ($null -eq $script:__awpCalc_dt) { return }
    if ($grid.SelectedItem -eq $null) { return }
    $drv = [System.Data.DataRowView]$grid.SelectedItem
    $script:__awpCalc_dt.Rows.Remove($drv.Row)
    Update-AwpTotalsOnly
  })
  $cmGrid.Items.Add($miRemoveRow) | Out-Null

  $miResetPriceAuto = New-Object System.Windows.Controls.MenuItem
  $miResetPriceAuto.Header = "Preis wieder automatisch (aus Katalog)"
  $miResetPriceAuto.Add_Click({
    if ($null -eq $script:__awpCalc_dt) { return }
    if ($grid.SelectedItem -eq $null) { return }
    $drv = [System.Data.DataRowView]$grid.SelectedItem
    $drv.Row.PriceAuto = $true
    $missing = $null
    Update-AwpPricesAndMissing -dt $script:__awpCalc_dt -missingList ([ref]$missing)
    if ($missing -and $missing.Count -gt 0) {
      $tbMissing.Text = "Preis nicht gefunden fuer: " + ($missing -join ", ")
    } else {
      $tbMissing.Text = ""
    }
    Update-AwpTotalsOnly
  })
  $cmGrid.Items.Add($miResetPriceAuto) | Out-Null

  $miResetQtyAuto = New-Object System.Windows.Controls.MenuItem
  $miResetQtyAuto.Header = "Menge wieder automatisch (Formel)"
  $miResetQtyAuto.Add_Click({
    if ($null -eq $script:__awpCalc_dt) { return }
    if ($grid.SelectedItem -eq $null) { return }
    $drv = [System.Data.DataRowView]$grid.SelectedItem
    $drv.Row.QtyAuto = $true

    [double]$dn=0; [double]$len=0
    Try-Parse-AwpDouble2 $tbDN.Text ([ref]$dn) | Out-Null
    Try-Parse-AwpDouble2 $tbLen.Text ([ref]$len) | Out-Null

    Update-AwpQuantitiesFromInputs -dt $script:__awpCalc_dt -dn $dn -len $len
    Update-AwpTotalsOnly
  })
  $cmGrid.Items.Add($miResetQtyAuto) | Out-Null

  $grid.ContextMenu = $cmGrid

  # --- Measures context menu (templates CRUD) ---
  $cmMeasures = New-Object System.Windows.Controls.ContextMenu

  $doNew = {
    $name = [Microsoft.VisualBasic.Interaction]::InputBox("Name der Massnahme", "Neue Massnahme", "")
    if ([string]::IsNullOrWhiteSpace($name)) { return }
    $name = Clean-AwpCalcTemplateName $name

    $editor = Show-AwpCalcTemplateEditorDialog -TemplateName $name -InitialItems @()
    if (-not $editor.Saved) { return }

    Set-AwpCalcTemplate -name $name -items $editor.Items

    $prev = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    Refresh-MeasureList -reselect (@($prev) + @($name))
  }

  $doEdit = {
    if ($lbMeasures.SelectedItem -eq $null) { return }
    $name = Clean-AwpCalcTemplateName ([string]$lbMeasures.SelectedItem)

    $items = Get-AwpCalcTemplateItems -name $name -IncludeDisabled
    $editor = Show-AwpCalcTemplateEditorDialog -TemplateName $name -InitialItems $items
    if (-not $editor.Saved) { return }

    Set-AwpCalcTemplate -name $name -items $editor.Items

    $prev = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    Refresh-MeasureList -reselect $prev
    Refresh-AwpCalcFromUI
  }

  $doDup = {
    if ($lbMeasures.SelectedItem -eq $null) { return }
    $src = Clean-AwpCalcTemplateName ([string]$lbMeasures.SelectedItem)
    $dst = [Microsoft.VisualBasic.Interaction]::InputBox("Neuer Name", "Duplizieren", ($src + " (Kopie)"))
    if ([string]::IsNullOrWhiteSpace($dst)) { return }
    $dst = Clean-AwpCalcTemplateName $dst

    $items = Get-AwpCalcTemplateItems -name $src -IncludeDisabled
    Set-AwpCalcTemplate -name $dst -items $items

    $prev = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    Refresh-MeasureList -reselect (@($prev) + @($dst))
  }

  $doDel = {
    if ($lbMeasures.SelectedItem -eq $null) { return }
    $name = Clean-AwpCalcTemplateName ([string]$lbMeasures.SelectedItem)
    Disable-AwpCalcTemplate -name $name

    $prev = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    Refresh-MeasureList -reselect $prev
    Refresh-AwpCalcFromUI
  }

  $doReset = {
    if ($lbMeasures.SelectedItem -eq $null) { return }
    $name = Clean-AwpCalcTemplateName ([string]$lbMeasures.SelectedItem)
    Remove-AwpCalcTemplateOverride -name $name

    $prev = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    Refresh-MeasureList -reselect $prev
    Refresh-AwpCalcFromUI
  }

  $miNew = New-Object System.Windows.Controls.MenuItem
  $miNew.Header = "Neue Massnahme definieren..."
  $miNew.Add_Click($doNew)
  $cmMeasures.Items.Add($miNew) | Out-Null

  $miEdit = New-Object System.Windows.Controls.MenuItem
  $miEdit.Header = "Massnahme bearbeiten..."
  $miEdit.Add_Click($doEdit)
  $cmMeasures.Items.Add($miEdit) | Out-Null

  $miDup = New-Object System.Windows.Controls.MenuItem
  $miDup.Header = "Duplizieren..."
  $miDup.Add_Click($doDup)
  $cmMeasures.Items.Add($miDup) | Out-Null

  $miDel = New-Object System.Windows.Controls.MenuItem
  $miDel.Header = "Loeschen (ausblenden)"
  $miDel.Add_Click($doDel)
  $cmMeasures.Items.Add($miDel) | Out-Null

  $miReset = New-Object System.Windows.Controls.MenuItem
  $miReset.Header = "Override entfernen (zuruecksetzen)"
  $miReset.Add_Click($doReset)
  $cmMeasures.Items.Add($miReset) | Out-Null

  $lbMeasures.ContextMenu = $cmMeasures

  $btnTplNew.Add_Click($doNew)
  $btnTplEdit.Add_Click($doEdit)
  $btnTplDup.Add_Click($doDup)
  $btnTplDel.Add_Click($doDel)
  $btnTplReset.Add_Click($doReset)

  # --- Events ---
  $lbMeasures.Add_SelectionChanged({ Refresh-AwpCalcFromUI })

  # Doppelklick: NUR Massnahmen uebernehmen
  $lbMeasures.Add_MouseDoubleClick({
    $sel = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    if ($sel.Count -le 0) { return }
    $win.Tag = "apply_measures"
    $win.DialogResult = $true
    $win.Close()
  })

  $tbDN.Add_TextChanged({
    if ($null -eq $script:__awpCalc_dt) { return }
    [double]$dn=0; [double]$len=0
    Try-Parse-AwpDouble2 $tbDN.Text ([ref]$dn) | Out-Null
    Try-Parse-AwpDouble2 $tbLen.Text ([ref]$len) | Out-Null

    Update-AwpQuantitiesFromInputs -dt $script:__awpCalc_dt -dn $dn -len $len
    Update-AwpTotalsOnly
  })

  $tbLen.Add_TextChanged({
    if ($null -eq $script:__awpCalc_dt) { return }
    [double]$dn=0; [double]$len=0
    Try-Parse-AwpDouble2 $tbDN.Text ([ref]$dn) | Out-Null
    Try-Parse-AwpDouble2 $tbLen.Text ([ref]$len) | Out-Null

    Update-AwpQuantitiesFromInputs -dt $script:__awpCalc_dt -dn $dn -len $len
    Update-AwpTotalsOnly
  })

  # When user edits Menge/Preis, mark as manual for that row
  $grid.Add_CellEditEnding({
    if ($null -eq $script:__awpCalc_dt) { return }
    if ($grid.SelectedItem -eq $null) { return }
    $col = $_.Column.Header
    $drv = [System.Data.DataRowView]$grid.SelectedItem
    if ($col -eq "Menge") { $drv.Row.QtyAuto = $false }
    if ($col -eq "Preis") { $drv.Row.PriceAuto = $false }
  })

  $grid.Add_CurrentCellChanged({ Update-AwpTotalsOnly })

  # Buttons
  $result = [pscustomobject]@{
    Accepted = $false
    ApplyMode = "none"
    Measures = @()
    MeasuresText = ""
    TotalCHF = 0.0
  }

  $btnApplyMeasures.Add_Click({
    $sel = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
    if ($sel.Count -le 0) { return }
    $win.Tag = "apply_measures"
    $win.DialogResult = $true
    $win.Close()
  })

  $btnSave.Add_Click({
    if ($null -ne $script:__awpCalc_dt) {
      Merge-AwpCatalogPricesFromGrid -dt $script:__awpCalc_dt
      [System.Windows.MessageBox]::Show("Preise gespeichert (Leistungskatalog).", "Speichern") | Out-Null
      # refresh auto prices
      $missing = $null
      Update-AwpPricesAndMissing -dt $script:__awpCalc_dt -missingList ([ref]$missing)
      if ($missing -and $missing.Count -gt 0) {
        $tbMissing.Text = "Preis nicht gefunden fuer: " + ($missing -join ", ")
      } else {
        $tbMissing.Text = ""
      }
      Update-AwpTotalsOnly
    }
  })

  $btnClose.Add_Click({
    $win.DialogResult = $false
    $win.Close()
  })

  # initial refresh
  Refresh-AwpCalcFromUI

  $dlg = $win.ShowDialog()

  if ($dlg -ne $true) {
    return $result
  }

  $sel = Get-AwpSelectedActiveMeasuresFromListBox -lbMeasures $lbMeasures
  $result.Measures = $sel
  $result.MeasuresText = ($sel -join "; ")

  $dtFinal = $script:__awpCalc_dt
  if ($null -ne $dtFinal) {
    $result.TotalCHF = Get-AwpCalcTotalCHF -dt $dtFinal
  }

  if ($win.Tag -eq "apply_measures") {
    $result.Accepted = $true
    $result.ApplyMode = "measures"
  }

  return $result
}
