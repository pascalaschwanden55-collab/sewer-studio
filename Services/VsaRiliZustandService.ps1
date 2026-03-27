# Ordnet der Zustandsnote (ZN) die VSA-konforme Klasse und Beschreibung zu
function Get-ZustandsklasseUndBeschreibung {
    param(
        [double]$Zustandsnote
    )
    if ($null -eq $Zustandsnote) {
        return @{ Klasse = 'NULL'; Beschreibung = 'Es ist keine Berechnung gemäß vorliegender Richtlinie vorhanden.' }
    }
    if ($Zustandsnote -ge 0.00 -and $Zustandsnote -le 0.49) {
        return @{ Klasse = 'nicht mehr funktionstüchtig'; Beschreibung = 'Das Abwasserbauwerk ist bereits oder demnächst nicht mehr durchgängig und ist undicht, da es eingestürzt, vollständig verwurzelt ist und/oder andere Abflussbehinderungen bestehen.' }
    }
    if ($Zustandsnote -ge 0.50 -and $Zustandsnote -le 1.49) {
        return @{ Klasse = 'starke Defizite'; Beschreibung = 'Es bestehen Defizite, bei welchen die statische Sicherheit, Hydraulik oder Dichtheit nicht mehr gewährleistet ist: Rohrbrüche axial oder radial, Rohrdeformationen, visuell sichtbare Austritte oder Wasseraustritte, Löcher in der Rohrwand, stark vorstehende seitliche Anschlüsse, ausgewaschene Rohrwandung etc.' }
    }
    if ($Zustandsnote -ge 1.50 -and $Zustandsnote -le 2.49) {
        return @{ Klasse = 'mittlere Defizite'; Beschreibung = 'Defizite, welche die Statik, Hydraulik oder Dichtheit beeinträchtigen: breite Rohrfugen, nicht verputzte seitliche Anschlüsse, Risse, leichte Abflussbehinderungen wie Verkalkungen, vorstehende seitliche Anschlüsse, leichte Rohrwandbeschädigungen, einzelne Wurzeleinwüchse, Rohrwand ausgewaschen etc.' }
    }
    if ($Zustandsnote -ge 2.50 -and $Zustandsnote -le 3.49) {
        return @{ Klasse = 'leichte Defizite'; Beschreibung = 'Es bestehen Defizite oder Vorkommnisse, welche für die Dichtheit, Hydraulik oder Rohrstatik einen untergeordneten Einfluss haben: breite Rohrfugen, schlecht verputzte seitliche Anschlüsse, leichte Deformation bei Kunststoffleitungen, leichte Auswaschung der Rohrwandung etc.' }
    }
    if ($Zustandsnote -ge 3.50 -and $Zustandsnote -le 4.00) {
        return @{ Klasse = 'keine Defizite'; Beschreibung = '–' }
    }
    return @{ Klasse = 'NULL'; Beschreibung = 'Es ist keine Berechnung gemäß vorliegender Richtlinie vorhanden.' }
}
function Get-EinzelzustandFromCode {
    param(
        [string]$Schadencode,
        [string]$Kriterium = 'D',  # 'D', 'S', 'B'
        [string]$RuleFile = 'f:\\AuswertungPro\\vsa_rili_rules_kanaele.json'
    )

    if (-not (Test-Path $RuleFile)) { throw "VSA-Rili Regeldatei nicht gefunden: $RuleFile" }

    $rules = Get-Content -Raw -Path $RuleFile | ConvertFrom-Json

    # 1. fixedEzByMain (direktes Mapping)
    if ($rules.fixedEzByMain.PSObject.Properties.Name -contains $Schadencode) {
        $val = $rules.fixedEzByMain.$Schadencode
        if ($null -ne $val) { return $val }
    }

    # 2. rules (komplexe Regeln, hier nur Dummy: immer 2)
    if ($rules.rules.PSObject.Properties.Name -contains $Schadencode) {
        # TODO: Komplexe Regel auswerten (hier Dummy)
        return 2
    }

    Write-Verbose "VSA-Rili: Kein Mapping für Schadencode $Schadencode gefunden."
    return $null
}

function Compute-VsaZustandsnote {
    param(
        [array]$SchadensListe,  # Array von @{ Einzelzustand = 2; LL = 2.5 }
        [double]$Haltungslaenge
    )

    if (-not $SchadensListe -or $SchadensListe.Count -eq 0) { return $null }

    $EZmin = ($SchadensListe | Measure-Object -Property Einzelzustand -Minimum).Minimum
    if ($EZmin -eq $null) { return $null }

    $ZN_Start = [math]::Round([double]$EZmin + 0.4, 2)

    $sum = 0.0
    foreach ($s in $SchadensListe) {
        $sum += (4 - $s.Einzelzustand) * $s.LL
    }

    $A = 0.0
    if ((4 - $EZmin) * $Haltungslaenge -ne 0) {
        $A = 0.4 * ($sum / ((4 - $EZmin) * $Haltungslaenge))
        if ($A -gt 0.8) { $A = 0.8 }
    }

    $ZN = [math]::Round($ZN_Start - $A, 2)
    return $ZN
}

function Get-AnyValue {
    param(
        [Parameter(Mandatory=$true)][object]$Obj,
        [Parameter(Mandatory=$true)][string]$Name
    )

    if ($null -eq $Obj) { return $null }

    # Hashtable / Dictionary
    if ($Obj -is [System.Collections.IDictionary]) {
        if ($Obj.Contains($Name)) { return $Obj[$Name] }
        return $null
    }

    # PSCustomObject / normales Objekt
    $p = $Obj.PSObject.Properties[$Name]
    if ($null -ne $p) { return $p.Value }

    return $null
}

function ConvertTo-VsaFindings {
    param(
        [object[]]$Schaeden,
        [string]$Kriterium = 'D',
        [string]$RuleFile = 'f:\AuswertungPro\vsa_rili_rules_kanaele.json'
    )

    $findings = @()
    if ($null -eq $Schaeden -or $Schaeden.Count -eq 0) { return $findings }

    foreach ($s in $Schaeden) {
        if ($null -eq $s) { continue }

        # Unterstützt Hashtable UND PSCustomObject
        $isDict = $s -is [System.Collections.IDictionary]
        $code   = if ($isDict) { $s['Schadencode'] } else { $s.Schadencode }
        if ([string]::IsNullOrWhiteSpace($code)) { continue }

        $llRaw = if ($isDict) { $s['LL'] } else { $s.LL }
        $ll = 0.0
        try { if ($null -ne $llRaw -and "$llRaw" -ne "") { $ll = [double]$llRaw } } catch { $ll = 0.0 }

        $zustand = Get-EinzelzustandFromCode -Schadencode $code -Kriterium $Kriterium -RuleFile $RuleFile
        if ($zustand -ne $null) {
            $findings += @{ Einzelzustand = [int]$zustand; LL = $ll }
        }
    }

    return $findings
}
