<#
.SYNOPSIS
    MergeService für AuswertungPro - Zentrale Merge-Logik
.DESCRIPTION
    Implementiert die Kernregeln:
    - UserEdited Felder NIEMALS überschreiben
    - Priorität: manual > pdf > xtf405 > xtf
    - Konflikte protokollieren statt zu fehlen
#>

# ========== Konstanten ==========
$script:SourcePriority = @{
    'manual'  = 10
    'pdf'     = 7
    'xtf405'  = 5
    'xtf'     = 3
}

# ========== Core Merge Logic ==========
<#
.SYNOPSIS
    Merge-Field: Zentrale Merge-Logik für ein einzelnes Feld
.DESCRIPTION
    Entscheidet, ob ein Feld überschrieben wird und markiert Konflikte.
    
    Regeln:
    1. Wenn FieldMeta.UserEdited = $true → NEVER OVERWRITE, mark conflict
    2. Wenn currentValue leer und newSource existiert → SET
    3. Wenn currentValue nicht leer und Priorität:
       - newSource > currentSource → UPDATE
       - sonst → mark conflict oder skip

.PARAMETER CurrentValue
    Aktueller Wert im Feld (kann leer sein)

.PARAMETER NewValue
    Neuer Wert vom Import

.PARAMETER FieldMeta
    FieldMetadata mit Source, UserEdited, LastUpdated

.PARAMETER NewSource
    Quelle des neuen Wertes: "xtf", "xtf405", "pdf", "manual"

.PARAMETER AllowConflicts
    Wenn $true: Konflikte protokollieren statt zu fehlen (best-effort)

.OUTPUTS
    PSCustomObject mit:
    - Merged: $true/$false (wurde überschrieben?)
    - NewValue: String (neuer Wert, falls Merged=$true)
    - Conflict: Hashtable oder $null
    - Message: String (Grund für Entscheidung)
#>
function Merge-Field {
    param(
        [string] $FieldName,
        [string] $CurrentValue = "",
        [string] $NewValue = "",
        [FieldMetadata] $FieldMeta,
        [string] $NewSource = "xtf",
        [bool] $AllowConflicts = $true
    )
    
    # Validierung
    if ([string]::IsNullOrEmpty($FieldName)) {
        throw "FieldName erforderlich"
    }
    
    if (-not $script:SourcePriority.ContainsKey($NewSource)) {
        throw "Unbekannte Source: $NewSource (erlaubt: $($script:SourcePriority.Keys -join ', '))"
    }
    
    # Normalize values (PS 5.1 kompatibel)
    if ($null -eq $CurrentValue) { $CurrentValue = "" }
    if ($null -eq $NewValue) { $NewValue = "" }
    $CurrentValue = [string]$CurrentValue.Trim()
    $NewValue = [string]$NewValue.Trim()
    
    # Wenn NewValue leer, kein Merge nötig
    if ([string]::IsNullOrEmpty($NewValue)) {
        return @{
            Merged = $false
            NewValue = $CurrentValue
            Conflict = $null
            Message = "NewValue ist leer"
        }
    }
    
    # === REGEL 1: UserEdited = $true → NIE ÜBERSCHREIBEN ===
    if ($FieldMeta.UserEdited) {
        $conflict = @{
            FieldName = $FieldName
            Source = $NewSource
            CurrentValue = $CurrentValue
            NewValue = $NewValue
            Reason = "Nutzer hat Feld editiert, nicht überschrieben"
            Timestamp = (Get-Date).ToUniversalTime()
        }
        
        if ($AllowConflicts) {
            Log-Warn -Message "Konflikt: Feld '$FieldName' wurde nicht überschrieben (UserEdited=true)" -Context "Merge"
            return @{
                Merged = $false
                NewValue = $CurrentValue
                Conflict = $conflict
                Message = "UserEdited=true, nicht überschrieben"
            }
        } else {
            throw "Konflikt: Feld '$FieldName' von Nutzer editiert, aber Überschreibung verlangt"
        }
    }
    
    # === REGEL 2: Aktueller Wert ist leer → EINFACH FÜLLEN ===
    if ([string]::IsNullOrEmpty($CurrentValue)) {
        # Update metadata
        $FieldMeta.Source = $NewSource
        $FieldMeta.UserEdited = $false
        $FieldMeta.LastUpdated = (Get-Date).ToUniversalTime()
        
        Log-Info -Message "Feld '$FieldName' gefüllt von Source '$NewSource'" -Context "Merge"
        return @{
            Merged = $true
            NewValue = $NewValue
            Conflict = $null
            Message = "Leeres Feld gefüllt"
        }
    }
    
    # === REGEL 3: Beide Werte befüllt → PRIORITÄT PRÜFEN ===
    $currentPriority = $script:SourcePriority[$FieldMeta.Source]
    $newPriority = $script:SourcePriority[$NewSource]
    
    if ($NewValue -eq $CurrentValue) {
        # Wert ist identisch, kein Update nötig
        Log-Debug -Message "Feld '$FieldName' hat identischen Wert von Source '$NewSource'" -Context "Merge"
        return @{
            Merged = $false
            NewValue = $CurrentValue
            Conflict = $null
            Message = "Identischer Wert"
        }
    }
    
    # Werte unterscheiden sich
    if ($newPriority -gt $currentPriority) {
        # Neue Quelle hat höhere Priorität → UPDATE
        $FieldMeta.Source = $NewSource
        $FieldMeta.UserEdited = $false
        $FieldMeta.LastUpdated = (Get-Date).ToUniversalTime()
        
        Log-Info -Message "Feld '$FieldName' aktualisiert: '$CurrentValue' → '$NewValue' (Priorität: $($FieldMeta.Source) → $NewSource)" -Context "Merge"
        return @{
            Merged = $true
            NewValue = $NewValue
            Conflict = $null
            Message = "Höhere Priorität, überschrieben"
        }
    } else {
        # Neue Quelle hat niedrigere Priorität → KONFLIKT
        $conflict = @{
            FieldName = $FieldName
            Source = $NewSource
            CurrentValue = $CurrentValue
            NewValue = $NewValue
            CurrentSource = $FieldMeta.Source
            Reason = "Niedrigere Priorität: $($FieldMeta.Source) ($currentPriority) > $NewSource ($newPriority)"
            Timestamp = (Get-Date).ToUniversalTime()
        }
        
        if ($AllowConflicts) {
            Log-Warn -Message "Konflikt: Feld '$FieldName' von $($FieldMeta.Source) nicht überschrieben durch $NewSource (Priorität)" -Context "Merge"
            return @{
                Merged = $false
                NewValue = $CurrentValue
                Conflict = $conflict
                Message = "Niedrigere Priorität, nicht überschrieben"
            }
        } else {
            throw "Konflikt bei Feld '$FieldName': $($FieldMeta.Source) hat höhere Priorität als $NewSource"
        }
    }
}

<#
.SYNOPSIS
    Merge-Record: Merge einen kompletten HaltungRecord von einer Import-Quelle
.PARAMETER TargetRecord
    Ziel-Record (aus Projekt)
.PARAMETER SourceRecord
    Quell-Record (vom Import)
.PARAMETER ImportSource
    "xtf", "xtf405", "pdf"
.OUTPUTS
    PSCustomObject mit Statistik (Merged, Updated, Conflicts, Errors)
#>
function Merge-Record {
    param(
        [HaltungRecord] $TargetRecord,
        [HaltungRecord] $SourceRecord,
        [string] $ImportSource
    )
    
    $stats = @{
        Merged = 0
        Updated = 0
        Conflicts = 0
        Errors = 0
        ConflictDetails = @()
    }
    
    try {
        foreach ($fieldName in $script:FieldColumnOrder) {
            try {
                $sourceValue = $SourceRecord.GetFieldValue($fieldName)
                
                if ([string]::IsNullOrWhiteSpace($sourceValue)) {
                    continue
                }
                
                $currentValue = $TargetRecord.GetFieldValue($fieldName)
                $fieldMeta = $TargetRecord.FieldMeta[$fieldName]
                
                $mergeResult = Merge-Field -FieldName $fieldName `
                    -CurrentValue $currentValue `
                    -NewValue $sourceValue `
                    -FieldMeta $fieldMeta `
                    -NewSource $ImportSource `
                    -AllowConflicts $true
                
                if ($mergeResult.Merged) {
                    $TargetRecord.SetFieldValue($fieldName, $mergeResult.NewValue, $ImportSource, $false)
                    $stats.Updated++
                } elseif ($mergeResult.Conflict) {
                    $stats.Conflicts++
                    $stats.ConflictDetails += $mergeResult.Conflict
                }
                
            } catch {
                $stats.Errors++
                Log-Error -Message "Fehler beim Merge von Feld '$fieldName': $_" -Context "Merge:Record"
            }
        }
        
        $TargetRecord.ModifiedAt = (Get-Date).ToUniversalTime()
    } catch {
        $stats.Errors++
        Log-Error -Message "Fehler beim Merge von Record: $_" -Context "Merge:Record" -Exception $_
    }
    
    return $stats
}

<#
.SYNOPSIS
    Get-ConflictSummary: Erstellt eine Konfliktzusammenfassung für UI-Display
#>
function Get-ConflictSummary {
    param(
        [System.Collections.Generic.List[hashtable]] $Conflicts
    )
    
    if (-not $Conflicts -or $Conflicts.Count -eq 0) {
        return "Keine Konflikte"
    }
    
    $summary = "$($Conflicts.Count) Konflikte:`r`n`r`n"
    
    foreach ($conflict in $Conflicts | Select-Object -First 10) {
        $summary += "• $($conflict.FieldName):`r`n"
        $summary += "  Aktuell: '$($conflict.CurrentValue)' (von $($conflict.CurrentSource))`r`n"
        $summary += "  Neu: '$($conflict.NewValue)' (von $($conflict.Source))`r`n"
        $summary += "  Grund: $($conflict.Reason)`r`n`r`n"
    }
    
    if ($Conflicts.Count -gt 10) {
        $summary += "... und $($Conflicts.Count - 10) weitere Konflikte"
    }
    
    return $summary
}

# ========== Hilfsfunktionen ==========
function Get-SourcePriority {
    param([string]$Source)
    if ($script:SourcePriority.ContainsKey($Source)) {
        return $script:SourcePriority[$Source]
    }
    return 0
}

function Compare-SourcePriority {
    param(
        [string]$Source1,
        [string]$Source2
    )
    $p1 = Get-SourcePriority $Source1
    $p2 = Get-SourcePriority $Source2
    return $p1 - $p2  # Positiv = Source1 > Source2
}

Write-Host "[MergeService] Loaded - Priorität: $($script:SourcePriority | Out-String)" -ForegroundColor Green
