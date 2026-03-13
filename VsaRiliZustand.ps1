# ============================================================================
# VsaRiliZustand.ps1
# ============================================================================
# Helpers for VSA Rili Zustandsnote calculation (Kanaele).

Set-StrictMode -Version Latest

$script:VsaRiliRulesCache = $null
$script:VsaRiliRulesPath = $null
$script:VsaRiliUnmapped = @{}

function Get-VsaRiliRules {
    param(
        [string]$RulesPath,
        [switch]$ForceReload
    )

    if (-not $RulesPath) {
        if ($script:VsaRiliRulesPath) {
            $RulesPath = $script:VsaRiliRulesPath
        }
        else {
            $RulesPath = Join-Path $PSScriptRoot "vsa_rili_rules_kanaele.json"
        }
    }

    if (-not $ForceReload -and $script:VsaRiliRulesCache) {
        return $script:VsaRiliRulesCache
    }

    if (-not (Test-Path $RulesPath)) {
        throw "VSA rules file not found: $RulesPath"
    }

    $json = Get-Content -Path $RulesPath -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 30
    $script:VsaRiliRulesCache = $json
    return $json
}

function ConvertTo-Double {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $v = $Value -replace ',', '.'
    $d = 0.0
    if ([double]::TryParse($v, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$d)) {
        return $d
    }
    return $null
}

function ConvertTo-Int {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $i = 0
    if ([int]::TryParse($Value, [ref]$i)) {
        return $i
    }
    return $null
}

function Split-VsaSchadencode {
    param([string]$Code)

    if ([string]::IsNullOrWhiteSpace($Code)) { return $null }
    $clean = ($Code -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
    if ($clean.Length -lt 3) { return $null }

    $main = $clean.Substring(0, 3)
    $ch1 = if ($clean.Length -ge 4) { $clean.Substring(3, 1) } else { $null }
    $ch2 = if ($clean.Length -ge 5) { $clean.Substring(4, 1) } else { $null }

    return @{
        Code = $clean
        Main = $main
        Ch1 = $ch1
        Ch2 = $ch2
    }
}

function Get-RuleValue {
    param($Map, [string]$Key)

    if (-not $Map) { return $null }
    if ($Map -is [hashtable]) { return $Map[$Key] }
    if ($Map.PSObject.Properties.Name -contains $Key) { return $Map.$Key }
    return $null
}

function Get-EzFromThresholds {
    param(
        [double]$Value,
        [double[]]$Thresholds,
        [string]$Direction = "desc"
    )

    if ($null -eq $Value -or -not $Thresholds -or $Thresholds.Count -eq 0) {
        return $null
    }

    if ($Direction -eq "asc") {
        for ($i = 0; $i -lt $Thresholds.Count; $i++) {
            if ($Value -le $Thresholds[$i]) { return $i }
        }
        return 4
    }

    for ($i = 0; $i -lt $Thresholds.Count; $i++) {
        if ($Value -ge $Thresholds[$i]) { return $i }
    }
    return 4
}

function Test-RuleMatch {
    param(
        $Rule,
        $Parts,
        [double]$Quant1,
        [double]$Quant2,
        [int]$LageVon,
        [int]$LageBis,
        [int]$Dn
    )

    if ($Rule.ch1) {
        $set = @($Rule.ch1)
        if ($set -notcontains $Parts.Ch1) { return $false }
    }
    if ($Rule.ch2) {
        $set = @($Rule.ch2)
        if ($set -notcontains $Parts.Ch2) { return $false }
    }

    if ($Rule.condition) {
        $dnGt = Get-RuleValue $Rule.condition "dn_gt"
        $dnLte = Get-RuleValue $Rule.condition "dn_lte"
        if ($dnGt -ne $null -and ($null -eq $Dn -or $Dn -le [int]$dnGt)) { return $false }
        if ($dnLte -ne $null -and ($null -eq $Dn -or $Dn -gt [int]$dnLte)) { return $false }
    }

    return $true
}

function Get-EinzelzustandFromCode {
    param(
        [string]$Code,
        [double]$Quant1,
        [double]$Quant2,
        [int]$LageVon,
        [int]$LageBis,
        [int]$Dn,
        $Rules
    )

    if (-not $Rules) {
        $Rules = Get-VsaRiliRules
    }

    $parts = Split-VsaSchadencode -Code $Code
    if (-not $parts) { return $null }

    $ignore = @()
    $ignoreList = Get-RuleValue $Rules "ignoreCodes"
    if ($ignoreList) { $ignore = @($ignoreList) }
    if ($ignore -contains $parts.Code) {
        return @{
            Code = $parts.Code
            Ignored = $true
            Classified = $false
        }
    }

    $result = @{
        Code = $parts.Code
        Main = $parts.Main
        Ch1 = $parts.Ch1
        Ch2 = $parts.Ch2
        D = $null
        S = $null
        B = $null
        EZ_min = $null
        Classified = $false
        RuleSource = $null
    }

    $rulesMap = Get-RuleValue $Rules "rules"
    $rulesForMain = Get-RuleValue $rulesMap $parts.Main
    if ($rulesForMain) {
        foreach ($rule in @($rulesForMain)) {
            if (-not (Test-RuleMatch -Rule $rule -Parts $parts -Quant1 $Quant1 -Quant2 $Quant2 -LageVon $LageVon -LageBis $LageBis -Dn $Dn)) {
                continue
            }

            $ez = $null
            if ($rule.ez -ne $null) {
                $ez = [int]$rule.ez
            }
            elseif ($rule.quant1) {
                if ($null -eq $Quant1) { continue }
                $thr = @($rule.quant1.thresholds) | ForEach-Object { [double]$_ }
                $direction = $rule.quant1.direction
                $ez = Get-EzFromThresholds -Value $Quant1 -Thresholds $thr -Direction $direction
            }
            elseif ($rule.quant2) {
                if ($null -eq $Quant2) { continue }
                $thr = @($rule.quant2.thresholds) | ForEach-Object { [double]$_ }
                $direction = $rule.quant2.direction
                $ez = Get-EzFromThresholds -Value $Quant2 -Thresholds $thr -Direction $direction
            }

            if ($null -eq $ez) { continue }

            $targets = if ($rule.targets) { @($rule.targets) } else { @("EZ") }
            foreach ($t in $targets) {
                switch ($t.ToUpperInvariant()) {
                    "D" { $result.D = if ($null -eq $result.D -or $ez -gt $result.D) { $ez } else { $result.D } }
                    "S" { $result.S = if ($null -eq $result.S -or $ez -gt $result.S) { $ez } else { $result.S } }
                    "B" { $result.B = if ($null -eq $result.B -or $ez -gt $result.B) { $ez } else { $result.B } }
                    default { $result.EZ_min = if ($null -eq $result.EZ_min -or $ez -gt $result.EZ_min) { $ez } else { $result.EZ_min } }
                }
            }

            $result.Classified = $true
            $result.RuleSource = "rules"
        }
    }

    $ezCandidates = @()
    foreach ($v in @($result.D, $result.S, $result.B, $result.EZ_min)) {
        if ($null -ne $v) { $ezCandidates += $v }
    }
    if ($ezCandidates.Count -gt 0) {
        $result.EZ_min = ($ezCandidates | Measure-Object -Maximum).Maximum
        $result.Classified = $true
        return $result
    }

    $fixedByCode = Get-RuleValue (Get-RuleValue $Rules "fixedEzByCode") $parts.Code
    if ($fixedByCode -ne $null) {
        $result.EZ_min = [int]$fixedByCode
        $result.Classified = $true
        $result.RuleSource = "fixed-code"
        return $result
    }

    $fixedByMain = Get-RuleValue (Get-RuleValue $Rules "fixedEzByMain") $parts.Main
    if ($fixedByMain -ne $null) {
        $result.EZ_min = [int]$fixedByMain
        $result.Classified = $true
        $result.RuleSource = "fixed-main"
        return $result
    }

    if (-not $script:VsaRiliUnmapped.ContainsKey($parts.Code)) {
        $script:VsaRiliUnmapped[$parts.Code] = $true
    }

    return $null
}

function ConvertTo-VsaFindings {
    param(
        [array]$Schaeden,
        [double]$MinLength = 3.0
    )

    $findings = @()
    $starts = @{}
    $ends = @{}

    foreach ($s in $Schaeden) {
        $parts = Split-VsaSchadencode -Code $s.Code
        if (-not $parts) { continue }

        $item = [ordered]@{
            Code = $parts.Code
            Main = $parts.Main
            Ch1 = $parts.Ch1
            Ch2 = $parts.Ch2
            Quant1 = $s.Quant1
            Quant2 = $s.Quant2
            Distanz = $s.Distanz
            LageVon = $s.LageVon
            LageBis = $s.LageBis
            Streckenschaden = $s.Streckenschaden
            Anmerkung = $s.Anmerkung
            LL_i = $null
            IsStrecke = $false
            StreckenKey = $null
        }

        if ($item.Streckenschaden -match '^A(\d{2})$') {
            $key = "$($Matches[1])|$($item.Code)"
            $starts[$key] = $item
            continue
        }
        if ($item.Streckenschaden -match '^B(\d{2})$') {
            $key = "$($Matches[1])|$($item.Code)"
            $ends[$key] = $item
            continue
        }

        $item.LL_i = $MinLength
        $findings += $item
    }

    foreach ($key in $starts.Keys) {
        if ($ends.ContainsKey($key)) {
            $a = $starts[$key]
            $b = $ends[$key]

            $distA = if ($null -ne $a.Distanz) { [double]$a.Distanz } else { $null }
            $distB = if ($null -ne $b.Distanz) { [double]$b.Distanz } else { $null }

            $len = $null
            if ($null -ne $distA -and $null -ne $distB) {
                $len = [Math]::Abs($distB - $distA)
            }
            if ($null -eq $len -or $len -lt $MinLength) { $len = $MinLength }

            $merged = [ordered]@{
                Code = $a.Code
                Main = $a.Main
                Ch1 = $a.Ch1
                Ch2 = $a.Ch2
                Quant1 = if ($null -ne $a.Quant1) { $a.Quant1 } else { $b.Quant1 }
                Quant2 = if ($null -ne $a.Quant2) { $a.Quant2 } else { $b.Quant2 }
                Distanz = if ($null -ne $a.Distanz) { $a.Distanz } else { $b.Distanz }
                LageVon = if ($null -ne $a.LageVon) { $a.LageVon } else { $b.LageVon }
                LageBis = if ($null -ne $b.LageBis) { $b.LageBis } else { $a.LageBis }
                Streckenschaden = $a.Streckenschaden
                Anmerkung = if ($a.Anmerkung) { $a.Anmerkung } else { $b.Anmerkung }
                LL_i = $len
                IsStrecke = $true
                StreckenKey = $key
            }
            $findings += $merged
        }
        else {
            $starts[$key].LL_i = $MinLength
            $findings += $starts[$key]
        }
    }

    foreach ($key in $ends.Keys) {
        if (-not $starts.ContainsKey($key)) {
            $ends[$key].LL_i = $MinLength
            $findings += $ends[$key]
        }
    }

    return $findings
}

function Compute-VsaZustandsnote {
    param(
        [array]$Findings,
        [double]$Laenge,
        [int]$Dn,
        $Rules
    )

    if (-not $Rules) { $Rules = Get-VsaRiliRules }

    $minLength = 3.0
    $defaults = Get-RuleValue $Rules "defaults"
    if ($defaults) {
        $minLengthVal = Get-RuleValue $defaults "minLengthKanal_m"
        if ($minLengthVal -ne $null) { $minLength = [double]$minLengthVal }
    }

    $classified = @()
    $unclassified = 0

    foreach ($f in $Findings) {
        $ezInfo = Get-EinzelzustandFromCode -Code $f.Code -Quant1 $f.Quant1 -Quant2 $f.Quant2 -LageVon $f.LageVon -LageBis $f.LageBis -Dn $Dn -Rules $Rules
        if ($ezInfo -and $ezInfo.Classified) {
            $f.EZ_min_i = $ezInfo.EZ_min
            $f.EZ_D = $ezInfo.D
            $f.EZ_S = $ezInfo.S
            $f.EZ_B = $ezInfo.B
            $classified += $f
        }
        else {
            $unclassified++
        }
    }

    if ($classified.Count -eq 0) {
        return @{
            Zustandsnote = 4.00
            Zustandsklasse = (Get-ZustandsklasseFromNote -Zustandsnote 4.00)
            EZ_min = $null
            Abminderung = 0.0
            ClassifiedCount = 0
            UnclassifiedCount = $unclassified
            Pruefungsresultat = "ZN=4.00 (keine klassifizierbaren Feststellungen)"
        }
    }

    $ez_min = ($classified | Measure-Object -Property EZ_min_i -Maximum).Maximum
    if ($ez_min -ge 4) {
        return @{
            Zustandsnote = 4.00
            Zustandsklasse = (Get-ZustandsklasseFromNote -Zustandsnote 4.00)
            EZ_min = $ez_min
            Abminderung = 0.0
            ClassifiedCount = $classified.Count
            UnclassifiedCount = $unclassified
            Pruefungsresultat = "ZN=4.00 (EZ_min=4)"
        }
    }

    $length = $Laenge
    if ($null -eq $length -or $length -le 0) {
        $length = ($classified | Measure-Object -Property LL_i -Sum).Sum
        if ($null -eq $length -or $length -le 0) { $length = $minLength }
    }

    $sum = 0.0
    foreach ($f in $classified) {
        $ll = if ($f.LL_i -and $f.LL_i -gt 0) { [double]$f.LL_i } else { $minLength }
        $sum += (4 - $f.EZ_min_i) * $ll
    }

    $den = (4 - $ez_min) * $length
    $A = 0.0
    if ($den -gt 0) {
        $A = 0.4 * ($sum / $den)
    }

    $maxA = 0.8
    $maxAbminderung = Get-RuleValue $defaults "maxAbminderung"
    if ($maxAbminderung -ne $null) { $maxA = [double]$maxAbminderung }
    if ($A -gt $maxA) { $A = $maxA }

    $znStart = $ez_min + 0.4
    $zn = $znStart - $A
    if ($zn -lt 0) { $zn = 0 }
    $zn = [Math]::Round($zn, 2, [MidpointRounding]::AwayFromZero)

    return @{
        Zustandsnote = $zn
        Zustandsklasse = (Get-ZustandsklasseFromNote -Zustandsnote $zn)
        EZ_min = $ez_min
        Abminderung = [Math]::Round($A, 2, [MidpointRounding]::AwayFromZero)
        ClassifiedCount = $classified.Count
        UnclassifiedCount = $unclassified
        Pruefungsresultat = "ZN=$zn (EZ_min=$ez_min, A=$([Math]::Round($A,2)), n=$($classified.Count))"
    }
}

function Get-ZustandsklasseFromNote {
    param([double]$Zustandsnote)

    if ($null -eq $Zustandsnote) { return "" }
    $z = [double]$Zustandsnote

    if ($z -lt 0.5) { return "0" }
    if ($z -lt 1.5) { return "1" }
    if ($z -lt 2.5) { return "2" }
    if ($z -lt 3.5) { return "3" }
    return "4"
}

