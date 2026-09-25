param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Acquire', 'Release', 'Status')]
    [string]$Action,

    [string]$Token = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runDir = Join-Path $repoRoot 'tmp\ferien'
$lockPath = Join-Path $runDir 'run.lock'
$logPath = Join-Path $runDir 'run.log'
$readyPath = Join-Path $runDir 'READY.json'
$manifestPath = Join-Path (Split-Path $repoRoot -Parent) `
    'SewerStudio_10Tage_20260925_Snapshot\video-reference-manifest.json'

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
            'technical_isolation_verified',
            'quality_baseline_verified',
            'restart_dry_run_verified',
            'expired_login_dry_run_verified',
            'red_task_dry_run_verified',
            'continuation_dry_run_verified'
        )
        if ($ready.schema_version -ne 1 -or
            $ready.worktree_root -cne $repoRoot -or
            @($requiredChecks | Where-Object { $ready.$_ -ne $true }).Count -gt 0) {
            Write-RunLog 'BLOCKED: readiness marker invalid or incomplete.'
            [Console]::Error.WriteLine('Ferienläufe sind noch nicht freigegeben: Prüfungen fehlen.')
            exit 5
        }

        $expires = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse([string]$ready.expires_utc, [ref]$expires)) {
            Write-RunLog 'BLOCKED: readiness expiry invalid.'
            [Console]::Error.WriteLine('Ferienläufe sind noch nicht freigegeben: Ablaufdatum ungültig.')
            exit 5
        }
        if ($expires -le [DateTimeOffset]::UtcNow -or
            -not [IO.File]::Exists($manifestPath) -or
            (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne
                $ready.reference_manifest_sha256) {
            Write-RunLog 'BLOCKED: readiness expired or reference set changed.'
            [Console]::Error.WriteLine('Ferienlauf gestoppt: Freigabe abgelaufen oder Referenzsatz geändert.')
            exit 5
        }

        $branch = & git -C $repoRoot branch --show-current
        $branchExitCode = $LASTEXITCODE
        $changes = @(& git -C $repoRoot status --porcelain --untracked-files=normal)
        if ($branchExitCode -ne 0 -or $LASTEXITCODE -ne 0 -or
            [string]$branch -cne 'codex/ferien-20260925' -or $changes.Count -gt 0) {
            Write-RunLog 'BLOCKED: wrong branch or uncommitted changes.'
            [Console]::Error.WriteLine('Ferienlauf gestoppt: Arbeitsbranch oder Git-Stand stimmt nicht.')
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
                if (-not [DateTimeOffset]::TryParse([string]$record.created_utc, [ref]$created)) {
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
