param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Acquire', 'Release', 'Status', 'CheckPath')]
    [string]$Action,

    [string]$Token = '',

    [string]$CandidatePath = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runDir = Join-Path $repoRoot 'tmp\ferien'
$lockPath = Join-Path $runDir 'run.lock'
$logPath = Join-Path $runDir 'run.log'
$readyPath = Join-Path $runDir 'READY.json'
$protectedPathsPath = Join-Path $runDir 'protected-paths.txt'

function Write-RunLog([string]$Message) {
    $line = '{0} {1}' -f [DateTime]::UtcNow.ToString('o'), $Message
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            [IO.File]::AppendAllText($logPath, $line + [Environment]::NewLine)
            return
        }
        catch [IO.IOException] {
            if ($attempt -eq 5) { throw }
            Start-Sleep -Milliseconds (50 * $attempt)
        }
    }
}

[IO.Directory]::CreateDirectory($runDir) | Out-Null

switch ($Action) {
    'CheckPath' {
        if ([string]::IsNullOrWhiteSpace($CandidatePath) -or
            -not [IO.File]::Exists($protectedPathsPath)) {
            [Console]::Error.WriteLine('Pfad oder Schutzliste fehlt.')
            exit 2
        }
        try {
            $candidate = if ([IO.Path]::IsPathRooted($CandidatePath)) {
                $CandidatePath
            }
            else {
                Join-Path $repoRoot $CandidatePath
            }
            $fullPath = [IO.Path]::GetFullPath($candidate)
        }
        catch {
            [Console]::Error.WriteLine('BLOCKED: Pfad ist ungültig.')
            exit 4
        }
        $rootPrefix = $repoRoot.TrimEnd('\') + '\'
        if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            [Console]::Error.WriteLine('BLOCKED: Pfad liegt ausserhalb des Projekts.')
            exit 4
        }
        $current = $fullPath
        while ($current.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            if (([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) -and
                (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
                [Console]::Error.WriteLine('BLOCKED: Pfad führt über eine Verknüpfung.')
                exit 4
            }
            $current = [IO.Path]::GetDirectoryName($current)
            if ([string]::IsNullOrEmpty($current)) { break }
        }
        $relative = $fullPath.Substring($rootPrefix.Length).Replace('\', '/')
        $protected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($path in [IO.File]::ReadAllLines($protectedPathsPath)) {
            $normalized = $path.Trim().Replace('\', '/')
            if ($normalized.Length -gt 0) { [void]$protected.Add($normalized) }
        }
        if ($protected.Contains($relative)) {
            [Console]::Error.WriteLine('PROTECTED: bestehende offene Datei nicht bearbeiten.')
            exit 4
        }
        Write-Output 'ALLOWED'
    }

    'Acquire' {
        if (-not [IO.File]::Exists($readyPath)) {
            Write-RunLog 'BLOCKED: readiness marker missing.'
            [Console]::Error.WriteLine('Ferienläufe sind noch nicht freigegeben: READY.json fehlt.')
            exit 5
        }
        try {
            $ready = [IO.File]::ReadAllText($readyPath) | ConvertFrom-Json
        }
        catch {
            Write-RunLog 'BLOCKED: readiness marker cannot be read.'
            [Console]::Error.WriteLine('Ferienläufe sind noch nicht freigegeben: READY.json ist ungültig.')
            exit 5
        }
        $requiredChecks = @(
            'direct_checkout_authorized',
            'code_only_mode',
            'protected_paths_recorded'
        )
        if ($ready.schema_version -ne 1 -or
            $ready.project_root -cne $repoRoot -or
            @($requiredChecks | Where-Object { $ready.$_ -ne $true }).Count -gt 0) {
            Write-RunLog 'BLOCKED: readiness marker invalid or incomplete.'
            [Console]::Error.WriteLine('Ferienläufe sind noch nicht freigegeben: Prüfungen fehlen.')
            exit 5
        }

        # ConvertFrom-Json liefert unter PowerShell ein DateTime; dessen lokalisierte
        # String-Darstellung darf nicht erneut als Monat/Tag gelesen werden.
        $expires = [DateTimeOffset]::MinValue
        if ($ready.expires_utc -is [DateTime]) {
            $expires = [DateTimeOffset]$ready.expires_utc
        }
        elseif (-not [DateTimeOffset]::TryParseExact(
                [string]$ready.expires_utc,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                [Globalization.CultureInfo]::InvariantCulture,
                [Globalization.DateTimeStyles]::AssumeUniversal,
                [ref]$expires)) {
            Write-RunLog 'BLOCKED: readiness expiry invalid.'
            [Console]::Error.WriteLine('Ferienläufe sind noch nicht freigegeben: Ablaufdatum ungültig.')
            exit 5
        }
        if ($expires -le [DateTimeOffset]::UtcNow -or
            -not [IO.File]::Exists($protectedPathsPath) -or
            (Get-FileHash -LiteralPath $protectedPathsPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
                $ready.protected_paths_sha256) {
            Write-RunLog 'BLOCKED: readiness expired or protected path list changed.'
            [Console]::Error.WriteLine('Ferienlauf gestoppt: Freigabe abgelaufen oder Schutzliste geändert.')
            exit 5
        }

        $branch = & git -C $repoRoot branch --show-current
        $branchExitCode = $LASTEXITCODE
        if ($branchExitCode -ne 0 -or
            [string]$branch -cne 'feature/webgis-uebertragung') {
            Write-RunLog 'BLOCKED: wrong branch.'
            [Console]::Error.WriteLine('Ferienlauf gestoppt: Arbeitsbranch stimmt nicht.')
            exit 5
        }

        $runId = [Guid]::NewGuid().ToString('N')
        $record = [ordered]@{
            token = $runId
            created_utc = [DateTime]::UtcNow.ToString('o')
            process_id = $PID
        } | ConvertTo-Json -Compress

        try {
            $stream = [IO.File]::Open($lockPath, [IO.FileMode]::CreateNew,
                [IO.FileAccess]::Write, [IO.FileShare]::None)
        }
        catch [IO.IOException] {
            if (-not [IO.File]::Exists($lockPath)) { throw }
            Write-RunLog 'SKIPPED: another run holds the lock.'
            [Console]::Error.WriteLine('Ein anderer Ferienlauf hält die Sperre. Dieser Lauf wurde übersprungen.')
            exit 3
        }

        try {
            $bytes = [Text.Encoding]::UTF8.GetBytes($record)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
        try {
            Write-RunLog "ACQUIRED: $runId"
        }
        catch {
            [IO.File]::Delete($lockPath)
            throw
        }
        Write-Output $runId
    }

    'Release' {
        if ([string]::IsNullOrWhiteSpace($Token)) {
            [Console]::Error.WriteLine('Zum Freigeben ist der Token des eigenen Laufs erforderlich.')
            exit 2
        }
        if (-not [IO.File]::Exists($lockPath)) {
            [Console]::Error.WriteLine('Keine aktive Sperre vorhanden.')
            exit 2
        }

        $record = [IO.File]::ReadAllText($lockPath) | ConvertFrom-Json
        if ($record.token -cne $Token) {
            [Console]::Error.WriteLine('Die Sperre gehört einem anderen Lauf und bleibt bestehen.')
            exit 4
        }

        try {
            [IO.File]::Delete($lockPath)
            Write-RunLog "RELEASED: $Token"
        }
        catch {
            [Console]::Error.WriteLine('Ferienlauf konnte seine Sperre nicht vollständig freigeben oder protokollieren.')
            throw
        }
    }

    'Status' {
        if ([IO.File]::Exists($lockPath)) {
            try {
                $record = [IO.File]::ReadAllText($lockPath) | ConvertFrom-Json
                $created = [DateTimeOffset]::MinValue
                if ($record.created_utc -is [DateTime]) {
                    $created = [DateTimeOffset]$record.created_utc
                }
                elseif (-not [DateTimeOffset]::TryParseExact(
                        [string]$record.created_utc,
                        "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
                        [Globalization.CultureInfo]::InvariantCulture,
                        [Globalization.DateTimeStyles]::AssumeUniversal,
                        [ref]$created)) {
                    throw 'Invalid lock timestamp.'
                }
                Write-Output "LOCKED since $($created.ToUniversalTime().ToString('o'))"
            }
            catch {
                Write-Output 'LOCKED (Sperrdatei unlesbar; manuelle Prüfung erforderlich)'
            }
        }
        else {
            Write-Output 'UNLOCKED'
        }
    }
}
