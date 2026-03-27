<#
.SYNOPSIS
    ValidationService für AuswertungPro - Eingabe-Validierung
.DESCRIPTION
    Validiert und normalisiert Feldwerte für alle 25 Spalten
#>

# ========== Validierungsfunktionen ==========
<#
.SYNOPSIS
    Validate-FieldValue: Validiert einen Feldwert nach Feldtyp
.RETURNS
    PSCustomObject mit IsValid, Error, NormalizedValue
#>
function Validate-FieldValue {
    param(
        [string] $FieldName,
        [string] $Value,
        [bool] $AllowEmpty = $true
    )
    
    $result = @{
        IsValid = $true
        Error = ""
        NormalizedValue = ""
    }
    
    # Leere Werte zulassen?
    if ([string]::IsNullOrWhiteSpace($Value)) {
        if ($AllowEmpty) {
            $result.NormalizedValue = ""
            return $result
        } else {
            $result.IsValid = $false
            $result.Error = "Feld ist erforderlich"
            return $result
        }
    }
    
    $fieldType = Get-FieldType -FieldName $FieldName
    
    switch ($fieldType) {
        'int' {
            $normalized = Normalize-IntegerValue -Value $Value
            if ($null -eq $normalized) {
                $result.IsValid = $false
                $result.Error = "Keine gültige Ganzzahl"
            } else {
                # Feldspezifische Validierung
                switch ($FieldName) {
                    'NR' {
                        if ($normalized -lt 0) {
                            $result.IsValid = $false
                            $result.Error = "NR muss ≥ 0 sein"
                        }
                    }
                    'DN_mm' {
                        if ($normalized -lt 20 -or $normalized -gt 3000) {
                            # Warn aber akzeptiere (tolerant)
                            Log-Warn -Message "DN_mm = $normalized (empfohlen: 20-3000)" -Context "Validation"
                        }
                    }
                    default {
                        if ($normalized -lt 0) {
                            $result.IsValid = $false
                            $result.Error = "Wert muss ≥ 0 sein"
                        }
                    }
                }
                $result.NormalizedValue = $normalized.ToString()
            }
        }
        
        'decimal' {
            $normalized = Normalize-DecimalValue -Value $Value
            if ($null -eq $normalized) {
                $result.IsValid = $false
                $result.Error = "Keine gültige Dezimalzahl"
            } else {
                if ($normalized -lt 0) {
                    $result.IsValid = $false
                    $result.Error = "Wert muss ≥ 0 sein"
                } else {
                    $result.NormalizedValue = $normalized.ToString("F2")
                }
            }
        }
        
        'combo' {
            $normalized = Normalize-ComboValue -FieldName $FieldName -Value $Value
            if ([string]::IsNullOrEmpty($normalized)) {
                $result.IsValid = $false
                $result.Error = "Ungültige Auswahl"
            } else {
                $result.NormalizedValue = $normalized
            }
        }
        
        'multiline' {
            # Kein Trimmen nötig, aber Normalisierung
            $normalized = Normalize-MultilineValue -Value $Value
            $result.NormalizedValue = $normalized
        }
        
        'text' {
            $normalized = $Value.Trim()
            
            # Feldspezifische Validierung
            switch ($FieldName) {
                'Link' {
                    if (-not [string]::IsNullOrEmpty($normalized)) {
                        if (-not ($normalized -match '^\s*https?://')) {
                            # Warn aber akzeptiere
                            Log-Warn -Message "Link scheint keine HTTP(S)-URL zu sein: $normalized" -Context "Validation"
                        }
                    }
                }
                'Datum_Jahr' {
                    # Erlaubt: dd.mm.yyyy oder yyyy
                    if (-not [string]::IsNullOrEmpty($normalized)) {
                        if (-not ($normalized -match '^\d{4}$|^\d{1,2}\.\d{1,2}\.\d{2,4}$')) {
                            $result.IsValid = $false
                            $result.Error = "Format: dd.mm.yyyy oder yyyy"
                        }
                    }
                }
            }
            
            $result.NormalizedValue = $normalized
        }
    }
    
    return $result
}

# ========== Normalisierungsfunktionen ==========
function Normalize-IntegerValue {
    param([string] $Value)
    
    try {
        $Value = $Value.Trim()
        
        # Entferne Tausendertrenner
        $Value = $Value -replace "['\s]", ""
        
        # Versuche zu parsen
        [int]::Parse($Value, [System.Globalization.NumberStyles]::Integer)
    } catch {
        return $null
    }
}

function Normalize-DecimalValue {
    param([string] $Value)
    
    try {
        $Value = $Value.Trim()
        
        # Entferne Tausendertrenner und Währungs-Symbole
        $Value = $Value -replace "['\s]", ""
        $Value = $Value -replace "CHF|Fr\.|Sfr\.", ""
        
        # Ersetze Komma durch Punkt
        $Value = $Value -replace ",", "."
        
        [decimal]::Parse($Value, [System.Globalization.NumberStyles]::AllowDecimalPoint -bor [System.Globalization.NumberStyles]::AllowLeadingSign)
    } catch {
        return $null
    }
}

function Normalize-ComboValue {
    param(
        [string] $FieldName,
        [string] $Value
    )
    
    $Value = $Value.Trim()
    
    $items = Get-ComboBoxItems -FieldName $FieldName
    if ($items.Count -eq 0) {
        return $Value
    }
    
    # Case-insensitive match
    $match = $items | Where-Object { $_ -eq $Value -or $_ -like "$Value*" }
    if ($match) {
        return $match[0]
    }
    
    # Kein Match gefunden
    return $null
}

function Normalize-MultilineValue {
    param([string] $Value)
    
    $Value = $Value.Trim()
    
    # Normalisiere Zeilenumbrüche
    $Value = $Value -replace "`r`n", "`n"
    $Value = $Value -replace "`r", "`n"
    
    # Entferne doppelte Zeilenumbrüche
    while ($Value -match "`n`n") {
        $Value = $Value -replace "`n`n", "`n"
    }
    
    return $Value
}

function Normalize-CostValue {
    param([string] $Value)
    
    $normalized = Normalize-DecimalValue -Value $Value
    return if ($null -eq $normalized) { "" } else { $normalized.ToString("F2") }
}

function Normalize-DateValue {
    param([string] $Value)
    
    try {
        $Value = $Value.Trim()
        
        # Versuche zu parsen als dd.mm.yyyy
        if ($Value -match '^(\d{1,2})\.(\d{1,2})\.(\d{2,4})$') {
            $matches = [regex]::Matches($Value, '^(\d{1,2})\.(\d{1,2})\.(\d{2,4})$')[0]
            $day = [int]$matches.Groups[1].Value
            $month = [int]$matches.Groups[2].Value
            $year = [int]$matches.Groups[3].Value
            
            # 2-stelliges Jahr expandieren
            if ($year -lt 100) {
                $year = if ($year -le 30) { 2000 + $year } else { 1900 + $year }
            }
            
            $date = [datetime]::new($year, $month, $day)
            return $date.ToString("dd.MM.yyyy")
        }
        
        # Versuche zu parsen als yyyy
        if ($Value -match '^\d{4}$') {
            $year = [int]$Value
            if ($year -ge 1900 -and $year -le 2100) {
                return $year.ToString("D4")
            }
        }
        
        # Versuche standar-Parsing
        $date = [datetime]::Parse($Value)
        return $date.ToString("dd.MM.yyyy")
    } catch {
        return $Value  # Fallback: original value
    }
}

# ========== Batch-Validierung ==========
function Validate-Record {
    param([HaltungRecord] $Record)
    
    $errors = @()
    
    foreach ($fieldName in $script:FieldColumnOrder) {
        $value = $Record.GetFieldValue($fieldName)
        $allowEmpty = $fieldName -ne 'Haltungsname'  # Haltungsname ist erforderlich
        
        $validation = Validate-FieldValue -FieldName $fieldName -Value $value -AllowEmpty $allowEmpty
        
        if (-not $validation.IsValid) {
            $errors += @{
                FieldName = $fieldName
                Value = $value
                Error = $validation.Error
            }
        }
    }
    
    return $errors
}

# ========== Export ==========
Write-Host "[ValidationService] Loaded" -ForegroundColor Green
