<#
.SYNOPSIS
    LeistungskatalogService fuer AuswertungPro (PS)
.DESCRIPTION
    Laedt und speichert Benutzer-Katalogpositionen (JSON) und bietet Lookup-Funktionen.
    UI-Texte bewusst ASCII (ue/ae/oe), um Encoding-Probleme in PS1 zu vermeiden.
#>

Set-StrictMode -Version Latest

$script:AwpCatalogFile = Join-Path $env:APPDATA "AuswertungPro\\leistungskatalog.json"

function Get-AwpCatalogPath {
    return $script:AwpCatalogFile
}

function Normalize-AwpKey {
    param([Parameter(Mandatory)][string]$text)
    $t = $text.ToLowerInvariant()
    $t = $t -replace '\s+', ' '
    $t = $t.Trim()
    $t = $t -replace '[^a-z0-9]', ''
    return $t
}

function Load-AwpUserCatalogItems {
    if (-not (Test-Path $script:AwpCatalogFile)) {
        return @()
    }
    try {
        $raw = Get-Content -Path $script:AwpCatalogFile -Raw -Encoding UTF8
        $items = $raw | ConvertFrom-Json
        if ($null -eq $items) { return @() }
        return @($items)
    } catch {
        return @()
    }
}

function Save-AwpUserCatalogItems {
    param([Parameter(Mandatory)]$items)
    $dir = Split-Path $script:AwpCatalogFile -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    ($items | ConvertTo-Json -Depth 10) | Set-Content -Path $script:AwpCatalogFile -Encoding UTF8
}

function Find-AwpCatalogEntry {
    param([Parameter(Mandatory)][string]$positionText)
    $key = Normalize-AwpKey $positionText
    if (-not $key) { return $null }

    $items = Load-AwpUserCatalogItems
    foreach ($item in $items) {
        if ($null -eq $item) { continue }
        if ($item.key -and (Normalize-AwpKey $item.key) -eq $key) { return $item }
        if ($item.name -and (Normalize-AwpKey $item.name) -eq $key) { return $item }
        if ($item.aliases) {
            foreach ($a in $item.aliases) {
                if ((Normalize-AwpKey $a) -eq $key) { return $item }
            }
        }
    }
    return $null
}

function Show-AwpLeistungskatalogDialog {
    Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase | Out-Null

    $items = Load-AwpUserCatalogItems
    $data = New-Object System.Collections.ObjectModel.ObservableCollection[PSObject]
    foreach ($i in $items) {
        $data.Add([PSCustomObject]@{
            key = $i.key
            name = $i.name
            unit = $i.unit
            unitPriceCHF = $i.unitPriceCHF
        }) | Out-Null
    }

    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Leistungskatalog" Height="520" Width="820"
        WindowStartupLocation="CenterOwner">
  <Grid Margin="10">
    <Grid.RowDefinitions>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    <DataGrid Name="GridCatalog" Grid.Row="0" AutoGenerateColumns="False" CanUserAddRows="True"
              CanUserDeleteRows="True" IsReadOnly="False">
      <DataGrid.Columns>
        <DataGridTextColumn Header="Key" Binding="{Binding key, UpdateSourceTrigger=PropertyChanged}" Width="200"/>
        <DataGridTextColumn Header="Name" Binding="{Binding name, UpdateSourceTrigger=PropertyChanged}" Width="*"/>
        <DataGridTextColumn Header="Einheit" Binding="{Binding unit, UpdateSourceTrigger=PropertyChanged}" Width="90"/>
        <DataGridTextColumn Header="Preis CHF" Binding="{Binding unitPriceCHF, UpdateSourceTrigger=PropertyChanged}" Width="110"/>
      </DataGrid.Columns>
    </DataGrid>
    <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
      <Button Name="BtnSave" Content="Speichern" Height="38" Width="140" Margin="0,0,8,0"/>
      <Button Name="BtnClose" Content="Schliessen" Height="38" Width="140"/>
    </StackPanel>
  </Grid>
</Window>
"@

    $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml)
    $win = [Windows.Markup.XamlReader]::Load($reader)
    $grid = $win.FindName("GridCatalog")
    $btnSave = $win.FindName("BtnSave")
    $btnClose = $win.FindName("BtnClose")

    $grid.ItemsSource = $data

    $btnSave.Add_Click({
        $list = @()
        foreach ($row in $data) {
            if ([string]::IsNullOrWhiteSpace($row.name)) { continue }
            $list += [pscustomobject]@{
                key = if ($row.key) { $row.key } else { Normalize-AwpKey $row.name }
                name = $row.name
                category = ""
                unit = $row.unit
                unitPriceCHF = [double]($row.unitPriceCHF -as [double])
                minCHF = 0
                roundToCHF = 0
                measures = @()
                aliases = @()
                active = $true
            }
        }
        Save-AwpUserCatalogItems -items $list
        [System.Windows.MessageBox]::Show("Katalog gespeichert.", "Leistungskatalog") | Out-Null
    })

    $btnClose.Add_Click({ $win.Close() })

    $null = $win.ShowDialog()
}

Write-Host "[LeistungskatalogService] Loaded" -ForegroundColor Green
