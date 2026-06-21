<#
.SYNOPSIS
  Erstellt einen isolierten git-Worktree fuer eine parallele Agent-/Session-Arbeit.

.DESCRIPTION
  Verhindert die Branch-/Verzeichnis-Kollision vom 2026-06-21, bei der zwei Claude-Sessions
  gleichzeitig auf feature/gis-karte im SELBEN Arbeitsverzeichnis liefen und sich gegenseitig
  Commits per reset/cherry-pick ueberschrieben. Jede parallele Session/Agent arbeitet ab jetzt
  in IHREM eigenen Worktree-Ordner auf IHREM eigenen Branch -> keine geteilte Working-Copy mehr.

.PARAMETER Name
  Kurzname fuer Agent/Aufgabe. Ergibt Branch "agent/<Name>" und Ordner ".worktrees/<Name>".

.PARAMETER Base
  Basis-Branch/Commit fuer den neuen Branch. Default: aktueller Branch.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/new-agent-worktree.ps1 audit-fixes
  # -> .worktrees/audit-fixes auf Branch agent/audit-fixes (von aktuellem Branch)

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/new-agent-worktree.ps1 hotfix master
  # -> .worktrees/hotfix auf Branch agent/hotfix (von master)
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Base = ""
)
$ErrorActionPreference = "Stop"

$repo = (git rev-parse --show-toplevel).Trim()
Set-Location $repo

if (-not $Base) { $Base = (git branch --show-current).Trim() }
$branch = "agent/$Name"
$path = ".worktrees/$Name"

if (Test-Path $path) { throw "Worktree-Ordner existiert bereits: $path" }

git worktree add -b $branch $path $Base
if ($LASTEXITCODE -ne 0) { throw "git worktree add fehlgeschlagen (Branch existiert evtl. schon? -> 'git branch -D $branch')" }

Write-Host ""
Write-Host "OK  Worktree erstellt: $repo\$path"
Write-Host "    Branch:  $branch  (von $Base)"
Write-Host ""
Write-Host "    Die parallele Session/den Agenten in DIESEM Ordner starten:  cd `"$repo\$path`""
Write-Host "    Aufraeumen nach Merge:  git worktree remove $path ; git branch -d $branch"
