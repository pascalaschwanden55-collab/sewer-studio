<#
.SYNOPSIS
    Haltungs-Auswertungs-Tool
.DESCRIPTION
    Tool zur Erfassung und Auswertung von Haltungsdaten aus TV-Protokoll-PDFs.
    Jede Haltung wird als Block mit Feldern untereinander angezeigt.
#>

[CmdletBinding()]
param()

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

# ========== Konfiguration ==========
$script:AppVersion = "1.0.0"
$script:Rows = @{}  # Dictionary: RowId -> Hashtable mit Felddaten

# Feld-Definition (Reihenfolge wie im PDF "Anordnung Menü")
$script:FieldDefinitions = @(
    @{ Name = "NR"; Label = "NR."; Type = "Text"; Width = 60 }
    @{ Name = "Haltungsname"; Label = "Haltungsname (ID)"; Type = "Text"; Width = 150 }
    @{ Name = "Strasse"; Label = "Strasse"; Type = "Text"; Width = 200 }
    @{ Name = "Rohrmaterial"; Label = "Rohrmaterial"; Type = "Text"; Width = 150 }
    @{ Name = "DN_mm"; Label = "DN mm"; Type = "Text"; Width = 80 }
    @{ Name = "Nutzungsart"; Label = "Nutzungsart"; Type = "Text"; Width = 150 }
    @{ Name = "Haltungslaenge_m"; Label = "Haltungslänge m"; Type = "Text"; Width = 100 }
    @{ Name = "Fliessrichtung"; Label = "Fliessrichtung"; Type = "Text"; Width = 150 }
    @{ Name = "Primaere_Schaeden"; Label = "Primäre Schäden"; Type = "MultiLine"; Width = 300; Height = 80 }
    @{ Name = "Zustandsklasse"; Label = "Zustandsklasse"; Type = "Text"; Width = 80 }
    @{ Name = "Pruefungsresultat"; Label = "Prüfungsresultat"; Type = "Text"; Width = 150 }
    @{ Name = "Sanieren_JaNein"; Label = "Sanieren Ja/Nein"; Type = "ComboBox"; Items = @("", "Ja", "Nein"); Width = 80 }
    @{ Name = "Empfohlene_Sanierungsmassnahmen"; Label = "Empfohlene Sanierungsmassnahmen"; Type = "MultiLine"; Width = 300; Height = 60 }
    @{ Name = "Kosten"; Label = "Kosten"; Type = "Text"; Width = 100 }
    @{ Name = "Eigentuemer"; Label = "Eigentümer"; Type = "Text"; Width = 150 }
    @{ Name = "Bemerkungen"; Label = "Bemerkungen"; Type = "MultiLine"; Width = 300; Height = 60 }
    @{ Name = "Link"; Label = "Link"; Type = "Text"; Width = 300 }
    @{ Name = "Renovierung_Inliner_Stk"; Label = "Renovierung Inliner Stk."; Type = "Text"; Width = 80 }
    @{ Name = "Renovierung_Inliner_m"; Label = "Renovierung Inliner m"; Type = "Text"; Width = 80 }
    @{ Name = "Anschluesse_verpressen"; Label = "Anschlüsse verpressen"; Type = "Text"; Width = 80 }
)

# ========== PDF-Extraktion ==========
function Get-PdfToTextPath {
    $cmd = Get-Command -Name pdftotext -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    
    $root = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    $match = Get-ChildItem -Path $root -Recurse -Filter pdftotext.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($match) { return $match.FullName }
    
    # Suche in gängigen Pfaden
    $paths = @(
        "C:\Program Files\poppler\bin\pdftotext.exe",
        "C:\Program Files (x86)\poppler\bin\pdftotext.exe",
        "$env:USERPROFILE\scoop\apps\poppler\current\bin\pdftotext.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) { return $p }
    }
    
    return $null
}

function Convert-PdfToText {
    param([string]$PdfPath)
    
    $pdftotext = Get-PdfToTextPath
    if (-not $pdftotext) {
        throw "pdftotext.exe nicht gefunden. Bitte Poppler installieren (winget install poppler)."
    }
    
    $txtPath = Join-Path $env:TEMP ("pdf_extract_{0}.txt" -f [Guid]::NewGuid().ToString("N"))
    & $pdftotext -enc UTF-8 -layout $PdfPath $txtPath
    
    if (-not (Test-Path $txtPath)) {
        throw "PDF-Textextraktion fehlgeschlagen."
    }
    
    $content = Get-Content -Path $txtPath -Raw -Encoding UTF8
    Remove-Item -Path $txtPath -Force -ErrorAction SilentlyContinue
    return $content
}

# ========== PDF-Parsing ==========
function ConvertFrom-PdfProtokoll {
    param([string]$PdfPath)
    
    $text = Convert-PdfToText -PdfPath $PdfPath
    $lines = $text -split "`r?`n"
    
    # Finde alle Haltungen im PDF
    $haltungen = @()
    $currentHaltung = $null
    $currentBlock = @()
    
    foreach ($line in $lines) {
        # Neuer Haltungsblock erkannt
        $match = [regex]::Match($line, "^\s*Haltung\s+(?<id>\S+)")
        if ($match.Success) {
            if ($null -ne $currentHaltung) {
                $haltungen += @{
                    Id = $currentHaltung
                    Lines = $currentBlock
                }
            }
            $currentHaltung = $match.Groups["id"].Value
            $currentBlock = @($line)
        }
        elseif ($null -ne $currentHaltung) {
            $currentBlock += $line
        }
    }
    
    # Letzte Haltung hinzufügen
    if ($null -ne $currentHaltung) {
        $haltungen += @{
            Id = $currentHaltung
            Lines = $currentBlock
        }
    }
    
    # Falls keine Haltung gefunden, versuche alternatives Muster
    if ($haltungen.Count -eq 0) {
        $match = [regex]::Match($text, "Haltungsinspektion\s+-\s+\d{2}\.\d{2}\.\d{4}\s+-\s+(?<id>\S+)")
        if ($match.Success) {
            $haltungen += @{
                Id = $match.Groups["id"].Value
                Lines = $lines
            }
        }
    }
    
    return @{
        Haltungen = $haltungen
        FullText = $text
        Lines = $lines
    }
}

function Get-HaltungDataFromBlock {
    param(
        [string]$HaltungId,
        [string[]]$Lines,
        [string]$FullText
    )
    
    $data = @{
        Haltungsname = $HaltungId
        Strasse = ""
        Rohrmaterial = ""
        DN_mm = ""
        Nutzungsart = ""
        Haltungslaenge_m = ""
        Fliessrichtung = ""
        Primaere_Schaeden = ""
        Zustandsklasse = ""
    }
    
    $schaeden = @()
    $blockText = $Lines -join "`n"
    
    foreach ($line in $Lines) {
        $norm = $line -replace "[äÄ]", "ae" -replace "[öÖ]", "oe" -replace "[üÜ]", "ue" -replace "ß", "ss"
        
        # Strasse
        $m = [regex]::Match($line, "Stra(?:ss|ß)e[/\s]*Standort\s+(?<val>.+?)(?:\s{2,}|$)")
        if ($m.Success -and -not $data.Strasse) { 
            $data.Strasse = $m.Groups["val"].Value.Trim() 
        }
        
        # Rohrmaterial
        $m = [regex]::Match($line, "Material\s+(?<val>.+?)(?:\s{2,}|$)")
        if ($m.Success -and -not $data.Rohrmaterial) { 
            $data.Rohrmaterial = $m.Groups["val"].Value.Trim() 
        }
        
        # DN mm (Profilhöhe/Dimension)
        $m = [regex]::Match($line, "(?:Profilh[oö]he|Profilbreite|Dimension)\s*(?:\[mm\])?\s*(?<val>\d+)")
        if ($m.Success -and -not $data.DN_mm) { 
            $data.DN_mm = $m.Groups["val"].Value 
        }
        
        # Nutzungsart
        $m = [regex]::Match($line, "Nutzungsart\s+(?<val>.+?)(?:\s{2,}|$)")
        if ($m.Success -and -not $data.Nutzungsart) { 
            $data.Nutzungsart = $m.Groups["val"].Value.Trim() 
        }
        
        # Haltungslänge
        $m = [regex]::Match($line, "(?:Haltungsl[aä]nge|Inspektionsl[aä]nge|HL\s*\[m\])\s*(?<val>\d+[.,]?\d*)\s*m?")
        if ($m.Success -and -not $data.Haltungslaenge_m) { 
            $data.Haltungslaenge_m = ($m.Groups["val"].Value -replace ",", ".") 
        }
        
        # Fliessrichtung
        $m = [regex]::Match($line, "Inspektionsrichtung\s+(?<val>.+?)(?:\s{2,}|$)")
        if ($m.Success -and -not $data.Fliessrichtung) {
            $dir = $m.Groups["val"].Value.Trim()
            if ($dir -like "*In Flie*") { $data.Fliessrichtung = "In Fliessrichtung" }
            elseif ($dir -like "*Gegen Flie*") { $data.Fliessrichtung = "Gegen Fliessrichtung" }
            else { $data.Fliessrichtung = $dir }
        }
        
        # Schäden-Codes erkennen (z.B. "BCD Rohranfang", "BDB Beginn")
        $m = [regex]::Match($line, "^\s*(?:\d+\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?:\d+[.,]?\d*\s+)?(?<code>[A-Z]{2,3}(?:\.[A-Z])?)\s+(?<desc>.+?)(?:\s{2,}|$)")
        if ($m.Success) {
            $code = $m.Groups["code"].Value
            $desc = $m.Groups["desc"].Value.Trim()
            if ($code -match "^[A-Z]{2,3}$" -and $desc -and $desc.Length -gt 2) {
                $schaeden += "$code $desc"
            }
        }
    }
    
    # Schäden zusammenfassen (max. 10)
    if ($schaeden.Count -gt 0) {
        $data.Primaere_Schaeden = ($schaeden | Select-Object -First 10) -join "`r`n"
    }
    
    return $data
}

# ========== XAML UI Definition ==========
$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Haltungs-Auswertung" 
        Height="800" Width="900"
        WindowStartupLocation="CenterScreen"
        Background="#F5F5F5">
    <Window.Resources>
        <Style TargetType="Button" x:Key="ModernButton">
            <Setter Property="Background" Value="#0078D4"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="15,8"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#1084D9"/>
                </Trigger>
            </Style.Triggers>
        </Style>
        <Style TargetType="Button" x:Key="SecondaryButton">
            <Setter Property="Background" Value="#E0E0E0"/>
            <Setter Property="Foreground" Value="#333333"/>
            <Setter Property="Padding" Value="10,5"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
        <Style TargetType="Button" x:Key="DangerButton">
            <Setter Property="Background" Value="#D32F2F"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="8,4"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>
    </Window.Resources>
    
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <Border Grid.Row="0" Background="White" Padding="15" BorderBrush="#E0E0E0" BorderThickness="0,0,0,1">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                
                <StackPanel Grid.Column="0" Orientation="Horizontal">
                    <Button Name="btnAdd" Content="+ Neue Haltung" Style="{StaticResource ModernButton}" Margin="0,0,10,0"/>
                    <Button Name="btnExport" Content="Excel Export" Style="{StaticResource SecondaryButton}"/>
                </StackPanel>
                
                <TextBlock Grid.Column="2" Text="Haltungs-Auswertung v1.0" VerticalAlignment="Center" Foreground="#888"/>
            </Grid>
        </Border>
        
        <!-- Scrollbarer Bereich für Haltungs-Zeilen -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Padding="15">
            <StackPanel Name="rowContainer" Margin="0,0,15,0"/>
        </ScrollViewer>
        
        <!-- Footer -->
        <Border Grid.Row="2" Background="White" Padding="10" BorderBrush="#E0E0E0" BorderThickness="0,1,0,0">
            <TextBlock Name="lblStatus" Text="Bereit" Foreground="#666"/>
        </Border>
    </Grid>
</Window>
"@

# ========== UI Laden ==========
$reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]::new($xaml))
$window = [System.Windows.Markup.XamlReader]::Load($reader)

# Controls referenzieren
$btnAdd = $window.FindName("btnAdd")
$btnExport = $window.FindName("btnExport")
$rowContainer = $window.FindName("rowContainer")
$lblStatus = $window.FindName("lblStatus")

$script:RowCounter = 0

# ========== Haltungs-Zeile erstellen ==========
function New-HaltungRow {
    $script:RowCounter++
    $rowId = [Guid]::NewGuid().ToString()
    $rowNumber = $script:RowCounter
    
    # Daten-Container für diese Zeile
    $script:Rows[$rowId] = @{}
    
    # Border als Container
    $border = New-Object System.Windows.Controls.Border
    $border.Background = [System.Windows.Media.Brushes]::White
    $border.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 224, 224))
    $border.BorderThickness = [System.Windows.Thickness]::new(1)
    $border.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    $border.Padding = [System.Windows.Thickness]::new(15)
    $border.CornerRadius = [System.Windows.CornerRadius]::new(4)
    $border.Tag = $rowId
    
    $mainStack = New-Object System.Windows.Controls.StackPanel
    
    # Header mit Titel und Buttons
    $headerGrid = New-Object System.Windows.Controls.Grid
    $col1 = New-Object System.Windows.Controls.ColumnDefinition
    $col1.Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)
    $col2 = New-Object System.Windows.Controls.ColumnDefinition
    $col2.Width = [System.Windows.GridLength]::Auto
    $headerGrid.ColumnDefinitions.Add($col1)
    $headerGrid.ColumnDefinitions.Add($col2)
    
    $titleLabel = New-Object System.Windows.Controls.TextBlock
    $titleLabel.Text = "Haltung #$rowNumber"
    $titleLabel.FontSize = 16
    $titleLabel.FontWeight = [System.Windows.FontWeights]::Bold
    $titleLabel.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(51, 51, 51))
    $titleLabel.Name = "titleLabel"
    [System.Windows.Controls.Grid]::SetColumn($titleLabel, 0)
    
    $buttonPanel = New-Object System.Windows.Controls.StackPanel
    $buttonPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    [System.Windows.Controls.Grid]::SetColumn($buttonPanel, 1)
    
    # PDF Import Button
    $btnImport = New-Object System.Windows.Controls.Button
    $btnImport.Content = "PDF import"
    $btnImport.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0, 120, 212))
    $btnImport.Foreground = [System.Windows.Media.Brushes]::White
    $btnImport.Padding = [System.Windows.Thickness]::new(12, 6, 12, 6)
    $btnImport.BorderThickness = [System.Windows.Thickness]::new(0)
    $btnImport.Margin = [System.Windows.Thickness]::new(0, 0, 8, 0)
    $btnImport.Cursor = [System.Windows.Input.Cursors]::Hand
    $btnImport.Tag = $rowId
    
    # Löschen Button
    $btnDelete = New-Object System.Windows.Controls.Button
    $btnDelete.Content = "X"
    $btnDelete.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(211, 47, 47))
    $btnDelete.Foreground = [System.Windows.Media.Brushes]::White
    $btnDelete.Padding = [System.Windows.Thickness]::new(8, 4, 8, 4)
    $btnDelete.BorderThickness = [System.Windows.Thickness]::new(0)
    $btnDelete.Cursor = [System.Windows.Input.Cursors]::Hand
    $btnDelete.Tag = $rowId
    
    $buttonPanel.Children.Add($btnImport)
    $buttonPanel.Children.Add($btnDelete)
    
    $headerGrid.Children.Add($titleLabel)
    $headerGrid.Children.Add($buttonPanel)
    $mainStack.Children.Add($headerGrid)
    
    # Separator
    $sep = New-Object System.Windows.Controls.Separator
    $sep.Margin = [System.Windows.Thickness]::new(0, 10, 0, 10)
    $mainStack.Children.Add($sep)
    
    # Felder-Container (2-spaltig für bessere Platznutzung)
    $fieldsGrid = New-Object System.Windows.Controls.Grid
    $fieldsGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star) }))
    $fieldsGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star) }))
    
    $fieldControls = @{}
    $fieldIndex = 0
    
    foreach ($fieldDef in $script:FieldDefinitions) {
        $row = [Math]::Floor($fieldIndex / 2)
        $col = $fieldIndex % 2
        
        # Zeile hinzufügen falls nötig
        while ($fieldsGrid.RowDefinitions.Count -le $row) {
            $fieldsGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))
        }
        
        $fieldPanel = New-Object System.Windows.Controls.StackPanel
        $fieldPanel.Margin = [System.Windows.Thickness]::new(0, 0, 15, 8)
        [System.Windows.Controls.Grid]::SetRow($fieldPanel, $row)
        [System.Windows.Controls.Grid]::SetColumn($fieldPanel, $col)
        
        # Label
        $label = New-Object System.Windows.Controls.TextBlock
        $label.Text = $fieldDef.Label
        $label.FontSize = 11
        $label.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(100, 100, 100))
        $label.Margin = [System.Windows.Thickness]::new(0, 0, 0, 3)
        $fieldPanel.Children.Add($label)
        
        # Eingabefeld
        $control = $null
        switch ($fieldDef.Type) {
            "MultiLine" {
                $control = New-Object System.Windows.Controls.TextBox
                $control.AcceptsReturn = $true
                $control.TextWrapping = [System.Windows.TextWrapping]::Wrap
                $control.Height = $fieldDef.Height
                $control.VerticalScrollBarVisibility = [System.Windows.Controls.ScrollBarVisibility]::Auto
            }
            "ComboBox" {
                $control = New-Object System.Windows.Controls.ComboBox
                $control.IsEditable = $true
                foreach ($item in $fieldDef.Items) {
                    $control.Items.Add($item) | Out-Null
                }
            }
            default {
                $control = New-Object System.Windows.Controls.TextBox
            }
        }
        
        $control.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(200, 200, 200))
        $control.Padding = [System.Windows.Thickness]::new(6, 4, 6, 4)
        $control.Tag = @{ RowId = $rowId; FieldName = $fieldDef.Name }
        
        $fieldPanel.Children.Add($control)
        $fieldsGrid.Children.Add($fieldPanel)
        
        $fieldControls[$fieldDef.Name] = $control
        $fieldIndex++
    }
    
    $mainStack.Children.Add($fieldsGrid)
    $border.Child = $mainStack
    
    # Events
    $btnImport.Add_Click({
        param($sender, $e)
        $rowId = $sender.Tag
        Import-PdfForRow -RowId $rowId -FieldControls $fieldControls -TitleLabel $titleLabel
    }.GetNewClosure())
    
    $btnDelete.Add_Click({
        param($sender, $e)
        $rowId = $sender.Tag
        $result = [System.Windows.MessageBox]::Show("Haltung wirklich löschen?", "Bestätigung", [System.Windows.MessageBoxButton]::YesNo)
        if ($result -eq [System.Windows.MessageBoxResult]::Yes) {
            $toRemove = $null
            foreach ($child in $rowContainer.Children) {
                if ($child.Tag -eq $rowId) {
                    $toRemove = $child
                    break
                }
            }
            if ($toRemove) {
                $rowContainer.Children.Remove($toRemove)
                $script:Rows.Remove($rowId)
                $lblStatus.Text = "Haltung gelöscht"
            }
        }
    }.GetNewClosure())
    
    return @{
        Border = $border
        FieldControls = $fieldControls
        RowId = $rowId
        TitleLabel = $titleLabel
    }
}

# ========== PDF Import für eine Zeile ==========
function Import-PdfForRow {
    param(
        [string]$RowId,
        [hashtable]$FieldControls,
        $TitleLabel
    )
    
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "PDF Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*"
    $dialog.Title = "TV-Protokoll PDF auswählen"
    $dialog.InitialDirectory = "F:\AuswertungPro"
    
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return
    }
    
    try {
        $lblStatus.Text = "PDF wird analysiert..."
        $window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
        
        $result = ConvertFrom-PdfProtokoll -PdfPath $dialog.FileName
        
        if ($result.Haltungen.Count -eq 0) {
            [System.Windows.MessageBox]::Show("Keine Haltungen im PDF gefunden.", "Hinweis", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
            $lblStatus.Text = "Keine Haltungen gefunden"
            return
        }
        
        # Falls mehrere Haltungen: Auswahl-Dialog
        $selectedHaltung = $null
        if ($result.Haltungen.Count -eq 1) {
            $selectedHaltung = $result.Haltungen[0]
        }
        else {
            # Auswahl-Dialog
            $selectWindow = New-Object System.Windows.Window
            $selectWindow.Title = "Haltung auswählen"
            $selectWindow.Width = 400
            $selectWindow.Height = 300
            $selectWindow.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterOwner
            $selectWindow.Owner = $window
            
            $stack = New-Object System.Windows.Controls.StackPanel
            $stack.Margin = [System.Windows.Thickness]::new(15)
            
            $infoLabel = New-Object System.Windows.Controls.TextBlock
            $infoLabel.Text = "Das PDF enthält mehrere Haltungen. Bitte wählen Sie eine:"
            $infoLabel.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
            $stack.Children.Add($infoLabel)
            
            $listBox = New-Object System.Windows.Controls.ListBox
            $listBox.Height = 180
            foreach ($h in $result.Haltungen) {
                $listBox.Items.Add($h.Id) | Out-Null
            }
            $listBox.SelectedIndex = 0
            $stack.Children.Add($listBox)
            
            $btnOk = New-Object System.Windows.Controls.Button
            $btnOk.Content = "Auswählen"
            $btnOk.Margin = [System.Windows.Thickness]::new(0, 10, 0, 0)
            $btnOk.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
            $btnOk.Add_Click({
                $selectWindow.DialogResult = $true
                $selectWindow.Close()
            })
            $stack.Children.Add($btnOk)
            
            $selectWindow.Content = $stack
            
            if ($selectWindow.ShowDialog() -eq $true) {
                $selectedIndex = $listBox.SelectedIndex
                if ($selectedIndex -ge 0) {
                    $selectedHaltung = $result.Haltungen[$selectedIndex]
                }
            }
        }
        
        if ($null -eq $selectedHaltung) {
            $lblStatus.Text = "Import abgebrochen"
            return
        }
        
        # Daten extrahieren
        $data = Get-HaltungDataFromBlock -HaltungId $selectedHaltung.Id -Lines $selectedHaltung.Lines -FullText $result.FullText
        
        # Felder befüllen (NUR diese Zeile!)
        foreach ($fieldDef in $script:FieldDefinitions) {
            $fieldName = $fieldDef.Name
            if ($data.ContainsKey($fieldName) -and $data[$fieldName]) {
                $control = $FieldControls[$fieldName]
                if ($control -is [System.Windows.Controls.TextBox]) {
                    $control.Text = $data[$fieldName]
                }
                elseif ($control -is [System.Windows.Controls.ComboBox]) {
                    $control.Text = $data[$fieldName]
                }
            }
        }
        
        # Titel aktualisieren
        if ($data.Haltungsname) {
            $TitleLabel.Text = "Haltung: $($data.Haltungsname)"
        }
        
        $lblStatus.Text = "Import erfolgreich: $($data.Haltungsname)"
    }
    catch {
        [System.Windows.MessageBox]::Show("Fehler beim Import:`n$($_.Exception.Message)", "Fehler", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
        $lblStatus.Text = "Import fehlgeschlagen"
    }
}

# ========== Export ==========
function Export-ToExcel {
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Filter = "Excel Dateien (*.xlsx)|*.xlsx"
    $dialog.FileName = "Haltungen_$(Get-Date -Format 'yyyyMMdd').xlsx"
    $dialog.InitialDirectory = "F:\AuswertungPro"
    
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return
    }
    
    try {
        $excel = New-Object -ComObject Excel.Application
        $excel.Visible = $false
        $excel.DisplayAlerts = $false
        $wb = $excel.Workbooks.Add()
        $ws = $wb.Worksheets.Item(1)
        $ws.Name = "Haltungen"
        
        # Header
        $col = 1
        foreach ($fieldDef in $script:FieldDefinitions) {
            $ws.Cells.Item(1, $col) = $fieldDef.Label
            $ws.Cells.Item(1, $col).Font.Bold = $true
            $col++
        }
        
        # Daten aus den Zeilen sammeln
        $rowIndex = 2
        foreach ($child in $rowContainer.Children) {
            if ($child -is [System.Windows.Controls.Border]) {
                $rowId = $child.Tag
                $col = 1
                
                # Felder durchgehen
                $mainStack = $child.Child
                $fieldsGrid = $null
                foreach ($c in $mainStack.Children) {
                    if ($c -is [System.Windows.Controls.Grid] -and $c.RowDefinitions.Count -gt 0) {
                        $fieldsGrid = $c
                        break
                    }
                }
                
                if ($fieldsGrid) {
                    foreach ($fieldPanel in $fieldsGrid.Children) {
                        if ($fieldPanel -is [System.Windows.Controls.StackPanel]) {
                            foreach ($ctrl in $fieldPanel.Children) {
                                if ($ctrl -is [System.Windows.Controls.TextBox]) {
                                    $ws.Cells.Item($rowIndex, $col) = $ctrl.Text
                                    $col++
                                    break
                                }
                                elseif ($ctrl -is [System.Windows.Controls.ComboBox]) {
                                    $ws.Cells.Item($rowIndex, $col) = $ctrl.Text
                                    $col++
                                    break
                                }
                            }
                        }
                    }
                }
                $rowIndex++
            }
        }
        
        $ws.Columns.AutoFit() | Out-Null
        $wb.SaveAs($dialog.FileName)
        $wb.Close($false)
        $excel.Quit()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
        
        $lblStatus.Text = "Export erfolgreich: $($dialog.FileName)"
        [System.Windows.MessageBox]::Show("Export erfolgreich!", "Erfolg")
    }
    catch {
        [System.Windows.MessageBox]::Show("Export fehlgeschlagen:`n$($_.Exception.Message)", "Fehler", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
    }
}

# ========== Events ==========
$btnAdd.Add_Click({
    $newRow = New-HaltungRow
    $rowContainer.Children.Add($newRow.Border)
    $lblStatus.Text = "Neue Haltung hinzugefügt"
})

$btnExport.Add_Click({
    if ($rowContainer.Children.Count -eq 0) {
        [System.Windows.MessageBox]::Show("Keine Haltungen zum Exportieren.", "Hinweis")
        return
    }
    Export-ToExcel
})

# ========== Start ==========
# Erste leere Zeile hinzufügen
$initialRow = New-HaltungRow
$rowContainer.Children.Add($initialRow.Border)

$window.ShowDialog() | Out-Null
