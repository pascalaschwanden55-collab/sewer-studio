<#
.SYNOPSIS
    Logging Service für AuswertungPro
.DESCRIPTION
    Zentrale Fehler-, Warn- und Info-Logging Infrastruktur
#>

# ========== Konfiguration ==========
$script:LogPath = Join-Path $PSScriptRoot "..\logs"
$script:LogFile = Join-Path $script:LogPath "app.log"
$script:ErrorLogFile = Join-Path $script:LogPath "errors.log"
$script:ImportLogFile = Join-Path $script:LogPath "imports.log"
$script:MaxLogSizeMB = 10
$script:KeepLogBackups = 5

# Ensure Directories
@($script:LogPath) | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -Path $_ -ItemType Directory -Force | Out-Null }
}

# ========== Log-Funktionen ==========
function Write-Log {
    param(
        [ValidateSet('Info', 'Warn', 'Error', 'Debug')]
        [string] $Level = 'Info',
        [string] $Message = '',
        [string] $Context = '',
        [object] $Exception = $null
    )
    
    try {
        $timestamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        $contextStr = if ($Context) { "[$Context]" } else { "" }
        $logMessage = "[$timestamp] [$Level] $contextStr $Message"
        
        if ($Exception) {
            # Unterstuetze sowohl Exception als auch ErrorRecord
            $exMessage = ""
            $exStack = ""
            
            if ($Exception -is [System.Exception]) {
                $exMessage = $Exception.Message
                $exStack = $Exception.StackTrace
            } elseif ($Exception -is [System.Management.Automation.ErrorRecord]) {
                $exMessage = $Exception.Exception.Message
                $exStack = $Exception.ScriptStackTrace
            } else {
                $exMessage = $Exception.ToString()
            }
            
            if ($exMessage) {
                $logMessage += "`n  Exception: $exMessage"
            }
            if ($exStack) {
                $logMessage += "`n  StackTrace: $exStack"
            }
        }
        
        # Schreibe in Konsole
        switch ($Level) {
            'Info' { Write-Host $logMessage -ForegroundColor Gray }
            'Warn' { Write-Host $logMessage -ForegroundColor Yellow }
            'Error' { Write-Host $logMessage -ForegroundColor Red }
            'Debug' { Write-Debug $logMessage }
        }
        
        # Schreibe in Datei
        $logFile = if ($Level -eq 'Error') { $script:ErrorLogFile } else { $script:LogFile }
        Add-Content -Path $logFile -Value $logMessage -ErrorAction SilentlyContinue
        
        # Log-Rotation falls zu groß
        Invoke-LogRotation -LogPath $logFile
    }
    catch {
        # Fallback: Konsole nur
        Write-Host "LOGGING ERROR: $_" -ForegroundColor Red
    }
}

function Write-ImportLog {
    param(
        [string] $ImportType,    # "XTF" | "XTF405" | "PDF"
        [string] $FilePath,
        [int] $CountCreated = 0,
        [int] $CountUpdated = 0,
        [int] $CountConflicts = 0,
        [int] $CountErrors = 0
    )
    
    $timestamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    $fileName = Split-Path $FilePath -Leaf
    $message = "$timestamp | $ImportType | $fileName | Created: $CountCreated | Updated: $CountUpdated | Conflicts: $CountConflicts | Errors: $CountErrors"
    
    Add-Content -Path $script:ImportLogFile -Value $message -ErrorAction SilentlyContinue
}

function Invoke-LogRotation {
    param([string]$LogPath)
    
    try {
        $file = Get-Item $LogPath -ErrorAction SilentlyContinue
        if ($file -and ($file.Length / 1MB) -gt $script:MaxLogSizeMB) {
            $dir = Split-Path $LogPath -Parent
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($LogPath)
            $ext = [System.IO.Path]::GetExtension($LogPath)
            
            # Verschiebe aktuelle Datei
            $backupPath = Join-Path $dir "$baseName.$(Get-Date -Format 'yyyyMMdd_HHmmss')$ext"
            Move-Item -Path $LogPath -Destination $backupPath -Force -ErrorAction SilentlyContinue
            
            # Alte Backups löschen
            Get-ChildItem -Path $dir -Filter "$baseName.*$ext" -ErrorAction SilentlyContinue |
                Sort-Object -Property LastWriteTime -Descending |
                Select-Object -Skip $script:KeepLogBackups |
                Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
        # Stille Fehlerbehandlung bei Rotation
    }
}

function Get-LogContent {
    param(
        [ValidateSet('App', 'Errors', 'Imports')]
        [string] $LogType = 'App',
        [int] $LastLines = 50
    )
    
    $logFile = switch ($LogType) {
        'Errors' { $script:ErrorLogFile }
        'Imports' { $script:ImportLogFile }
        default { $script:LogFile }
    }
    
    if (Test-Path $logFile) {
        Get-Content -Path $logFile -Tail $LastLines | Out-String
    } else {
        "(Logdatei existiert noch nicht)"
    }
}

function Clear-Logs {
    param(
        [ValidateSet('All', 'App', 'Errors', 'Imports')]
        [string] $LogType = 'All'
    )
    
    $logFiles = switch ($LogType) {
        'Errors' { @($script:ErrorLogFile) }
        'Imports' { @($script:ImportLogFile) }
        'App' { @($script:LogFile) }
        'All' { @($script:LogFile, $script:ErrorLogFile, $script:ImportLogFile) }
    }
    
    foreach ($logFile in $logFiles) {
        if (Test-Path $logFile) {
            Clear-Content -Path $logFile -ErrorAction SilentlyContinue
        }
    }
}

# ========== Häufig verwendete Helper ==========
function Log-Info {
    param([string]$Message, [string]$Context = '')
    Write-Log -Level Info -Message $Message -Context $Context
}

function Log-Warn {
    param([string]$Message, [string]$Context = '')
    Write-Log -Level Warn -Message $Message -Context $Context
}

function Log-Error {
    param([string]$Message, [string]$Context = '', [object]$Exception = $null)
    Write-Log -Level Error -Message $Message -Context $Context -Exception $Exception
}

function Log-Debug {
    param([string]$Message, [string]$Context = '')
    Write-Log -Level Debug -Message $Message -Context $Context
}

Write-Host "[LoggingService] Loaded" -ForegroundColor Green
