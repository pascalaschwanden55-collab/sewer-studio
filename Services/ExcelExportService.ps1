<#
.SYNOPSIS
    ExcelExportService für AuswertungPro - Excel-Datei-Export
.DESCRIPTION
    Exportiert Projekt zu Excel mit Formatierung, AutoFilter, FreezePane
    Unterstützt auch Export mit Vorlage (Template-basiert)
#>

# ========== Vorlage-Pfad ==========
$script:ExcelTemplatePath = Join-Path $PSScriptRoot "..\Export_Vorlage\Haltungen.xlsx"

# ========== Spaltenbreiten-Mapping (Pixel basierend auf Excel-Richtwert) ==========
# Umrechnung: Excel-Zeichenbreiten → WPF/Excel Pixel
$script:ColumnWidths = @{
    'NR' = 50
    'Haltungsname' = 150
    'Strasse' = 160
    'Rohrmaterial' = 100
    'DN_mm' = 70
    'Nutzungsart' = 130
    'Haltungslaenge_m' = 100
    'Fliessrichtung' = 130
    'Primaere_Schaeden' = 250
    'Zustandsklasse' = 100
    'Pruefungsresultat' = 130
    'Sanieren_JaNein' = 80
    'Empfohlene_Sanierungsmassnahmen' = 210
    'Kosten' = 100
    'Eigentuemer' = 100
    'Bemerkungen' = 300
    'Link' = 400
    'Renovierung_Inliner_Stk' = 120
    'Renovierung_Inliner_m' = 100
    'Anschluesse_verpressen' = 130
    'Reparatur_Manschette' = 130
    'Reparatur_Kurzliner' = 120
    'Erneuerung_Neubau_m' = 120
    'Offen_abgeschlossen' = 150
    'Datum_Jahr' = 120
}

# ========== Export-Funktionen ==========
<#
.SYNOPSIS
    Export-ProjectToExcel: Exportiert Projekt zu Excel-Datei
.PARAMETER Project
    Projekt-Objekt
.PARAMETER OutputPath
    Ziel-Excel-Datei
.RETURNS
    $true bei Erfolg, $false bei Fehler
#>
function Export-ProjectToExcel {
    param(
        [Project] $Project,
        [string] $OutputPath
    )
    
    try {
        # Lade Excel COM
        $excel = New-Object -ComObject Excel.Application -ErrorAction Stop
        $excel.Visible = $false
        $excel.DisplayAlerts = $false
        
        # Erstelle Workbook
        $workbook = $excel.Workbooks.Add()
        $worksheet = $workbook.Sheets(1)
        $worksheet.Name = "Haltungen"
        
        # Schreibe Header-Zeile
        $colIndex = 1
        foreach ($fieldName in $script:FieldColumnOrder) {
            $label = Get-FieldLabel -FieldName $fieldName
            $cell = $worksheet.Cells(1, $colIndex)
            $cell.Value = $label
            
            # Formatierung
            $cell.Font.Bold = $true
            $cell.Font.Color = [System.Drawing.ColorTranslator]::ToOle([System.Drawing.Color]::White)
            $cell.Interior.Color = [System.Drawing.ColorTranslator]::ToOle([System.Drawing.Color]::FromArgb(68, 114, 196))
            $cell.HorizontalAlignment = -4108  # xlCenter
            $cell.VerticalAlignment = -4108
            
            # Spaltenbreite
            $width = if ($script:ColumnWidths[$fieldName]) { $script:ColumnWidths[$fieldName] } else { 100 }
            $worksheet.Columns($colIndex).Width = [Math]::Round($width / 7, 1)
            
            $colIndex++
        }
        
        # Schreibe Daten
        $rowIndex = 2
        foreach ($record in $Project.Data) {
            $colIndex = 1
            foreach ($fieldName in $script:FieldColumnOrder) {
                $value = $record.GetFieldValue($fieldName)
                $cell = $worksheet.Cells($rowIndex, $colIndex)
                $cell.Value = $value
                
                # Alternating row color
                if ($rowIndex % 2 -eq 0) {
                    $cell.Interior.Color = [System.Drawing.ColorTranslator]::ToOle([System.Drawing.Color]::FromArgb(242, 242, 242))
                }
                
                # Wrapping für multiline Felder
                $fieldType = Get-FieldType -FieldName $fieldName
                if ($fieldType -eq 'multiline') {
                    $cell.WrapText = $true
                    $worksheet.Rows($rowIndex).RowHeight = 30
                }
                
                # Hyperlink für Link-Felder
                if ($fieldName -eq 'Link' -and -not [string]::IsNullOrWhiteSpace($value)) {
                    if ($value -match '^https?://') {
                        $worksheet.Hyperlinks.Add($cell, $value, $null, "Link") | Out-Null
                        $cell.Font.Color = [System.Drawing.ColorTranslator]::ToOle([System.Drawing.Color]::Blue)
                        $cell.Font.Underline = 1
                    }
                }
                
                $colIndex++
            }
            $rowIndex++
        }
        
        # AutoFilter
        $filterRange = $worksheet.Range("A1").CurrentRegion
        $filterRange.AutoFilter() | Out-Null
        
        # FreezePane (Header-Zeile)
        $worksheet.Range("A2").Select() | Out-Null
        $excel.ActiveWindow.FreezePanes = $true
        
        # Speichere Datei
        $outputPath = [System.IO.Path]::GetFullPath($OutputPath)
        $worksheet.Range("A1").Select() | Out-Null
        
        # Excel 2007+ Format (.xlsx)
        if ($outputPath -match '\.xlsx?$') {
            $workbook.SaveAs($outputPath, 51)  # 51 = xlOpenXMLWorkbook
        } else {
            $workbook.SaveAs($outputPath)
        }
        
        # Cleanup
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($worksheet) | Out-Null
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($workbook) | Out-Null
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
        [System.GC]::Collect()
        
        Log-Info -Message "Excel exportiert: $outputPath ($($Project.Data.Count) Haltungen)" -Context "ExcelExport"
        return $true
    } catch {
        Log-Error -Message "Fehler beim Excel-Export: $_" -Context "ExcelExport" -Exception $_
        try {
            $excel.Quit()
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
        } catch { }
        return $false
    }
}

<#
.SYNOPSIS
    Export-ProjectToExcelSimple: Vereinfachter Export ohne COM (Fallback)
.DESCRIPTION
    Nutzt CSV oder andere Formate wenn Excel COM nicht verfügbar
#>
function Export-ProjectToExcelSimple {
    param(
        [Project] $Project,
        [string] $OutputPath
    )
    
    try {
        # Exportiere als CSV
        $csvPath = $OutputPath -replace '\.xlsx?$', '.csv'
        
        $data = @()
        
        # Header
        $header = ($script:FieldColumnOrder | ForEach-Object { Get-FieldLabel $_ }) -join "`t"
        $data += $header
        
        # Rows
        foreach ($record in $Project.Data) {
            $row = ($script:FieldColumnOrder | ForEach-Object { $record.GetFieldValue($_) }) -join "`t"
            $data += $row
        }
        
        $data | Out-File -FilePath $csvPath -Encoding UTF8 -Force
        
        Log-Info -Message "CSV exportiert (Fallback): $csvPath" -Context "ExcelExport"
        return $csvPath
    } catch {
        Log-Error -Message "Fehler beim CSV-Export: $_" -Context "ExcelExport" -Exception $_
        return $null
    }
}

# ========== Template-basierter Export ==========
<#
.SYNOPSIS
    Export-ProjectWithTemplate: Exportiert mit Vorlage-Datei
.DESCRIPTION
    Kopiert die Vorlage und fügt Daten ab Zeile 2 ein.
    Die Vorlage kann Formatierungen, Logos, etc. enthalten.
.PARAMETER Project
    Projekt-Objekt
.PARAMETER OutputPath
    Ziel-Excel-Datei
.PARAMETER StartRow
    Erste Datenzeile (Standard: 2, da Zeile 1 = Header)
.RETURNS
    $true bei Erfolg, $false bei Fehler
#>
function Export-ProjectWithTemplate {
    param(
        [Project] $Project,
        [string] $OutputPath,
        [int] $StartRow = 2
    )
    
    try {
        # Prüfe ob Vorlage existiert
        $templatePath = [System.IO.Path]::GetFullPath($script:ExcelTemplatePath)
        
        if (-not (Test-Path $templatePath)) {
            Log-Warning -Message "Vorlage nicht gefunden: $templatePath - Verwende Standard-Export" -Context "ExcelExport"
            return Export-ProjectToExcel -Project $Project -OutputPath $OutputPath
        }
        
        # Kopiere Vorlage zum Ziel
        $outputPath = [System.IO.Path]::GetFullPath($OutputPath)
        Copy-Item -Path $templatePath -Destination $outputPath -Force
        
        Log-Info -Message "Vorlage kopiert: $templatePath -> $outputPath" -Context "ExcelExport"
        
        # Öffne die Kopie und füge Daten ein
        $excel = New-Object -ComObject Excel.Application -ErrorAction Stop
        $excel.Visible = $false
        $excel.DisplayAlerts = $false
        
        $workbook = $excel.Workbooks.Open($outputPath)
        $worksheet = $workbook.Sheets(1)
        
        # Schreibe Daten ab StartRow
        $rowIndex = $StartRow
        foreach ($record in $Project.Data) {
            $colIndex = 1
            foreach ($fieldName in $script:FieldColumnOrder) {
                $value = $record.GetFieldValue($fieldName)
                $cell = $worksheet.Cells($rowIndex, $colIndex)
                $cell.Value = $value
                
                # Hyperlink für Link-Felder
                if ($fieldName -eq 'Link' -and -not [string]::IsNullOrWhiteSpace($value)) {
                    if ($value -match '^https?://') {
                        $worksheet.Hyperlinks.Add($cell, $value, $null, "Link") | Out-Null
                        $cell.Font.Color = [System.Drawing.ColorTranslator]::ToOle([System.Drawing.Color]::Blue)
                        $cell.Font.Underline = 1
                    }
                }
                
                $colIndex++
            }
            $rowIndex++
        }
        
        # AutoFilter (falls noch nicht vorhanden)
        try {
            $filterRange = $worksheet.Range("A1").CurrentRegion
            if (-not $worksheet.AutoFilterMode) {
                $filterRange.AutoFilter() | Out-Null
            }
        } catch { }
        
        # Speichern und Schliessen
        $workbook.Save()
        $workbook.Close()
        
        # Cleanup
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($worksheet) | Out-Null
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($workbook) | Out-Null
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
        [System.GC]::Collect()
        
        Log-Info -Message "Excel mit Vorlage exportiert: $outputPath ($($Project.Data.Count) Haltungen)" -Context "ExcelExport"
        return $true
        
    } catch {
        Log-Error -Message "Fehler beim Template-Export: $_" -Context "ExcelExport" -Exception $_
        try {
            $excel.Quit()
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
        } catch { }
        return $false
    }
}

<#
.SYNOPSIS
    Test-ExcelTemplateExists: Prüft ob die Vorlage existiert
#>
function Test-ExcelTemplateExists {
    $templatePath = [System.IO.Path]::GetFullPath($script:ExcelTemplatePath)
    return (Test-Path $templatePath)
}

<#
.SYNOPSIS
    Get-ExcelTemplatePath: Gibt den Pfad zur Vorlage zurück
#>
function Get-ExcelTemplatePath {
    return [System.IO.Path]::GetFullPath($script:ExcelTemplatePath)
}

Write-Host "[ExcelExportService] Loaded" -ForegroundColor Green
