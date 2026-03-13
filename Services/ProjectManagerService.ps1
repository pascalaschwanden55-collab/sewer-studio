<#
.SYNOPSIS
    ProjectManagerService - Projekt-Metadaten-Verwaltung + Auto-Extraktion
.DESCRIPTION
    - UI-Dialog fuer Projekt-Metadaten (Zone, Firma, Bearbeiter)
    - Metadaten-Extraktion aus XTF und PDF
    - Merge-Vorschau: Importierte Daten vs. bestehende
#>

# ========== Metadaten-Felder Definition ==========
$script:ProjectMetadataFields = @(
    @{ Name = 'Zone'; Label = 'Zone / Gebiet'; Type = 'text'; Tooltip = 'Z.B. GEP Altdorf Zone 6.19' }
    @{ Name = 'Gemeinde'; Label = 'Gemeinde'; Type = 'text'; Tooltip = 'Z.B. Buerglen UR' }
    @{ Name = 'Strasse'; Label = 'Strasse/Bereich'; Type = 'text'; Tooltip = 'Klausenstrasse, Nemuehleweg, etc.' }
    @{ Name = 'FirmaName'; Label = 'Firma Name'; Type = 'text'; Tooltip = 'Inspektionsfirma' }
    @{ Name = 'FirmaAdresse'; Label = 'Firma Adresse'; Type = 'text'; Tooltip = 'Strasse PLZ Ort' }
    @{ Name = 'FirmaTelefon'; Label = 'Telefon'; Type = 'text'; Tooltip = '' }
    @{ Name = 'FirmaEmail'; Label = 'E-Mail'; Type = 'text'; Tooltip = '' }
    @{ Name = 'Bearbeiter'; Label = 'Bearbeiter'; Type = 'text'; Tooltip = 'Verantwortlicher fuer Inspektion' }
    @{ Name = 'Auftraggeber'; Label = 'Auftraggeber'; Type = 'text'; Tooltip = 'Gemeinde, Kanton, Privat' }
    @{ Name = 'AuftragNr'; Label = 'Auftrag-Nr.'; Type = 'text'; Tooltip = 'Auftragsnummer' }
    @{ Name = 'InspektionsDatum'; Label = 'Inspektionsdatum'; Type = 'text'; Tooltip = 'TT.MM.JJJJ' }
)

# ========== Metadaten aus XTF extrahieren ==========
function Extract-MetadataFromXtf {
    param(
        [string] $XtfPath,
        [xml] $XmlContent = $null
    )
    
    $metadata = @{}
    
    try {
        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($XtfPath)
        
        # Gemeinde-Muster
        if ($fileName -match '(Buerglen|Altdorf|Erstfeld|Schattdorf|Attinghausen|Seedorf|Fluelen|Silenen|Gurtnellen|Andermatt|Wassen|Goeschenen|Hospental|Realp)[\s_]*(UR|Uri)?') {
            $metadata.Gemeinde = $matches[1]
            if ($matches[2]) { $metadata.Gemeinde += " $($matches[2])" }
        }
        
        # Zone-Muster
        if ($fileName -match '(?:Zone|Gep|GEP)[\s_-]*([\d.]+|[A-Za-z0-9]+)') {
            $metadata.Zone = "Zone $($matches[1])"
        }
        if ($fileName -match 'Gep[\s_]*([\w]+)') {
            $metadata.Zone = "GEP $($matches[1])"
        }
        
        # Strasse-Muster
        if ($fileName -match '(Klausenstrasse|Nemuehleweg|Gotthard|Schulhaus|Bahnhof)[\w]*') {
            $metadata.Strasse = $matches[0]
        }
        
        # Auftraggeber
        if ($fileName -match '\b(Kanton|Privat|Gemeinde)\b') {
            $metadata.Auftraggeber = $matches[1]
        }
        
        # Auftrag-Nr
        if ($fileName -match '^(\d{5,6})[-_]') {
            $metadata.AuftragNr = $matches[1]
        }
        if ($fileName -match '[-_](\d{5,6})[-_]') {
            if (-not $metadata.ContainsKey('AuftragNr')) {
                $metadata['AuftragNr'] = $matches[1]
            }
        }
        
        # Aus XML-Inhalt extrahieren
        if (-not $XmlContent -and (Test-Path $XtfPath)) {
            try {
                [xml]$XmlContent = Get-Content -Path $XtfPath -Encoding UTF8 -ErrorAction SilentlyContinue
            } catch {}
        }
        
        if ($XmlContent) {
            # HEADERSECTION -> SENDER
            $sender = $XmlContent.TRANSFER.HEADERSECTION.SENDER
            if ($sender -and $sender -notmatch '^(WinCan|IKAS)') {
                $metadata.FirmaName = $sender
            }
            
            # Untersuchung -> Ausfuehrender
            $ausfuehrender = $XmlContent.SelectNodes('.//Ausfuehrender') | 
                Where-Object { $_.InnerText -and $_.InnerText.Trim() } | 
                Select-Object -First 1
            if ($ausfuehrender) {
                $metadata.Bearbeiter = $ausfuehrender.InnerText.Trim()
            }
            
            # Untersuchung -> Zeitpunkt
            $zeitpunkt = $XmlContent.SelectNodes('.//Zeitpunkt') | 
                Where-Object { $_.InnerText -and $_.InnerText.Trim() } | 
                Select-Object -First 1
            if ($zeitpunkt) {
                $dateStr = $zeitpunkt.InnerText.Trim()
                if ($dateStr -match '(\d{4})(\d{2})(\d{2})') {
                    $metadata.InspektionsDatum = "$($matches[3]).$($matches[2]).$($matches[1])"
                } else {
                    $metadata.InspektionsDatum = $dateStr
                }
            }
        }
        
        Log-Info -Message "XTF Metadaten extrahiert: $($metadata.Count) Felder" -Context "ProjectManager"
    } catch {
        Log-Error -Message "Fehler bei XTF-Metadaten-Extraktion: $_" -Context "ProjectManager" -Exception $_
    }
    
    return $metadata
}

# ========== Metadaten aus PDF extrahieren ==========
function Extract-MetadataFromPdf {
    param(
        [string] $PdfText,
        [string] $PdfPath = ""
    )
    
    $metadata = @{}
    
    try {
        $metadataRegexes = @{
            'FirmaName' = @(
                '(?im)^\s*(Firma|Unternehmen|Auftragnehmer|Inspektionsfirma)\s*[:\-]?\s*(.+?)\s*$'
            )
            'FirmaAdresse' = @(
                '(?im)^\s*(Adresse|Anschrift|Sitz)\s*[:\-]?\s*(.+?)\s*$'
            )
            'FirmaTelefon' = @(
                '(?im)\b(Tel\.?|Telefon|Phone|Fon)\s*[:\-]?\s*([\d\s\+\-\(\)]{8,20})\b'
            )
            'FirmaEmail' = @(
                '\b([A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Z]{2,})\b'
            )
            'Bearbeiter' = @(
                '(?im)^\s*(Bearbeiter|Inspektor|Ausfuehrender|Pruefer|Techniker)\s*[:\-]?\s*(.+?)\s*$'
            )
            'Auftraggeber' = @(
                '(?im)^\s*(Auftraggeber|Bauherr|Kunde|Gemeinde|Kanton)\s*[:\-]?\s*(.+?)\s*$'
            )
            'AuftragNr' = @(
                '(?im)^\s*(Auftrags?[\s\-]*Nr\.?|Bestellnummer|Projekt[\s\-]*Nr\.?)\s*[:\-]?\s*([A-Z0-9\-]+)\b'
            )
            'InspektionsDatum' = @(
                '(?im)^\s*(Inspektionsdatum|Datum|Pruefdatum)\s*[:\-]?\s*(\d{1,2}[\./]\d{1,2}[\./]\d{2,4})\b'
            )
            'Zone' = @(
                '(?im)^\s*(Zone|Gebiet|Bereich|GEP)\s*[:\-]?\s*(.+?)\s*$'
            )
            'Gemeinde' = @(
                '(?im)^\s*(Gemeinde|Ort|Stadt|Ortschaft)\s*[:\-]?\s*(.+?)\s*$'
            )
            'Strasse' = @(
                '(?im)^\s*(Strasse|Standort|Lage)\s*[:\-]?\s*(.+?)\s*$'
            )
        }
        
        foreach ($field in $metadataRegexes.Keys) {
            foreach ($regex in $metadataRegexes[$field]) {
                if ($PdfText -match $regex) {
                    $value = $matches[$matches.Count - 1]
                    if ($value -and $value.Trim()) {
                        $metadata[$field] = $value.Trim()
                        break
                    }
                }
            }
        }
        
        # Aus Dateiname extrahieren
        if ($PdfPath) {
            $fileName = [System.IO.Path]::GetFileNameWithoutExtension($PdfPath)
            
            if (-not $metadata.Gemeinde -and $fileName -match '(Buerglen|Altdorf|Erstfeld|Schattdorf)[\s_]*(UR)?') {
                $metadata.Gemeinde = $matches[1]
            }
            
            if (-not $metadata.Zone -and $fileName -match '(?:Zone|Gep|GEP)[\s_-]*([\d.]+)') {
                $metadata.Zone = "Zone $($matches[1])"
            }
        }
        
        Log-Info -Message "PDF Metadaten extrahiert: $($metadata.Count) Felder" -Context "ProjectManager"
    } catch {
        Log-Error -Message "Fehler bei PDF-Metadaten-Extraktion: $_" -Context "ProjectManager" -Exception $_
    }
    
    return $metadata
}

# ========== Metadaten in Projekt mergen ==========
function Merge-ProjectMetadata {
    param(
        [Project] $Project,
        [hashtable] $ExtractedMetadata,
        [bool] $OverwriteEmpty = $true
    )
    
    $updatedCount = 0
    
    foreach ($field in $ExtractedMetadata.Keys) {
        $newValue = $ExtractedMetadata[$field]
        
        if ([string]::IsNullOrWhiteSpace($newValue)) {
            continue
        }
        
        if (-not $Project.Metadata.ContainsKey($field)) {
            $Project.Metadata[$field] = ""
        }
        
        $currentValue = $Project.Metadata[$field]
        
        if ($OverwriteEmpty) {
            if ([string]::IsNullOrWhiteSpace($currentValue)) {
                $Project.Metadata[$field] = $newValue
                $updatedCount++
                Log-Info -Message "Projekt-Metadaten: '$field' = '$newValue'" -Context "ProjectManager"
            }
        } else {
            if ($currentValue -ne $newValue) {
                $Project.Metadata[$field] = $newValue
                $updatedCount++
            }
        }
    }
    
    if ($updatedCount -gt 0) {
        $Project.ModifiedAt = (Get-Date).ToUniversalTime()
        $Project.Dirty = $true
    }
    
    return $updatedCount
}

# ========== Projekt-Manager-Dialog (UI) ==========
function Show-ProjectManagerDialog {
    param(
        [Project] $Project,
        [hashtable] $ExtractedMetadata = @{}
    )
    
    $dialog = New-Object System.Windows.Window
    $dialog.Title = "Projekt-Manager: $($Project.Name)"
    $dialog.Width = 550
    $dialog.Height = 650
    $dialog.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterOwner
    $dialog.ResizeMode = [System.Windows.ResizeMode]::NoResize
    
    $mainPanel = New-Object System.Windows.Controls.StackPanel
    $mainPanel.Margin = [System.Windows.Thickness]::new(20)
    
    # Header
    $header = New-Object System.Windows.Controls.TextBlock
    $header.Text = "Projekt-Metadaten"
    $header.FontSize = 18
    $header.FontWeight = [System.Windows.FontWeights]::Bold
    $header.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    $mainPanel.Children.Add($header)
    
    # Projekt-Name
    $namePanel = New-Object System.Windows.Controls.StackPanel
    $namePanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $namePanel.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    
    $nameLabel = New-Object System.Windows.Controls.TextBlock
    $nameLabel.Text = "Projektname:"
    $nameLabel.Width = 130
    $nameLabel.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    
    $nameTextBox = New-Object System.Windows.Controls.TextBox
    $nameTextBox.Text = $Project.Name
    $nameTextBox.Width = 350
    $nameTextBox.Padding = [System.Windows.Thickness]::new(5)
    
    $namePanel.Children.Add($nameLabel)
    $namePanel.Children.Add($nameTextBox)
    $mainPanel.Children.Add($namePanel)
    
    # Separator
    $separator = New-Object System.Windows.Controls.Separator
    $separator.Margin = [System.Windows.Thickness]::new(0, 5, 0, 15)
    $mainPanel.Children.Add($separator)
    
    # ScrollViewer
    $scrollViewer = New-Object System.Windows.Controls.ScrollViewer
    $scrollViewer.Height = 400
    $scrollViewer.VerticalScrollBarVisibility = [System.Windows.Controls.ScrollBarVisibility]::Auto
    
    $fieldsPanel = New-Object System.Windows.Controls.StackPanel
    
    $textBoxes = @{}
    
    foreach ($fieldDef in $script:ProjectMetadataFields) {
        $fieldName = $fieldDef.Name
        $fieldLabel = $fieldDef.Label
        
        $fieldPanel = New-Object System.Windows.Controls.StackPanel
        $fieldPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
        $fieldPanel.Margin = [System.Windows.Thickness]::new(0, 0, 0, 8)
        
        $label = New-Object System.Windows.Controls.TextBlock
        $label.Text = "${fieldLabel}:"
        $label.Width = 130
        $label.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
        if ($fieldDef.Tooltip) {
            $label.ToolTip = $fieldDef.Tooltip
        }
        
        $textBox = New-Object System.Windows.Controls.TextBox
        $textBox.Width = 280
        $textBox.Padding = [System.Windows.Thickness]::new(5)
        
        $currentValue = ""
        if ($Project.Metadata.ContainsKey($fieldName)) {
            $currentValue = $Project.Metadata[$fieldName]
        }
        $textBox.Text = $currentValue
        
        # Vorschlag anzeigen wenn leer
        if ($ExtractedMetadata.ContainsKey($fieldName) -and [string]::IsNullOrWhiteSpace($currentValue)) {
            $suggestedValue = $ExtractedMetadata[$fieldName]
            if ($suggestedValue) {
                $textBox.Text = $suggestedValue
                $textBox.Foreground = [System.Windows.Media.Brushes]::Gray
                $textBox.FontStyle = [System.Windows.FontStyles]::Italic
                $textBox.Tag = @{ IsSuggested = $true; SuggestedValue = $suggestedValue }
                
                $textBox.Add_GotFocus({
                    param($sender, $e)
                    if ($sender.Tag.IsSuggested) {
                        $sender.Foreground = [System.Windows.Media.Brushes]::Black
                        $sender.FontStyle = [System.Windows.FontStyles]::Normal
                        $sender.SelectAll()
                    }
                })
            }
        }
        
        $textBoxes[$fieldName] = $textBox
        
        $fieldPanel.Children.Add($label)
        $fieldPanel.Children.Add($textBox)
        $fieldsPanel.Children.Add($fieldPanel)
    }
    
    $scrollViewer.Content = $fieldsPanel
    $mainPanel.Children.Add($scrollViewer)
    
    # Button-Panel
    $buttonPanel = New-Object System.Windows.Controls.StackPanel
    $buttonPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $buttonPanel.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    $buttonPanel.Margin = [System.Windows.Thickness]::new(0, 20, 0, 0)
    
    $btnCancel = New-Object System.Windows.Controls.Button
    $btnCancel.Content = "Abbrechen"
    $btnCancel.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
    $btnCancel.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    $btnCancel.Add_Click({
        $dialog.DialogResult = $false
        $dialog.Close()
    })
    
    $btnSave = New-Object System.Windows.Controls.Button
    $btnSave.Content = "Speichern"
    $btnSave.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
    $btnSave.FontWeight = [System.Windows.FontWeights]::Bold
    $btnSave.Add_Click({
        $Project.Name = $nameTextBox.Text
        
        foreach ($fieldName in $textBoxes.Keys) {
            $tb = $textBoxes[$fieldName]
            $value = $tb.Text
            $Project.Metadata[$fieldName] = $value
        }
        
        $Project.ModifiedAt = (Get-Date).ToUniversalTime()
        $Project.Dirty = $true
        
        $dialog.DialogResult = $true
        $dialog.Close()
    })
    
    $btnAutoFill = New-Object System.Windows.Controls.Button
    $btnAutoFill.Content = "Alle Vorschlaege"
    $btnAutoFill.Padding = [System.Windows.Thickness]::new(15, 8, 15, 8)
    $btnAutoFill.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    $btnAutoFill.ToolTip = "Alle Vorschlaege aus Import uebernehmen"
    $btnAutoFill.Add_Click({
        foreach ($fieldName in $textBoxes.Keys) {
            if ($ExtractedMetadata.ContainsKey($fieldName)) {
                $tb = $textBoxes[$fieldName]
                $suggestedValue = $ExtractedMetadata[$fieldName]
                if ($suggestedValue -and [string]::IsNullOrWhiteSpace($Project.Metadata[$fieldName])) {
                    $tb.Text = $suggestedValue
                    $tb.Foreground = [System.Windows.Media.Brushes]::Black
                    $tb.FontStyle = [System.Windows.FontStyles]::Normal
                    $tb.Tag = @{ IsSuggested = $false }
                }
            }
        }
    })
    
    $buttonPanel.Children.Add($btnAutoFill)
    $buttonPanel.Children.Add($btnCancel)
    $buttonPanel.Children.Add($btnSave)
    $mainPanel.Children.Add($buttonPanel)
    
    $dialog.Content = $mainPanel
    
    return $dialog.ShowDialog()
}

function Show-QuickProjectManager {
    param([Project] $Project)
    return Show-ProjectManagerDialog -Project $Project -ExtractedMetadata @{}
}

Write-Host "[ProjectManagerService] Loaded" -ForegroundColor Green
