<#
.SYNOPSIS
    AuswertungPro - Professionelles Haltungs-Verwaltungs-Tool
.DESCRIPTION
    Excel-ähnliches DataGrid-Tool für tägliche Kanalinspektion/Sanierung
    - 25-Spalten-Tabelle
    - Robuster Multi-Source-Import (XTF, XTF405, PDF)
    - Merge-Logik: Nutzer-Eingaben nie überschrieben
    - Excel-Export, Autosave, Crash-Recovery
#>

[CmdletBinding()]
param()

# ========== Initialisierung ==========
$ErrorActionPreference = "Continue"
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

$script:AppVersion = "2.1.0"
$script:AppRoot = $PSScriptRoot
$script:ServiceRoot = Join-Path $script:AppRoot "Services"

# Lade Services
. (Join-Path $script:ServiceRoot "Bootstrap.ps1")

# ========== Globale Variablen (UI State) ==========
$script:CurrentProject = $null
$script:CurrentProjectPath = ""
$script:MainWindow = $null
$script:DataGrid = $null
$script:SuppressFieldEvents = $false  # Event-Suppression während Import
$script:AutosaveCallback = $null
$script:UnsavedChangesOnClose = $false

# ========== MainWindow Aufbau ==========
function New-MainWindow {
    $window = New-Object System.Windows.Window
    $window.Title = "AuswertungPro - Professionelle Haltungs-Verwaltung v$script:AppVersion"
    $window.Width = 1400
    $window.Height = 800
    $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterScreen
    
    # ========== Layout Grid ==========
    $mainGrid = New-Object System.Windows.Controls.Grid
    $null = $mainGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "Auto" }))     # Toolbar
    $null = $mainGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "*" }))        # Content
    $null = $mainGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "Auto" }))     # Statusbar
    
    # ========== Toolbar ==========
    $toolbar = New-Object System.Windows.Controls.StackPanel
    $toolbar.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $toolbar.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(240, 240, 240))
    $toolbar.Margin = [System.Windows.Thickness]::new(10)
    $toolbar.Height = 50
    
    # Helper: Create ToolButton
    $createToolButton = {
        param([string]$Content, [string]$Tooltip, [scriptblock]$ClickHandler)
        $btn = New-Object System.Windows.Controls.Button
        $btn.Content = $Content
        $btn.Padding = [System.Windows.Thickness]::new(12, 8, 12, 8)
        $btn.Margin = [System.Windows.Thickness]::new(5, 0, 0, 0)
        $btn.ToolTip = $Tooltip
        $btn.Cursor = [System.Windows.Input.Cursors]::Hand
        $btn.Add_Click($ClickHandler)
        return $btn
    }
    
    # Buttons
    $btnNew = & $createToolButton "[+] Projekt" "Ctrl+N" {
        New-ProjectDialog
    }
    $btnOpen = & $createToolButton "[O] Oeffnen" "Ctrl+O" {
        Open-ProjectDialog
    }
    $btnSave = & $createToolButton "[S] Speichern" "Ctrl+S" {
        Save-CurrentProject
    }
    $btnSaveAs = & $createToolButton "[A] Speichern Unter" "Ctrl+Shift+S" {
        Save-ProjectAsDialog
    }
    $btnProjektManager = & $createToolButton "[P] Projekt" "Projekt-Metadaten" {
        Show-ProjectMetadataDialog
    }
    
    $sep1 = New-Object System.Windows.Controls.Separator
    $sep1.Margin = [System.Windows.Thickness]::new(10, 0, 10, 0)
    
    $btnAddRow = & $createToolButton "[+] Haltung" "Insert" {
        Add-HaltungRow
    }
    $btnDelRow = & $createToolButton "[-] Loeschen" "Del" {
        Delete-CurrentRow
    }
    
    $sep2 = New-Object System.Windows.Controls.Separator
    $sep2.Margin = [System.Windows.Thickness]::new(10, 0, 10, 0)
    
    # Import-Buttons mit klarer Nummerierung
    $btnImportXTF = & $createToolButton "[1] XTF Stamm" "1. SIA405-XTF importieren (Material, DN, Nutzungsart)" {
        Import-Xtf-Dialog
    }
    $btnImportXTF.ToolTip = "SCHRITT 1: SIA405-XTF für Stammdaten (Material, DN, Nutzungsart, Strasse)"
    
    $btnImportVSA = & $createToolButton "[2] XTF Insp." "2. VSA_KEK-XTF importieren (Schäden, Zustandsklasse)" {
        Import-Xtf-Dialog
    }
    $btnImportVSA.ToolTip = "SCHRITT 2: VSA_KEK-XTF für Inspektionsdaten (Schäden, Zustandsklasse)"
    
    $btnImportPDF = & $createToolButton "[3] PDF" "3. PDF importieren (Details, Fotos)" {
        Import-Pdf-Dialog
    }
    $btnImportPDF.ToolTip = "SCHRITT 3 (optional): PDF für zusätzliche Details und Bemerkungen"
    
    $btnImportProj = & $createToolButton "[I] Projekt" "Import .haltproj" {
        Import-Project-Dialog
    }
    $btnExport = & $createToolButton "[E] Excel" "Ctrl+E" {
        Export-Excel-Dialog
    }
    
    $sep3 = New-Object System.Windows.Controls.Separator
    $sep3.Margin = [System.Windows.Thickness]::new(10, 0, 10, 0)
    
    $btnSearch = & $createToolButton "[F] Suche" "Ctrl+F" {
        Show-SearchDialog
    }
    $btnFilter = & $createToolButton "[R] Reset" "Filter" {
        Reset-GridFilter
    }
    
    # Statusbar-Label (in Toolbar integriert)
    $spacer = New-Object System.Windows.Controls.TextBlock
    $spacer.Width = 1000
    
    $script:lblStatus = New-Object System.Windows.Controls.TextBlock
    $script:lblStatus.Text = "Bereit"
    $script:lblStatus.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(100, 100, 100))
    $script:lblStatus.FontSize = 10
    
    $null = $toolbar.Children.Add($btnNew)
    $null = $toolbar.Children.Add($btnOpen)
    $null = $toolbar.Children.Add($btnSave)
    $null = $toolbar.Children.Add($btnSaveAs)
    $null = $toolbar.Children.Add($btnProjektManager)
    $null = $toolbar.Children.Add($sep1)
    $null = $toolbar.Children.Add($btnAddRow)
    $null = $toolbar.Children.Add($btnDelRow)
    $null = $toolbar.Children.Add($sep2)
    $null = $toolbar.Children.Add($btnImportXTF)
    $null = $toolbar.Children.Add($btnImportVSA)
    $null = $toolbar.Children.Add($btnImportPDF)
    $null = $toolbar.Children.Add($btnImportProj)
    $null = $toolbar.Children.Add($btnExport)
    $null = $toolbar.Children.Add($sep3)
    $null = $toolbar.Children.Add($btnSearch)
    $null = $toolbar.Children.Add($btnFilter)
    $null = $toolbar.Children.Add($spacer)
    $null = $toolbar.Children.Add($script:lblStatus)
    
    [System.Windows.Controls.Grid]::SetRow($toolbar, 0)
    
    # ========== DataGrid ==========
    $script:DataGrid = New-Object System.Windows.Controls.DataGrid
    $script:DataGrid.AutoGenerateColumns = $false
    $script:DataGrid.CanUserAddRows = $false
    $script:DataGrid.CanUserDeleteRows = $false
    $script:DataGrid.CanUserResizeRows = $false
    $script:DataGrid.RowHeight = 25
    $script:DataGrid.HeadersVisibility = [System.Windows.Controls.DataGridHeadersVisibility]::Column
    $script:DataGrid.SelectionMode = [System.Windows.Controls.DataGridSelectionMode]::Single
    $script:DataGrid.SelectionUnit = [System.Windows.Controls.DataGridSelectionUnit]::FullRow
    $script:DataGrid.Background = [System.Windows.Media.Brushes]::White
    $script:DataGrid.GridLinesVisibility = [System.Windows.Controls.DataGridGridLinesVisibility]::Horizontal
    
    # Definiere Spalten (25 Spalten)
    foreach ($fieldName in $script:FieldColumnOrder) {
        $label = Get-FieldLabel -FieldName $fieldName
        $fieldType = Get-FieldType -FieldName $fieldName
        $width = if ($script:ColumnWidths[$fieldName]) { $script:ColumnWidths[$fieldName] } else { 100 }
        
        $column = $null
        $binding = New-Object System.Windows.Data.Binding -ArgumentList $fieldName
        
        if ($fieldType -eq 'combo') {
            $column = New-Object System.Windows.Controls.DataGridComboBoxColumn
            $column.ItemsSource = New-Object System.Collections.ObjectModel.ObservableCollection[string]
            (Get-ComboBoxItems -FieldName $fieldName) | ForEach-Object {
                $null = $column.ItemsSource.Add($_)
            }
            $column.IsReadOnly = $false
            $column.SelectedItemBinding = $binding
        } else {
            $column = New-Object System.Windows.Controls.DataGridTextColumn
            if ($fieldType -eq 'multiline') {
                # TextWrapping fuer multiline
                $column.EditingElementStyle = New-Object System.Windows.Style -ArgumentList ([System.Windows.Controls.TextBox])
                $null = $column.EditingElementStyle.Setters.Add((New-Object System.Windows.Setter -ArgumentList ([System.Windows.Controls.TextBox]::TextWrappingProperty, [System.Windows.TextWrapping]::Wrap)))
            }
            $column.Binding = $binding
        }
        
        $column.Header = $label
        $column.Width = [System.Windows.Controls.DataGridLength]::new($width)
        # Speichere Feldname im Tag für Sortierung
        $column.SortMemberPath = $fieldName
        
        $null = $script:DataGrid.Columns.Add($column)
    }
    
    # Sorting-Event-Handler für numerische Sortierung
    $script:DataGrid.Add_Sorting({
        param($sender, $e)
        
        $fieldName = $e.Column.SortMemberPath
        $fieldType = Get-FieldType -FieldName $fieldName
        
        # Nur für numerische Felder benutzerdefinierte Sortierung
        if ($fieldType -eq 'int' -or $fieldType -eq 'decimal' -or $fieldName -eq 'DN_mm' -or $fieldName -eq 'Haltungslaenge_m') {
            $e.Handled = $true
            
            # Bestimme Sortierrichtung
            $direction = if ($e.Column.SortDirection -eq [System.ComponentModel.ListSortDirection]::Ascending) {
                [System.ComponentModel.ListSortDirection]::Descending
            } else {
                [System.ComponentModel.ListSortDirection]::Ascending
            }
            $e.Column.SortDirection = $direction
            
            # Hole aktuelle Items
            $items = $script:DataGrid.ItemsSource
            if ($items -and $items.Count -gt 0) {
                # Sortiere mit numerischem Vergleich
                $sorted = if ($direction -eq [System.ComponentModel.ListSortDirection]::Ascending) {
                    $items | Sort-Object { 
                        $val = $_.$fieldName
                        if ([string]::IsNullOrWhiteSpace($val)) { return [double]::MaxValue }
                        $num = 0
                        if ([double]::TryParse($val.ToString().Replace(',', '.'), [ref]$num)) { return $num }
                        return [double]::MaxValue
                    }
                } else {
                    $items | Sort-Object { 
                        $val = $_.$fieldName
                        if ([string]::IsNullOrWhiteSpace($val)) { return [double]::MinValue }
                        $num = 0
                        if ([double]::TryParse($val.ToString().Replace(',', '.'), [ref]$num)) { return $num }
                        return [double]::MinValue
                    } -Descending
                }
                
                # Erstelle neue ObservableCollection
                $newList = New-Object System.Collections.ObjectModel.ObservableCollection[PSObject]
                foreach ($item in $sorted) {
                    $null = $newList.Add($item)
                }
                $script:DataGrid.ItemsSource = $newList
            }
        }
    })
    
    # Doppelklick-Handler für Detail-Ansicht (besonders für lange Texte wie Primaere_Schaeden)
    $script:DataGrid.Add_MouseDoubleClick({
        param($sender, $e)
        
        # Finde die angeklickte Zelle
        $cell = $e.OriginalSource
        while ($cell -ne $null -and $cell.GetType().Name -ne 'DataGridCell') {
            $cell = [System.Windows.Media.VisualTreeHelper]::GetParent($cell)
        }
        
        if ($cell -ne $null) {
            $column = $cell.Column
            $row = $script:DataGrid.SelectedItem
            
            if ($column -and $row) {
                $fieldName = $column.SortMemberPath
                $fieldLabel = $column.Header.ToString()
                $value = $row.$fieldName
                
                # Zeige Detail-Dialog
                Show-CellDetailDialog -FieldName $fieldName -FieldLabel $fieldLabel -Value $value -Row $row
            }
        }
    })
    
    # Kontextmenü für Zeilen-Operationen
    $contextMenu = New-Object System.Windows.Controls.ContextMenu
    
    $menuMoveUp = New-Object System.Windows.Controls.MenuItem
    $menuMoveUp.Header = "↑ Nach oben (Ctrl+↑)"
    $menuMoveUp.Add_Click({ Move-RowUp })
    
    $menuMoveDown = New-Object System.Windows.Controls.MenuItem
    $menuMoveDown.Header = "↓ Nach unten (Ctrl+↓)"
    $menuMoveDown.Add_Click({ Move-RowDown })
    
    $menuMoveTo = New-Object System.Windows.Controls.MenuItem
    $menuMoveTo.Header = "→ Auf Position... (Ctrl+M)"
    $menuMoveTo.Add_Click({ Show-MoveToPositionDialog })
    
    $menuSeparator = New-Object System.Windows.Controls.Separator
    
    $menuDelete = New-Object System.Windows.Controls.MenuItem
    $menuDelete.Header = "✕ Löschen (Del)"
    $menuDelete.Add_Click({ Delete-CurrentRow })
    
    $null = $contextMenu.Items.Add($menuMoveUp)
    $null = $contextMenu.Items.Add($menuMoveDown)
    $null = $contextMenu.Items.Add($menuMoveTo)
    $null = $contextMenu.Items.Add($menuSeparator)
    $null = $contextMenu.Items.Add($menuDelete)
    
    $script:DataGrid.ContextMenu = $contextMenu
    
    [System.Windows.Controls.Grid]::SetRow($script:DataGrid, 1)
    
    # ========== Statusbar (unten) ==========
    $statusBar = New-Object System.Windows.Controls.Border
    $statusBar.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(245, 245, 245))
    $statusBar.BorderThickness = [System.Windows.Thickness]::new(0, 1, 0, 0)
    $statusBar.BorderBrush = [System.Windows.Media.Brushes]::LightGray
    
    $lblProjectPath = New-Object System.Windows.Controls.TextBlock
    $lblProjectPath.Text = "Kein Projekt geladen"
    $lblProjectPath.Margin = [System.Windows.Thickness]::new(10, 5, 10, 5)
    $lblProjectPath.FontSize = 10
    $lblProjectPath.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(100, 100, 100))
    
    $statusBar.Child = $lblProjectPath
    $script:lblProjectPath = $lblProjectPath
    
    [System.Windows.Controls.Grid]::SetRow($statusBar, 2)
    
    # ========== Zusammenbau ==========
    $null = $mainGrid.Children.Add($toolbar)
    $null = $mainGrid.Children.Add($script:DataGrid)
    $null = $mainGrid.Children.Add($statusBar)
    
    $window.Content = $mainGrid
    
    # ========== Keyboard Shortcuts ==========
    $window.Add_PreviewKeyDown({
        param($sender, $e)
        $ctrl = [System.Windows.Input.Keyboard]::IsKeyDown([System.Windows.Input.Key]::LeftCtrl) -or `
                [System.Windows.Input.Keyboard]::IsKeyDown([System.Windows.Input.Key]::RightCtrl)
        $shift = [System.Windows.Input.Keyboard]::IsKeyDown([System.Windows.Input.Key]::LeftShift) -or `
                 [System.Windows.Input.Keyboard]::IsKeyDown([System.Windows.Input.Key]::RightShift)
        
        if ($ctrl -and $shift) {
            switch ($e.Key) {
                'S' { Save-ProjectAsDialog; $e.Handled = $true }
            }
        } elseif ($ctrl) {
            switch ($e.Key) {
                'N' { New-ProjectDialog; $e.Handled = $true }
                'O' { Open-ProjectDialog; $e.Handled = $true }
                'S' { Save-CurrentProject; $e.Handled = $true }
                'E' { Export-Excel-Dialog; $e.Handled = $true }
                'F' { Show-SearchDialog; $e.Handled = $true }
                'Add' { Add-HaltungRow; $e.Handled = $true }
                'Up' { Move-RowUp; $e.Handled = $true }
                'Down' { Move-RowDown; $e.Handled = $true }
                'M' { Show-MoveToPositionDialog; $e.Handled = $true }
            }
        } else {
            switch ($e.Key) {
                'Insert' { Add-HaltungRow; $e.Handled = $true }
                'Delete' { Delete-CurrentRow; $e.Handled = $true }
            }
        }
    })
    
    # ========== Window Close ==========
    $window.Add_Closing({
        param($sender, $e)
        if ($script:CurrentProject -and $script:CurrentProject.Dirty) {
            $result = [System.Windows.MessageBox]::Show(
                "Ungespeicherte Aenderungen vorhanden. Moechten Sie speichern?",
                "AuswertungPro",
                [System.Windows.MessageBoxButton]::YesNoCancel,
                [System.Windows.MessageBoxImage]::Question
            )
            
            switch ($result) {
                'Yes' { Save-CurrentProject }
                'Cancel' { $e.Cancel = $true }
            }
        }
    })
    
    return $window
}

# ========== Project Management ==========
function New-ProjectDialog {
    $script:CurrentProject = New-Project
    $script:CurrentProjectPath = ""
    $script:LastImportMetadata = @{}
    
    # Oeffne sofort den Projekt-Manager zum Ausfuellen der Daten
    $result = Show-ProjectManagerDialog -Project $script:CurrentProject -ExtractedMetadata @{}
    
    if ($result) {
        $script:lblProjectPath.Text = "Projekt: $($script:CurrentProject.Name)"
        $script:lblStatus.Text = "Neues Projekt erstellt: $($script:CurrentProject.Name)"
    } else {
        $script:lblProjectPath.Text = "Neues Projekt (nicht gespeichert)"
        $script:lblStatus.Text = "Neues Projekt erstellt"
    }
    
    Update-GridFromProject
}

function Open-ProjectDialog {
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "Haltungs-Projekte (*.haltproj)|*.haltproj|Alle Dateien|*.*"
    $dialog.InitialDirectory = (Get-ProjectPath)
    
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $script:CurrentProject = Load-Project -Path $dialog.FileName
        if ($script:CurrentProject) {
            $script:CurrentProjectPath = $dialog.FileName
            $script:lblProjectPath.Text = "Projekt: $($script:CurrentProject.Name) ($($script:CurrentProject.Data.Count) Haltungen)"
            Update-GridFromProject
            $script:lblStatus.Text = "Projekt geladen"
        }
    }
}

function Save-CurrentProject {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Kein Projekt geladen.", "AuswertungPro")
        return
    }
    
    if ([string]::IsNullOrEmpty($script:CurrentProjectPath)) {
        # Kein Pfad gesetzt -> Speichern unter Dialog
        Save-ProjectAsDialog
        return
    }
    
    if (Save-Project -Project $script:CurrentProject -Path $script:CurrentProjectPath) {
        $script:lblStatus.Text = "Projekt gespeichert: $($script:CurrentProjectPath)"
    } else {
        [System.Windows.MessageBox]::Show("Fehler beim Speichern.", "Fehler")
    }
}

function Save-ProjectAsDialog {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Kein Projekt geladen.", "AuswertungPro")
        return
    }
    
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Filter = "Haltungs-Projekte (*.haltproj)|*.haltproj|Alle Dateien|*.*"
    $dialog.DefaultExt = ".haltproj"
    $dialog.InitialDirectory = (Get-ProjectPath)
    
    # Schlage Projektname als Dateiname vor
    $suggestedName = $script:CurrentProject.Name
    if ([string]::IsNullOrWhiteSpace($suggestedName)) {
        $suggestedName = "Neues_Projekt"
    }
    # Entferne ungueltige Zeichen aus Dateiname
    $suggestedName = $suggestedName -replace '[<>:"/\\|?*]', '_'
    $dialog.FileName = $suggestedName
    
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $savePath = $dialog.FileName
        
        if (Save-Project -Project $script:CurrentProject -Path $savePath) {
            $script:CurrentProjectPath = $savePath
            $script:lblProjectPath.Text = "Projekt: $($script:CurrentProject.Name)"
            $script:lblStatus.Text = "Projekt gespeichert unter: $savePath"
            
            [System.Windows.MessageBox]::Show(
                "Projekt erfolgreich gespeichert:`n$savePath", 
                "Gespeichert", 
                [System.Windows.MessageBoxButton]::OK,
                [System.Windows.MessageBoxImage]::Information
            )
        } else {
            [System.Windows.MessageBox]::Show("Fehler beim Speichern.", "Fehler")
        }
    }
}

function Update-GridFromProject {
    # Konvertiere Project.Data zu DataGrid-Items
    $dataList = New-Object System.Collections.ObjectModel.ObservableCollection[PSObject]
    
    if ($script:CurrentProject -and $script:CurrentProject.Data) {
        foreach ($record in $script:CurrentProject.Data) {
            $item = [PSCustomObject]@{}
            foreach ($fieldName in $script:FieldColumnOrder) {
                $item | Add-Member -Name $fieldName -Value $record.GetFieldValue($fieldName) -MemberType NoteProperty
            }
            $null = $dataList.Add($item)
        }
    }
    
    $script:DataGrid.ItemsSource = $dataList
    
    # Aktualisiere Projekt-Label mit Datenqualitäts-Info
    if ($script:CurrentProject -and $script:lblProjectPath) {
        $count = $script:CurrentProject.Data.Count
        $qualityInfo = Get-DataQualityInfo
        $script:lblProjectPath.Text = "Projekt: $($script:CurrentProject.Name) ($count Haltungen) $qualityInfo"
    }
}

function Get-DataQualityInfo {
    if (-not $script:CurrentProject -or $script:CurrentProject.Data.Count -eq 0) {
        return ""
    }
    
    # Prüfe ob wichtige Stammdaten vorhanden sind
    $withMaterial = 0
    $withDN = 0
    $withNutzungsart = 0
    $total = $script:CurrentProject.Data.Count
    
    foreach ($record in $script:CurrentProject.Data) {
        if ($record.GetFieldValue('Rohrmaterial')) { $withMaterial++ }
        if ($record.GetFieldValue('DN_mm')) { $withDN++ }
        if ($record.GetFieldValue('Nutzungsart')) { $withNutzungsart++ }
    }
    
    # Berechne Vollständigkeit
    $completeness = [math]::Round((($withMaterial + $withDN + $withNutzungsart) / ($total * 3)) * 100, 0)
    
    if ($completeness -eq 0) {
        return "| ⚠ Stammdaten fehlen"
    } elseif ($completeness -lt 50) {
        return "| Stammdaten: ${completeness}%"
    } else {
        return ""
    }
}

function Add-HaltungRow {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Kein Projekt geladen.", "AuswertungPro")
        return
    }
    
    $record = $script:CurrentProject.CreateNewRecord()
    $script:CurrentProject.AddRecord($record)
    Update-GridFromProject
    $script:CurrentProject.Dirty = $true
    $script:lblStatus.Text = "Neue Haltung hinzugefügt"
}

function Delete-CurrentRow {
    if ($script:DataGrid.SelectedIndex -ge 0) {
        $selectedIndex = $script:DataGrid.SelectedIndex
        if ($selectedIndex -lt $script:CurrentProject.Data.Count) {
            $record = $script:CurrentProject.Data[$selectedIndex]
            $script:CurrentProject.RemoveRecord($record.Id)
            Update-GridFromProject
            $script:lblStatus.Text = "Haltung gelöscht"
        }
    }
}

# ========== Zeilen-Verschiebung ==========
function Move-RowUp {
    $selectedIndex = $script:DataGrid.SelectedIndex
    if ($selectedIndex -le 0 -or -not $script:CurrentProject) { return }
    
    # Tausche Positionen in der Daten-Liste
    $temp = $script:CurrentProject.Data[$selectedIndex - 1]
    $script:CurrentProject.Data[$selectedIndex - 1] = $script:CurrentProject.Data[$selectedIndex]
    $script:CurrentProject.Data[$selectedIndex] = $temp
    
    # NR-Nummern aktualisieren
    Renumber-AllRows
    
    Update-GridFromProject
    $script:DataGrid.SelectedIndex = $selectedIndex - 1
    $script:CurrentProject.Dirty = $true
    $script:lblStatus.Text = "Zeile nach oben verschoben"
}

function Move-RowDown {
    $selectedIndex = $script:DataGrid.SelectedIndex
    if ($selectedIndex -lt 0 -or -not $script:CurrentProject) { return }
    if ($selectedIndex -ge $script:CurrentProject.Data.Count - 1) { return }
    
    # Tausche Positionen in der Daten-Liste
    $temp = $script:CurrentProject.Data[$selectedIndex + 1]
    $script:CurrentProject.Data[$selectedIndex + 1] = $script:CurrentProject.Data[$selectedIndex]
    $script:CurrentProject.Data[$selectedIndex] = $temp
    
    # NR-Nummern aktualisieren
    Renumber-AllRows
    
    Update-GridFromProject
    $script:DataGrid.SelectedIndex = $selectedIndex + 1
    $script:CurrentProject.Dirty = $true
    $script:lblStatus.Text = "Zeile nach unten verschoben"
}

function Move-RowToPosition {
    param([int] $NewPosition)
    
    $selectedIndex = $script:DataGrid.SelectedIndex
    if ($selectedIndex -lt 0 -or -not $script:CurrentProject) { return }
    
    $maxPos = $script:CurrentProject.Data.Count
    if ($NewPosition -lt 1 -or $NewPosition -gt $maxPos) {
        [System.Windows.MessageBox]::Show("Position muss zwischen 1 und $maxPos liegen.", "Ungültige Position")
        return
    }
    
    $targetIndex = $NewPosition - 1
    if ($targetIndex -eq $selectedIndex) { return }
    
    # Entferne Element und füge an neuer Position ein
    $record = $script:CurrentProject.Data[$selectedIndex]
    $script:CurrentProject.Data.RemoveAt($selectedIndex)
    $script:CurrentProject.Data.Insert($targetIndex, $record)
    
    # NR-Nummern aktualisieren
    Renumber-AllRows
    
    Update-GridFromProject
    $script:DataGrid.SelectedIndex = $targetIndex
    $script:CurrentProject.Dirty = $true
    $script:lblStatus.Text = "Zeile auf Position $NewPosition verschoben"
}

function Renumber-AllRows {
    if (-not $script:CurrentProject) { return }
    
    $nr = 1
    foreach ($record in $script:CurrentProject.Data) {
        $record.SetFieldValue('NR', $nr.ToString(), 'manual', $false)
        $nr++
    }
}

function Show-MoveToPositionDialog {
    $selectedIndex = $script:DataGrid.SelectedIndex
    if ($selectedIndex -lt 0 -or -not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Bitte zuerst eine Zeile auswählen.", "AuswertungPro")
        return
    }
    
    $currentNr = $selectedIndex + 1
    $maxNr = $script:CurrentProject.Data.Count
    $haltungsname = $script:CurrentProject.Data[$selectedIndex].GetFieldValue('Haltungsname')
    
    $dialog = New-Object System.Windows.Window
    $dialog.Title = "Zeile verschieben"
    $dialog.Width = 350
    $dialog.Height = 180
    $dialog.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterScreen
    $dialog.ResizeMode = [System.Windows.ResizeMode]::NoResize
    
    $panel = New-Object System.Windows.Controls.StackPanel
    $panel.Margin = [System.Windows.Thickness]::new(20)
    
    $lblInfo = New-Object System.Windows.Controls.TextBlock
    $lblInfo.Text = "Haltung: $haltungsname`nAktuelle Position: $currentNr von $maxNr"
    $lblInfo.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    
    $inputPanel = New-Object System.Windows.Controls.StackPanel
    $inputPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $inputPanel.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    
    $lblNew = New-Object System.Windows.Controls.TextBlock
    $lblNew.Text = "Neue Position (1-$maxNr):"
    $lblNew.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $lblNew.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    
    $txtPos = New-Object System.Windows.Controls.TextBox
    $txtPos.Width = 60
    $txtPos.Text = $currentNr.ToString()
    $txtPos.VerticalContentAlignment = [System.Windows.VerticalAlignment]::Center
    
    $null = $inputPanel.Children.Add($lblNew)
    $null = $inputPanel.Children.Add($txtPos)
    
    $btnPanel = New-Object System.Windows.Controls.StackPanel
    $btnPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $btnPanel.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    
    $btnOk = New-Object System.Windows.Controls.Button
    $btnOk.Content = "Verschieben"
    $btnOk.Padding = [System.Windows.Thickness]::new(15, 5, 15, 5)
    $btnOk.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    $btnOk.Add_Click({
        $newPos = 0
        if ([int]::TryParse($txtPos.Text, [ref]$newPos)) {
            Move-RowToPosition -NewPosition $newPos
            $dialog.Close()
        } else {
            [System.Windows.MessageBox]::Show("Bitte eine gültige Zahl eingeben.", "Fehler")
        }
    }.GetNewClosure())
    
    $btnCancel = New-Object System.Windows.Controls.Button
    $btnCancel.Content = "Abbrechen"
    $btnCancel.Padding = [System.Windows.Thickness]::new(15, 5, 15, 5)
    $btnCancel.Add_Click({ $dialog.Close() })
    
    $null = $btnPanel.Children.Add($btnOk)
    $null = $btnPanel.Children.Add($btnCancel)
    
    $null = $panel.Children.Add($lblInfo)
    $null = $panel.Children.Add($inputPanel)
    $null = $panel.Children.Add($btnPanel)
    
    $dialog.Content = $panel
    $null = $dialog.ShowDialog()
}

# ========== Projekt-Import Dialog ==========
function Import-Project-Dialog {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Bitte zuerst ein Projekt erstellen oder oeffnen.", "AuswertungPro")
        return
    }
    
    # Datei auswaehlen
    $fileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $fileDialog.Filter = "Haltungs-Projekte (*.haltproj)|*.haltproj|Alle Dateien|*.*"
    $fileDialog.Title = "Projekt zum Importieren auswaehlen"
    $fileDialog.InitialDirectory = (Get-ProjectPath)
    
    if ($fileDialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return
    }
    
    $sourcePath = $fileDialog.FileName
    
    # Merge-Modus auswaehlen
    $modeDialog = New-Object System.Windows.Window
    $modeDialog.Title = "Import-Modus waehlen"
    $modeDialog.Width = 450
    $modeDialog.Height = 320
    $modeDialog.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterOwner
    $modeDialog.ResizeMode = [System.Windows.ResizeMode]::NoResize
    
    $panel = New-Object System.Windows.Controls.StackPanel
    $panel.Margin = [System.Windows.Thickness]::new(20)
    
    $header = New-Object System.Windows.Controls.TextBlock
    $header.Text = "Wie sollen die Haltungen importiert werden?"
    $header.FontSize = 14
    $header.FontWeight = [System.Windows.FontWeights]::Bold
    $header.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    $null = $panel.Children.Add($header)
    
    $fileInfo = New-Object System.Windows.Controls.TextBlock
    $fileInfo.Text = "Datei: $([System.IO.Path]::GetFileName($sourcePath))"
    $fileInfo.FontStyle = [System.Windows.FontStyles]::Italic
    $fileInfo.Margin = [System.Windows.Thickness]::new(0, 0, 0, 20)
    $null = $panel.Children.Add($fileInfo)
    
    # Radio Buttons fuer Merge-Modus
    $rbMerge = New-Object System.Windows.Controls.RadioButton
    $rbMerge.Content = "Zusammenfuehren (Merge)"
    $rbMerge.IsChecked = $true
    $rbMerge.Margin = [System.Windows.Thickness]::new(0, 0, 0, 5)
    $null = $panel.Children.Add($rbMerge)
    
    $mergeDesc = New-Object System.Windows.Controls.TextBlock
    $mergeDesc.Text = "    Bestehende Haltungen aktualisieren, neue hinzufuegen.`n    Manuell bearbeitete Felder werden nicht ueberschrieben."
    $mergeDesc.FontSize = 11
    $mergeDesc.Foreground = [System.Windows.Media.Brushes]::Gray
    $mergeDesc.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
    $null = $panel.Children.Add($mergeDesc)
    
    $rbAppend = New-Object System.Windows.Controls.RadioButton
    $rbAppend.Content = "Nur neue hinzufuegen (Append)"
    $rbAppend.Margin = [System.Windows.Thickness]::new(0, 0, 0, 5)
    $null = $panel.Children.Add($rbAppend)
    
    $appendDesc = New-Object System.Windows.Controls.TextBlock
    $appendDesc.Text = "    Nur Haltungen hinzufuegen, die noch nicht existieren.`n    Bestehende Haltungen bleiben unveraendert."
    $appendDesc.FontSize = 11
    $appendDesc.Foreground = [System.Windows.Media.Brushes]::Gray
    $appendDesc.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
    $null = $panel.Children.Add($appendDesc)
    
    $rbReplace = New-Object System.Windows.Controls.RadioButton
    $rbReplace.Content = "Alles ersetzen (Replace)"
    $rbReplace.Margin = [System.Windows.Thickness]::new(0, 0, 0, 5)
    $null = $panel.Children.Add($rbReplace)
    
    $replaceDesc = New-Object System.Windows.Controls.TextBlock
    $replaceDesc.Text = "    ACHTUNG: Alle bestehenden Haltungen werden geloescht`n    und durch die importierten ersetzt!"
    $replaceDesc.FontSize = 11
    $replaceDesc.Foreground = [System.Windows.Media.Brushes]::OrangeRed
    $replaceDesc.Margin = [System.Windows.Thickness]::new(0, 0, 0, 15)
    $null = $panel.Children.Add($replaceDesc)
    
    # Buttons
    $btnPanel = New-Object System.Windows.Controls.StackPanel
    $btnPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $btnPanel.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    
    $btnCancel = New-Object System.Windows.Controls.Button
    $btnCancel.Content = "Abbrechen"
    $btnCancel.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
    $btnCancel.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    $btnCancel.Add_Click({ $modeDialog.DialogResult = $false; $modeDialog.Close() })
    $null = $btnPanel.Children.Add($btnCancel)
    
    $btnImport = New-Object System.Windows.Controls.Button
    $btnImport.Content = "Importieren"
    $btnImport.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
    $btnImport.FontWeight = [System.Windows.FontWeights]::Bold
    $btnImport.Add_Click({ $modeDialog.DialogResult = $true; $modeDialog.Close() })
    $null = $btnPanel.Children.Add($btnImport)
    
    $null = $panel.Children.Add($btnPanel)
    $modeDialog.Content = $panel
    
    if ($modeDialog.ShowDialog() -ne $true) {
        return
    }
    
    # Merge-Modus bestimmen
    $mergeMode = 'merge'
    if ($rbAppend.IsChecked) { $mergeMode = 'append' }
    if ($rbReplace.IsChecked) { $mergeMode = 'replace' }
    
    # Bestaetigung bei Replace
    if ($mergeMode -eq 'replace') {
        $confirm = [System.Windows.MessageBox]::Show(
            "ACHTUNG: Alle $($script:CurrentProject.Data.Count) bestehenden Haltungen werden geloescht!`n`nFortfahren?",
            "Bestaetigung",
            [System.Windows.MessageBoxButton]::YesNo,
            [System.Windows.MessageBoxImage]::Warning
        )
        if ($confirm -ne [System.Windows.MessageBoxResult]::Yes) {
            return
        }
    }
    
    # Import durchfuehren
    $script:lblStatus.Text = "Importiere Projekt..."
    
    $result = Import-ProjectData -TargetProject $script:CurrentProject -SourcePath $sourcePath -MergeMode $mergeMode
    
    if ($result.Success) {
        Update-GridFromProject
        
        $message = "Import erfolgreich!`n`n"
        $message += "Quelle: $($result.SourceName)`n"
        $message += "Modus: $mergeMode`n`n"
        $message += "Neu importiert: $($result.Imported)`n"
        $message += "Zusammengefuehrt: $($result.Merged)`n"
        if ($result.Skipped -gt 0) {
            $message += "Uebersprungen: $($result.Skipped)"
        }
        
        [System.Windows.MessageBox]::Show($message, "Import abgeschlossen", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
        
        $script:lblStatus.Text = "Import: $($result.Imported) neu, $($result.Merged) gemerged"
    } else {
        [System.Windows.MessageBox]::Show("Fehler beim Import:`n$($result.Error)", "Fehler", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
        $script:lblStatus.Text = "Import fehlgeschlagen"
    }
}

# ========== Projekt-Manager Dialog ==========
function Show-ProjectMetadataDialog {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Bitte zuerst ein Projekt erstellen oder öffnen.", "AuswertungPro")
        return
    }
    
    $result = Show-ProjectManagerDialog -Project $script:CurrentProject -ExtractedMetadata $script:LastImportMetadata
    if ($result) {
        $script:lblStatus.Text = "Projekt-Metadaten gespeichert"
        $script:lblProjectPath.Text = "Projekt: $($script:CurrentProject.Name) ($($script:CurrentProject.Data.Count) Haltungen)"
    }
}

# Speichert letzte Import-Metadaten für Projekt-Manager
$script:LastImportMetadata = @{}
$script:ImportHelpShown = $false

# ========== Zell-Detail Dialog (Doppelklick) ==========
function Show-CellDetailDialog {
    param(
        [string] $FieldName,
        [string] $FieldLabel,
        [string] $Value,
        [PSObject] $Row
    )
    
    $dialog = New-Object System.Windows.Window
    $dialog.Title = "Details: $FieldLabel"
    $dialog.Width = 500
    $dialog.Height = 400
    $dialog.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterScreen
    $dialog.ResizeMode = [System.Windows.ResizeMode]::CanResize
    
    $grid = New-Object System.Windows.Controls.Grid
    $grid.Margin = [System.Windows.Thickness]::new(10)
    
    # Zeilen: Header, TextBox, Buttons
    $row0 = New-Object System.Windows.Controls.RowDefinition
    $row0.Height = [System.Windows.GridLength]::Auto
    $row1 = New-Object System.Windows.Controls.RowDefinition
    $row1.Height = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)
    $row2 = New-Object System.Windows.Controls.RowDefinition
    $row2.Height = [System.Windows.GridLength]::Auto
    $null = $grid.RowDefinitions.Add($row0)
    $null = $grid.RowDefinitions.Add($row1)
    $null = $grid.RowDefinitions.Add($row2)
    
    # Header mit Haltungsname
    $haltungsname = $Row.Haltungsname
    $lblHeader = New-Object System.Windows.Controls.TextBlock
    $lblHeader.Text = "Haltung: $haltungsname`nFeld: $FieldLabel"
    $lblHeader.FontWeight = [System.Windows.FontWeights]::Bold
    $lblHeader.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
    [System.Windows.Controls.Grid]::SetRow($lblHeader, 0)
    
    # TextBox mit vollem Inhalt (scrollbar)
    $txtContent = New-Object System.Windows.Controls.TextBox
    $txtContent.Text = $Value
    $txtContent.TextWrapping = [System.Windows.TextWrapping]::Wrap
    $txtContent.AcceptsReturn = $true
    $txtContent.VerticalScrollBarVisibility = [System.Windows.Controls.ScrollBarVisibility]::Auto
    $txtContent.FontFamily = New-Object System.Windows.Media.FontFamily("Consolas")
    $txtContent.FontSize = 12
    $txtContent.IsReadOnly = $false
    [System.Windows.Controls.Grid]::SetRow($txtContent, 1)
    
    # Button-Panel
    $btnPanel = New-Object System.Windows.Controls.StackPanel
    $btnPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $btnPanel.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    $btnPanel.Margin = [System.Windows.Thickness]::new(0, 10, 0, 0)
    [System.Windows.Controls.Grid]::SetRow($btnPanel, 2)
    
    # Kopieren-Button
    $btnCopy = New-Object System.Windows.Controls.Button
    $btnCopy.Content = "Kopieren"
    $btnCopy.Padding = [System.Windows.Thickness]::new(15, 5, 15, 5)
    $btnCopy.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    $btnCopy.Add_Click({
        [System.Windows.Clipboard]::SetText($txtContent.Text)
        $script:lblStatus.Text = "Text kopiert"
    })
    
    # Speichern-Button (falls geändert)
    $btnSave = New-Object System.Windows.Controls.Button
    $btnSave.Content = "Speichern"
    $btnSave.Padding = [System.Windows.Thickness]::new(15, 5, 15, 5)
    $btnSave.Margin = [System.Windows.Thickness]::new(0, 0, 10, 0)
    $btnSave.Add_Click({
        param($s, $e)
        $newValue = $txtContent.Text
        
        # Finde und aktualisiere den Record
        $haltName = $Row.Haltungsname
        foreach ($rec in $script:CurrentProject.Data) {
            if ($rec.GetFieldValue('Haltungsname') -eq $haltName) {
                $rec.SetFieldValue($FieldName, $newValue, 'manual', $false)
                break
            }
        }
        
        # Grid aktualisieren
        Update-GridFromProject
        $script:lblStatus.Text = "Feld '$FieldLabel' gespeichert"
        $dialog.Close()
    }.GetNewClosure())
    
    # Schliessen-Button
    $btnClose = New-Object System.Windows.Controls.Button
    $btnClose.Content = "Schliessen"
    $btnClose.Padding = [System.Windows.Thickness]::new(15, 5, 15, 5)
    $btnClose.Add_Click({ $dialog.Close() })
    
    $null = $btnPanel.Children.Add($btnCopy)
    $null = $btnPanel.Children.Add($btnSave)
    $null = $btnPanel.Children.Add($btnClose)
    
    $null = $grid.Children.Add($lblHeader)
    $null = $grid.Children.Add($txtContent)
    $null = $grid.Children.Add($btnPanel)
    
    $dialog.Content = $grid
    $null = $dialog.ShowDialog()
}

# ========== Import-Hilfe Dialog ==========
function Show-ImportHelpIfNeeded {
    if ($script:ImportHelpShown) { return }
    
    if ($script:CurrentProject -and $script:CurrentProject.Data.Count -eq 0) {
        $helpText = @"
IMPORT-REIHENFOLGE (empfohlen):

SCHRITT 1: [1] XTF Stamm
   → SIA405-XTF (*_SIA405.xtf)
   → Enthält: Material, DN, Nutzungsart, Strasse

SCHRITT 2: [2] XTF Insp.
   → VSA_KEK-XTF (normale .xtf)
   → Enthält: Schäden, Zustandsklasse, Prüfungsdaten

SCHRITT 3: [3] PDF (optional)
   → Inspektionsbericht als PDF
   → Enthält: Details, Bemerkungen, Fotos

Die Daten werden automatisch über den Haltungsnamen zusammengeführt.
"@
        
        [System.Windows.MessageBox]::Show($helpText, "Import-Hilfe", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
        $script:ImportHelpShown = $true
    }
}

# ========== XTF Import Dialog ==========
function Import-Xtf-Dialog {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Bitte zuerst ein Projekt erstellen.", "AuswertungPro")
        return
    }
    
    # Zeige Import-Hilfe beim ersten Import
    Show-ImportHelpIfNeeded
    
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "XTF-Dateien (*.xtf)|*.xtf|Alle Dateien|*.*"
    $dialog.Title = "XTF-Datei importieren (SIA405 oder VSA_KEK)"
    $dialog.InitialDirectory = (Join-Path $script:AppRoot "Rohdaten")
    
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $xtfPath = $dialog.FileName
        $script:lblStatus.Text = "Importiere XTF: $(Split-Path $xtfPath -Leaf)..."
        
        try {
            # Prüfe ob SIA405
            $isSIA405 = $xtfPath -match 'SIA405'
            $source = if ($isSIA405) { 'xtf405' } else { 'xtf' }
            
            # Parse XTF
            $parseResult = Parse-XtfFile -XtfPath $xtfPath
            
            if ($parseResult.Error) {
                [System.Windows.MessageBox]::Show("Fehler beim Parsen: $($parseResult.Error)", "Import-Fehler")
                return
            }
            
            Log-Info -Message "XTF geparst: $($parseResult.Haltungen.Count) Haltungen gefunden (SIA405=$($parseResult.IsSIA405), VSA=$($parseResult.IsVSA))" -Context "Import"
            
            # Debug: Zeige gefundene Haltungen
            if ($parseResult.Haltungen.Count -eq 0) {
                [System.Windows.MessageBox]::Show("Keine Haltungen in der XTF-Datei gefunden.`n`nDatei: $xtfPath`nSIA405: $($parseResult.IsSIA405)`nVSA_KEK: $($parseResult.IsVSA)", "Import-Warnung", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Warning)
                return
            }
            
            # Metadaten extrahieren
            $extractedMeta = Extract-MetadataFromXtf -XtfPath $xtfPath
            if ($extractedMeta.Count -gt 0) {
                $script:LastImportMetadata = $extractedMeta
                Merge-ProjectMetadata -Project $script:CurrentProject -ExtractedMetadata $extractedMeta -OverwriteEmpty $true
            }
            
            # Haltungen mergen
            $importedCount = 0
            $mergedCount = 0
            $script:SuppressFieldEvents = $true
            
            foreach ($haltungData in $parseResult.Haltungen) {
                $haltungsname = $haltungData.Felder['Haltungsname']
                
                # Suche bestehende Haltung
                $existingRecord = $script:CurrentProject.Data | Where-Object {
                    $_.GetFieldValue('Haltungsname') -eq $haltungsname
                } | Select-Object -First 1
                
                if ($existingRecord) {
                    # Merge in bestehende
                    foreach ($fieldName in $haltungData.Felder.Keys) {
                        $value = $haltungData.Felder[$fieldName]
                        if ($value) {
                            $currentValue = $existingRecord.GetFieldValue($fieldName)
                            $fieldMeta = $existingRecord.FieldMeta[$fieldName]
                            
                            if (-not $fieldMeta) {
                                $fieldMeta = New-Object FieldMetadata
                                $fieldMeta.FieldName = $fieldName
                                $existingRecord.FieldMeta[$fieldName] = $fieldMeta
                            }
                            
                            $mergeResult = Merge-Field -FieldName $fieldName `
                                -CurrentValue $currentValue `
                                -NewValue $value `
                                -FieldMeta $fieldMeta `
                                -NewSource $source `
                                -AllowConflicts $true
                            
                            if ($mergeResult.Merged) {
                                $existingRecord.SetFieldValue($fieldName, $mergeResult.NewValue, $source, $false)
                            }
                        }
                    }
                    $mergedCount++
                    Log-Debug -Message "Haltung gemerged: $haltungsname" -Context "Import"
                } else {
                    # Neue Haltung anlegen
                    $newRecord = $script:CurrentProject.CreateNewRecord()
                    foreach ($fieldName in $haltungData.Felder.Keys) {
                        $value = $haltungData.Felder[$fieldName]
                        if ($value) {
                            $newRecord.SetFieldValue($fieldName, $value, $source, $false)
                        }
                    }
                    $script:CurrentProject.AddRecord($newRecord)
                    $importedCount++
                    Log-Debug -Message "Neue Haltung erstellt: $haltungsname (NR: $($newRecord.GetFieldValue('NR')))" -Context "Import"
                }
            }
            
            $script:SuppressFieldEvents = $false
            
            # Grid aktualisieren
            Update-GridFromProject
            $script:CurrentProject.Dirty = $true
            
            Log-Info -Message "Import abgeschlossen: $importedCount neue Haltungen, $mergedCount aktualisiert, Total im Projekt: $($script:CurrentProject.Data.Count)" -Context "Import"
            
            # Import-History protokollieren
            $null = $script:CurrentProject.ImportHistory.Add(@{
                Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
                Source = $source
                File = $xtfPath
                RecordsImported = $importedCount
                RecordsMerged = $mergedCount
            })
            
            $script:lblStatus.Text = "XTF importiert: $($parseResult.Haltungen.Count) Haltungen ($importedCount neu, $mergedCount aktualisiert)"
            
            # Prüfe ob Stammdaten vorhanden sind
            $hasStammdaten = $false
            $missingFields = @()
            
            if ($parseResult.Haltungen.Count -gt 0) {
                $sampleHaltung = $parseResult.Haltungen[0].Felder
                $stammdatenFelder = @('Rohrmaterial', 'DN_mm', 'Nutzungsart')
                foreach ($feld in $stammdatenFelder) {
                    if ($sampleHaltung[$feld]) {
                        $hasStammdaten = $true
                    } else {
                        $missingFields += $feld
                    }
                }
            }
            
            # Zeige Import-Zusammenfassung mit Stammdaten-Hinweis
            $message = "XTF Import abgeschlossen`n`n"
            $message += "Datei: $(Split-Path $xtfPath -Leaf)`n"
            $message += "Typ: $(if ($parseResult.IsSIA405) { 'SIA405 (Stammdaten)' } else { 'VSA_KEK (Inspektionsdaten)' })`n`n"
            $message += "Haltungen gefunden: $($parseResult.Haltungen.Count)`n"
            $message += "- Neu erstellt: $importedCount`n"
            $message += "- Aktualisiert: $mergedCount"
            
            # Warnung bei fehlenden Stammdaten
            if (-not $hasStammdaten -and $missingFields.Count -gt 0 -and -not $parseResult.IsSIA405) {
                $message += "`n`n⚠ HINWEIS: Die XTF-Datei enthält keine Stammdaten.`n"
                $message += "Fehlende Felder: $($missingFields -join ', ')`n`n"
                $message += "Um Rohrmaterial, Durchmesser und Nutzungsart zu erhalten,`n"
                $message += "importieren Sie zusaetzlich die SIA405-Datei (*_SIA405.xtf).`n"
                $message += "Die Daten werden automatisch zusammengefuehrt."
            }
            
            [System.Windows.MessageBox]::Show(
                $message,
                "XTF Import",
                [System.Windows.MessageBoxButton]::OK,
                $(if ($hasStammdaten -or $parseResult.IsSIA405) { [System.Windows.MessageBoxImage]::Information } else { [System.Windows.MessageBoxImage]::Warning })
            )
            
            # Frage ob Projekt-Metadaten bearbeiten
            if ($extractedMeta.Count -gt 0) {
                $askResult = [System.Windows.MessageBox]::Show(
                    "$($extractedMeta.Count) Metadaten-Felder gefunden.`n`nMoechten Sie die Projekt-Metadaten bearbeiten?",
                    "Projekt-Metadaten",
                    [System.Windows.MessageBoxButton]::YesNo,
                    [System.Windows.MessageBoxImage]::Question
                )
                if ($askResult -eq [System.Windows.MessageBoxResult]::Yes) {
                    Show-ProjectMetadataDialog
                }
            }
            
            Log-Info -Message "XTF Import abgeschlossen: $xtfPath ($($parseResult.Haltungen.Count) Haltungen, $importedCount neu, $mergedCount aktualisiert)" -Context "Import"
        } catch {
            Log-Error -Message "XTF Import Fehler: $_" -Context "Import" -Exception $_
            [System.Windows.MessageBox]::Show("Fehler beim Import: $_", "Fehler")
        }
    }
}

# ========== PDF Import Dialog ==========
function Import-Pdf-Dialog {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Bitte zuerst ein Projekt erstellen.", "AuswertungPro")
        return
    }
    
    # Zeige Import-Hilfe beim ersten Import
    Show-ImportHelpIfNeeded
    
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "PDF-Dateien (*.pdf)|*.pdf|Alle Dateien|*.*"
    $dialog.Title = "PDF-Datei(en) importieren (Schritt 3: Details & Bemerkungen)"
    $dialog.Multiselect = $true
    
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $totalFound = 0
        $totalCreated = 0
        $totalUpdated = 0
        $totalConflicts = 0
        $totalErrors = 0
        $totalUncertain = 0
        $filesProcessed = 0
        $anyImported = $false

        $script:SuppressFieldEvents = $true
        
        foreach ($pdfPath in $dialog.FileNames) {
            $fileName = Split-Path $pdfPath -Leaf
            $script:lblStatus.Text = "PDF Analyse: $fileName..."
            
            try {
                Log-Info -Message "Starte PDF Batch-Import: $pdfPath" -Context "Import"

                $pages = ExtractPdfTextByPage -PdfPath $pdfPath
                if (-not $pages -or $pages.Count -eq 0) {
                    Log-Warn -Message "Keine Seiten extrahiert: $pdfPath" -Context "Import:PDF"
                    continue
                }

                $chunks = SplitIntoHaltungChunks -PagesText $pages
                if (-not $chunks -or $chunks.Count -eq 0) {
                    Log-Warn -Message "Keine Haltungs-Chunks erkannt: $pdfPath" -Context "Import:PDF"
                    continue
                }

                $uncertainChunks = $chunks | Where-Object { $_.IsUncertain }
                if ($uncertainChunks.Count -gt 0) {
                    $previewOk = Show-PdfImportPreview -PdfPath $pdfPath -Chunks $chunks
                    if (-not $previewOk) {
                        Log-Info -Message "PDF-Import abgebrochen (Preview): $pdfPath" -Context "Import:PDF"
                        continue
                    }
                }

                # Metadaten extrahieren
                $fullText = ($pages -join "`n`n")
                $extractedMeta = Extract-MetadataFromPdf -PdfText $fullText -PdfPath $pdfPath
                if ($extractedMeta.Count -gt 0) {
                    $script:LastImportMetadata = $extractedMeta
                    Merge-ProjectMetadata -Project $script:CurrentProject -ExtractedMetadata $extractedMeta -OverwriteEmpty $true
                }

                # Batch-Import
                $script:lblStatus.Text = "PDF Import: $fileName..."
                $importStats = ImportPdfBatch -PdfPath $pdfPath -Project $script:CurrentProject -Chunks $chunks

                $filesProcessed++
                $totalFound += $importStats.Found
                $totalCreated += $importStats.Created
                $totalUpdated += $importStats.UpdatedRecords
                $totalConflicts += $importStats.Conflicts
                $totalErrors += $importStats.Errors
                $totalUncertain += $importStats.Uncertain
                $anyImported = $true

                # Import-History
                $null = $script:CurrentProject.ImportHistory.Add(@{
                    Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
                    Source = 'pdf'
                    File = $pdfPath
                    RecordsFound = $importStats.Found
                    RecordsCreated = $importStats.Created
                    RecordsUpdated = $importStats.UpdatedRecords
                    Conflicts = $importStats.Conflicts
                    Errors = $importStats.Errors
                })

                Write-ImportLog -ImportType "PDF" -FilePath $pdfPath -CountCreated $importStats.Created -CountUpdated $importStats.UpdatedRecords -CountConflicts $importStats.Conflicts -CountErrors $importStats.Errors
                
            } catch {
                $totalErrors++
                Log-Error -Message "PDF Batch-Import Fehler bei $fileName : $_" -Context "Import" -Exception $_
            }
        }
        
        $script:SuppressFieldEvents = $false
        if ($anyImported) {
            Update-GridFromProject
            $script:CurrentProject.Dirty = $true
        }
        
        # Status-Zusammenfassung
        $script:lblStatus.Text = "PDF Batch-Import: $totalFound Haltungen ($totalCreated neu, $totalUpdated aktualisiert)"
        
        # Zeige Import-Zusammenfassung
        $summaryMessage = @()
        $summaryMessage += "PDF Batch-Import abgeschlossen"
        $summaryMessage += ""
        $summaryMessage += "Dateien verarbeitet: $filesProcessed"
        $summaryMessage += ""
        $summaryMessage += "Ergebnis:"
        $summaryMessage += "- Haltungen gesamt: $totalFound"
        $summaryMessage += "  - Neu erstellt: $totalCreated"
        $summaryMessage += "  - Aktualisiert: $totalUpdated"
        $summaryMessage += "- Konflikte (UserEdited): $totalConflicts"
        $summaryMessage += "- Fehler: $totalErrors"
        if ($totalUncertain -gt 0) {
            $summaryMessage += "- Unsicher erkannt: $totalUncertain"
        }
        
        if ($totalConflicts -gt 0) {
            $summaryMessage += ""
            $summaryMessage += "Hinweis: Konflikte wurden nicht ueberschrieben."
        }
        
        $icon = if ($totalErrors -eq 0) { 
            [System.Windows.MessageBoxImage]::Information 
        } else { 
            [System.Windows.MessageBoxImage]::Warning 
        }
        
        [System.Windows.MessageBox]::Show(($summaryMessage -join "`r`n"), "PDF Batch-Import Ergebnis", 
            [System.Windows.MessageBoxButton]::OK, $icon)

        if ($totalConflicts -gt 0 -and $script:CurrentProject.Conflicts.Count -gt 0) {
            $showConflicts = [System.Windows.MessageBox]::Show(
                "Es gibt Konflikte. Details anzeigen?",
                "Konflikte",
                [System.Windows.MessageBoxButton]::YesNo,
                [System.Windows.MessageBoxImage]::Warning
            )
            if ($showConflicts -eq [System.Windows.MessageBoxResult]::Yes) {
                $conflictText = Get-ConflictSummary -Conflicts $script:CurrentProject.Conflicts
                [System.Windows.MessageBox]::Show($conflictText, "Konflikte (Details)")
            }
        }
        
        # Frage ob Projekt-Metadaten
        if ($script:LastImportMetadata -and $script:LastImportMetadata.Count -gt 0) {
            $askResult = [System.Windows.MessageBox]::Show(
                "Metadaten aus PDF gefunden.`n`nMoechten Sie die Projekt-Metadaten bearbeiten?",
                "Projekt-Metadaten",
                [System.Windows.MessageBoxButton]::YesNo,
                [System.Windows.MessageBoxImage]::Question
            )
            if ($askResult -eq [System.Windows.MessageBoxResult]::Yes) {
                Show-ProjectMetadataDialog
            }
        }
        
        Log-Info -Message "PDF Batch-Import abgeschlossen: $($dialog.FileNames.Count) Dateien, $totalFound Haltungen" -Context "Import"
    }
}

function Show-PdfImportPreview {
    param(
        [string] $PdfPath,
        [object[]] $Chunks
    )
    
    $window = New-Object System.Windows.Window
    $window.Title = "PDF Import Vorschau"
    $window.Width = 700
    $window.Height = 450
    $window.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterOwner
    if ($script:MainWindow) { $window.Owner = $script:MainWindow }
    
    $grid = New-Object System.Windows.Controls.Grid
    $null = $grid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "Auto" }))
    $null = $grid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "*" }))
    $null = $grid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "Auto" }))
    
    $header = New-Object System.Windows.Controls.TextBlock
    $header.Text = "Unsichere PDF-Zuordnung erkannt: $(Split-Path $PdfPath -Leaf)"
    $header.Margin = [System.Windows.Thickness]::new(12, 10, 12, 8)
    $header.FontSize = 12
    $header.FontWeight = [System.Windows.FontWeights]::SemiBold
    [System.Windows.Controls.Grid]::SetRow($header, 0)
    
    $dataGrid = New-Object System.Windows.Controls.DataGrid
    $dataGrid.AutoGenerateColumns = $false
    $dataGrid.IsReadOnly = $true
    $dataGrid.HeadersVisibility = [System.Windows.Controls.DataGridHeadersVisibility]::Column
    $dataGrid.RowHeight = 24
    $dataGrid.Margin = [System.Windows.Thickness]::new(12, 0, 12, 8)
    
    $colIndex = New-Object System.Windows.Controls.DataGridTextColumn
    $colIndex.Header = "#"
    $colIndex.Binding = New-Object System.Windows.Data.Binding -ArgumentList "Index"
    $colIndex.Width = 50
    
    $colId = New-Object System.Windows.Controls.DataGridTextColumn
    $colId.Header = "Haltung-ID"
    $colId.Binding = New-Object System.Windows.Data.Binding -ArgumentList "HaltungId"
    $colId.Width = 250
    
    $colPages = New-Object System.Windows.Controls.DataGridTextColumn
    $colPages.Header = "Seiten"
    $colPages.Binding = New-Object System.Windows.Data.Binding -ArgumentList "Seiten"
    $colPages.Width = 80
    
    $colStatus = New-Object System.Windows.Controls.DataGridTextColumn
    $colStatus.Header = "Status"
    $colStatus.Binding = New-Object System.Windows.Data.Binding -ArgumentList "Status"
    $colStatus.Width = 100
    
    $null = $dataGrid.Columns.Add($colIndex)
    $null = $dataGrid.Columns.Add($colId)
    $null = $dataGrid.Columns.Add($colPages)
    $null = $dataGrid.Columns.Add($colStatus)
    
    $items = New-Object System.Collections.ObjectModel.ObservableCollection[PSObject]
    foreach ($chunk in $Chunks) {
        $status = if ($chunk.IsUncertain -or -not $chunk.DetectedId) { "Unsicher" } else { "OK" }
        $id = if ($chunk.DetectedId) { $chunk.DetectedId } else { "(keine ID)" }
        $pageRange = if ($chunk.PageRange) { $chunk.PageRange } else { "" }
        $item = [PSCustomObject]@{
            Index = $chunk.Index
            HaltungId = $id
            Seiten = $pageRange
            Status = $status
        }
        $null = $items.Add($item)
    }
    $dataGrid.ItemsSource = $items
    [System.Windows.Controls.Grid]::SetRow($dataGrid, 1)
    
    $buttonPanel = New-Object System.Windows.Controls.StackPanel
    $buttonPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $buttonPanel.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    $buttonPanel.Margin = [System.Windows.Thickness]::new(12, 0, 12, 10)
    
    $btnImport = New-Object System.Windows.Controls.Button
    $btnImport.Content = "Importieren"
    $btnImport.Padding = [System.Windows.Thickness]::new(14, 6, 14, 6)
    $btnImport.Margin = [System.Windows.Thickness]::new(0, 0, 8, 0)
    $btnImport.Add_Click({
        $window.DialogResult = $true
        $window.Close()
    })
    
    $btnCancel = New-Object System.Windows.Controls.Button
    $btnCancel.Content = "Abbrechen"
    $btnCancel.Padding = [System.Windows.Thickness]::new(14, 6, 14, 6)
    $btnCancel.Add_Click({
        $window.DialogResult = $false
        $window.Close()
    })
    
    $null = $buttonPanel.Children.Add($btnImport)
    $null = $buttonPanel.Children.Add($btnCancel)
    [System.Windows.Controls.Grid]::SetRow($buttonPanel, 2)
    
    $null = $grid.Children.Add($header)
    $null = $grid.Children.Add($dataGrid)
    $null = $grid.Children.Add($buttonPanel)
    
    $window.Content = $grid
    return ($window.ShowDialog() -eq $true)
}

# ========== Excel Export Dialog ==========
function Export-Excel-Dialog {
    if (-not $script:CurrentProject) {
        [System.Windows.MessageBox]::Show("Kein Projekt geladen.", "AuswertungPro")
        return
    }
    
    # Prüfe ob Vorlage existiert
    $useTemplate = Test-ExcelTemplateExists
    $templateInfo = ""
    if ($useTemplate) {
        $templateInfo = "`n(Verwendet Vorlage: Export_Vorlage\Haltungen.xlsx)"
    }
    
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Filter = "Excel-Dateien (*.xlsx)|*.xlsx"
    $dialog.Title = "Nach Excel exportieren$templateInfo"
    $dialog.FileName = "$($script:CurrentProject.Name)_Export.xlsx"
    
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $script:lblStatus.Text = "Exportiere nach Excel..."
        
        try {
            $success = $false
            
            if ($useTemplate) {
                # Export mit Vorlage (Kopie der Vorlage + Daten einfügen)
                $success = Export-ProjectWithTemplate -Project $script:CurrentProject -OutputPath $dialog.FileName -StartRow 2
            } else {
                # Standard-Export (ohne Vorlage)
                $success = Export-ProjectToExcel -Project $script:CurrentProject -OutputPath $dialog.FileName
            }
            
            if ($success) {
                $script:lblStatus.Text = "Excel exportiert: $($script:CurrentProject.Data.Count) Haltungen"
                $msg = "Export erfolgreich!`n`n$($dialog.FileName)"
                if ($useTemplate) {
                    $msg += "`n`n(Mit Vorlage exportiert)"
                }
                [System.Windows.MessageBox]::Show($msg, "Excel Export", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information)
            } else {
                [System.Windows.MessageBox]::Show("Export fehlgeschlagen. Siehe Log für Details.", "Fehler", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error)
            }
        } catch {
            Log-Error -Message "Excel Export Fehler: $_" -Context "Export" -Exception $_
            [System.Windows.MessageBox]::Show("Fehler beim Export: $_", "Fehler")
        }
    }
}

function Show-SearchDialog { $script:lblStatus.Text = "[TODO] Suche Dialog" }
function Reset-GridFilter { $script:lblStatus.Text = "Filter zurückgesetzt" }

# ========== Hauptprogramm ==========
try {
    $window = New-MainWindow
    $script:MainWindow = $window
    
    # Starte Autosave (einfacher Aufruf - Timer holt Projekt dynamisch)
    Start-Autosave -Project $null -SaveCallback $null
    
    Log-Info -Message "AuswertungPro v$script:AppVersion gestartet" -Context "Main"
    
    $window.ShowDialog() | Out-Null
} catch {
    $errMsg = $_.Exception.Message
    Write-Host "Kritischer Fehler: $errMsg" -ForegroundColor Red
    [System.Windows.MessageBox]::Show("Fehler: $errMsg", "Fehler")
    exit 1
}
