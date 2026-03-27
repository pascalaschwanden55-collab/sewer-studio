[CmdletBinding()]
param()

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# ========== Styling-Funktion für Buttons ==========
function Set-UiButtonStyle {
    param(
        [System.Windows.Forms.Button]$Button,
        [int]$Width = 120,
        [int]$Height = 34
    )

    $Button.AutoSize = $false
    $Button.Size = New-Object System.Drawing.Size($Width, $Height)
    $Button.MinimumSize = New-Object System.Drawing.Size($Width, $Height)
    $Button.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
    $Button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $Button.FlatAppearance.BorderSize = 0
    $Button.BackColor = [System.Drawing.Color]::White
    $Button.ForeColor = [System.Drawing.Color]::Black
    $Button.UseVisualStyleBackColor = $false
}

# ========== XML-Parsing Funktionen ==========
function Get-ChildText {
    param([System.Xml.XmlNode]$Node, [string]$LocalName)
    $child = $Node.SelectSingleNode("./*[local-name()='$LocalName']")
    if ($null -eq $child) { return "" }
    return $child.InnerText
}

function Get-RefAttr {
    param([System.Xml.XmlNode]$Node, [string]$LocalName)
    $child = $Node.SelectSingleNode("./*[local-name()='$LocalName']")
    if ($null -eq $child) { return "" }
    return $child.GetAttribute("REF")
}

function Import-SIA405 {
    param([string]$FilePath)
    
    [xml]$xml = Get-Content -Path $FilePath -Encoding UTF8
    
    $kanalNodes = $xml.SelectNodes("//*[contains(local-name(),'.Kanal')]")
    $knotenNodes = $xml.SelectNodes("//*[contains(local-name(),'.Abwasserknoten')]")
    $haltungspunktNodes = $xml.SelectNodes("//*[contains(local-name(),'.Haltungspunkt')]")
    $haltungNodes = $xml.SelectNodes("//*[contains(local-name(),'.Haltung')]")
    
    $kanalByTid = @{}
    foreach ($n in $kanalNodes) {
        $tid = $n.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $kanalByTid[$tid] = @{
            Bezeichnung = (Get-ChildText -Node $n -LocalName "Bezeichnung")
            Nutzungsart = (Get-ChildText -Node $n -LocalName "Nutzungsart_Ist")
        }
    }
    
    $knotenByTid = @{}
    foreach ($n in $knotenNodes) {
        $tid = $n.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $knotenByTid[$tid] = (Get-ChildText -Node $n -LocalName "Bezeichnung")
    }
    
    $haltungspunktByTid = @{}
    foreach ($n in $haltungspunktNodes) {
        $tid = $n.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $haltungspunktByTid[$tid] = (Get-RefAttr -Node $n -LocalName "AbwassernetzelementRef")
    }
    
    $rows = @()
    $nr = 1
    foreach ($n in $haltungNodes) {
        if ($n.LocalName -notlike "*.Haltung") { continue }
        $bezeichnung = (Get-ChildText -Node $n -LocalName "Bezeichnung")
        if ([string]::IsNullOrWhiteSpace($bezeichnung)) { continue }
        
        $laenge = (Get-ChildText -Node $n -LocalName "LaengeEffektiv")
        $dn = (Get-ChildText -Node $n -LocalName "Lichte_Hoehe")
        $material = (Get-ChildText -Node $n -LocalName "Material")
        $kanalRef = (Get-RefAttr -Node $n -LocalName "AbwasserbauwerkRef")
        $nutzungsart = ""
        if ($kanalByTid.ContainsKey($kanalRef)) {
            $nutzungsart = $kanalByTid[$kanalRef].Nutzungsart
        }
        
        $vonRef = (Get-RefAttr -Node $n -LocalName "vonHaltungspunktRef")
        $nachRef = (Get-RefAttr -Node $n -LocalName "nachHaltungspunktRef")
        $vonKnoten = ""
        $nachKnoten = ""
        if ($haltungspunktByTid.ContainsKey($vonRef)) {
            $knotenTid = $haltungspunktByTid[$vonRef]
            if ($knotenByTid.ContainsKey($knotenTid)) { $vonKnoten = $knotenByTid[$knotenTid] }
        }
        if ($haltungspunktByTid.ContainsKey($nachRef)) {
            $knotenTid = $haltungspunktByTid[$nachRef]
            if ($knotenByTid.ContainsKey($knotenTid)) { $nachKnoten = $knotenByTid[$knotenTid] }
        }
        
        $rows += [PSCustomObject]@{
            NR = $nr
            Ausführung_durch = ""
            Haltungsname = $bezeichnung
            Strasse = ""
            Rohrmaterial = $material
            "DN mm" = $dn
            Nutzungsart = $nutzungsart
            "Haltungslänge m" = $laenge
            Fliessrichtung = ""
            "Primäre Schäden" = ""
            Zustandsklasse = ""
            Prüfungsresultat = ""
            Sanieren = ""
            "Empfohlene Massnahmen" = ""
            Kosten = ""
            Eigentümer = ""
            Bemerkungen = ""
            Link = ""
            "Von Schacht" = $vonKnoten
            "Bis Schacht" = $nachKnoten
        }
        $nr++
    }
    
    return $rows
}

function Export-ToExcel {
    param([array]$Rows, [string]$OutputPath)
    
    $headers = @(
        "NR","Ausführung_durch","Haltungsname","Strasse","Rohrmaterial","DN mm","Nutzungsart",
        "Haltungslänge m","Fliessrichtung","Primäre Schäden","Zustandsklasse","Prüfungsresultat",
        "Sanieren","Empfohlene Massnahmen","Kosten","Eigentümer","Bemerkungen","Link",
        "Von Schacht","Bis Schacht"
    )
    
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $wb = $excel.Workbooks.Add()
    $ws = $wb.Worksheets.Item(1)
    $ws.Name = "Haltungen"
    
    for ($c = 0; $c -lt $headers.Count; $c++) {
        $ws.Cells.Item(1, $c + 1) = $headers[$c]
        $ws.Cells.Item(1, $c + 1).Font.Bold = $true
    }
    
    $rowIndex = 2
    foreach ($row in $Rows) {
        for ($c = 0; $c -lt $headers.Count; $c++) {
            $key = $headers[$c]
            $ws.Cells.Item($rowIndex, $c + 1) = $row.$key
        }
        $rowIndex++
    }
    
    $ws.Columns.AutoFit() | Out-Null
    $wb.SaveAs($OutputPath)
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}

# ========== GUI ==========
$form = New-Object System.Windows.Forms.Form
$form.Text = "Haltungen Auswertung Tool"
$form.Width = 1400
$form.Height = 700
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = [System.Drawing.Color]::White

# Panel für Buttons
$btnPanel = New-Object System.Windows.Forms.Panel
$btnPanel.Dock = [System.Windows.Forms.DockStyle]::Top
$btnPanel.Height = 50
$btnPanel.BackColor = [System.Drawing.Color]::White
$btnPanel.Padding = New-Object System.Windows.Forms.Padding(10, 8, 10, 8)

$btnFlow = New-Object System.Windows.Forms.FlowLayoutPanel
$btnFlow.Dock = [System.Windows.Forms.DockStyle]::Fill
$btnFlow.WrapContents = $false
$btnFlow.FlowDirection = [System.Windows.Forms.FlowDirection]::LeftToRight

$btnImport = New-Object System.Windows.Forms.Button
$btnImport.Text = "XTF importieren"
Set-UiButtonStyle -Button $btnImport -Width 130 -Height 34

$btnExport = New-Object System.Windows.Forms.Button
$btnExport.Text = "Excel exportieren"
Set-UiButtonStyle -Button $btnExport -Width 130 -Height 34

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = "Bereit"
$lblStatus.AutoSize = $true
$lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen

$btnFlow.Controls.Add($btnImport)
$btnFlow.Controls.Add($btnExport)
$btnFlow.Controls.Add($lblStatus)
$btnPanel.Controls.Add($btnFlow)

# DataGridView für Haltungen
$grid = New-Object System.Windows.Forms.DataGridView
$grid.Dock = [System.Windows.Forms.DockStyle]::Fill
$grid.AutoSizeColumnsMode = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::AllCells
$grid.AllowUserToAddRows = $false
$grid.AllowUserToDeleteRows = $false
$grid.RowHeadersWidth = 30
$grid.DefaultCellStyle.Font = New-Object System.Drawing.Font("Arial", 9)

$form.Controls.Add($grid)
$form.Controls.Add($btnPanel)

# Global data
$script:Data = @()

# ========== Button Events ==========
$btnImport.Add_Click({
    $openFileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $openFileDialog.Filter = "XTF Dateien (*.xtf)|*.xtf|Alle Dateien (*.*)|*.*"
    $openFileDialog.InitialDirectory = "F:\AuswertungPro\Rohdaten"
    
    if ($openFileDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        try {
            $lblStatus.Text = "Laden..."
            $lblStatus.ForeColor = [System.Drawing.Color]::Orange
            $form.Refresh()
            
            $script:Data = Import-SIA405 -FilePath $openFileDialog.FileName
            
            $grid.DataSource = $null
            $bindingSource = New-Object System.Windows.Forms.BindingSource
            $bindingSource.DataSource = $script:Data
            $grid.DataSource = $bindingSource
            
            $lblStatus.Text = "✓ $($script:Data.Count) Haltungen geladen"
            $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
        }
        catch {
            $lblStatus.Text = "✗ Fehler beim Laden: $($_.Message)"
            $lblStatus.ForeColor = [System.Drawing.Color]::Red
        }
    }
})

$btnExport.Add_Click({
    if ($script:Data.Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("Keine Daten zum Exportieren!", "Info")
        return
    }
    
    $saveFileDialog = New-Object System.Windows.Forms.SaveFileDialog
    $saveFileDialog.Filter = "Excel Dateien (*.xlsx)|*.xlsx"
    $saveFileDialog.InitialDirectory = "F:\AuswertungPro\Tabellen"
    $saveFileDialog.FileName = "Haltungen_$(Get-Date -Format 'yyyyMMdd').xlsx"
    
    if ($saveFileDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        try {
            $lblStatus.Text = "Exportiere..."
            $form.Refresh()
            
            # DataGridView Daten aktualisieren
            foreach ($row in $grid.Rows) {
                for ($i = 0; $i -lt $grid.ColumnCount; $i++) {
                    $colName = $grid.Columns[$i].Name
                    if ($row.Index -lt $script:Data.Count) {
                        $script:Data[$row.Index].$colName = $row.Cells[$i].Value
                    }
                }
            }
            
            Export-ToExcel -Rows $script:Data -OutputPath $saveFileDialog.FileName
            
            $lblStatus.Text = "✓ Exportiert: $($saveFileDialog.FileName)"
            $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
            [System.Windows.Forms.MessageBox]::Show("Erfolgreich exportiert!", "Erfolg")
        }
        catch {
            $lblStatus.Text = "✗ Fehler beim Export: $($_.Message)"
            $lblStatus.ForeColor = [System.Drawing.Color]::Red
            [System.Windows.Forms.MessageBox]::Show($_.Message, "Fehler")
        }
    }
})

# Show Form
$form.ShowDialog() | Out-Null
