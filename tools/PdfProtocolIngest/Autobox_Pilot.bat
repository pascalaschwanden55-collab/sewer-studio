@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "PYTHONUNBUFFERED=1"
set "PY=C:\Sewer-Studio_KI_4.5\sidecar\.venv\Scripts\python.exe"
if not exist "%PY%" set "PY=python"
set "SLOG=C:\KI_BRAIN\training\sidecar_start_log.txt"
set "PLOG=C:\KI_BRAIN\training\autobox_pilot_log.txt"

echo ============================================================
echo   Auto-Boxing-Pilot (mit Sidecar-Token) - Sichtpruefung
echo ============================================================
echo.
echo [1/3] Pruefe, ob der Sidecar laeuft (mit Token)...
powershell -NoProfile -Command "$t='';if($env:SEWER_SIDECAR_AUTH_TOKEN){$t=$env:SEWER_SIDECAR_AUTH_TOKEN}else{$f=Join-Path $env:LOCALAPPDATA 'SewerStudio\.sidecar_token';if(Test-Path $f){$t=(Get-Content $f -Raw).Trim()}};try{Invoke-WebRequest http://127.0.0.1:8100/health -Headers @{'X-Sidecar-Token'=$t} -TimeoutSec 3 -UseBasicParsing|Out-Null;exit 0}catch{exit 1}"
if not errorlevel 1 goto RUN

echo     Nicht erreichbar - starte Sidecar (Log per Tee, zeilenweise).
echo     Protokoll: %SLOG%
start "Sidecar" /min powershell -NoProfile -ExecutionPolicy Bypass -Command "$env:PYTHONUNBUFFERED='1'; & 'C:\Sewer-Studio_KI_4.5\sidecar\start_sidecar.ps1' 2>&1 | Tee-Object -FilePath '%SLOG%'"
echo [2/3] Warte auf den Sidecar (bis zu 3 Minuten)...
powershell -NoProfile -Command "for($i=0;$i -lt 90;$i++){$t='';if($env:SEWER_SIDECAR_AUTH_TOKEN){$t=$env:SEWER_SIDECAR_AUTH_TOKEN}else{$f=Join-Path $env:LOCALAPPDATA 'SewerStudio\.sidecar_token';if(Test-Path $f){$t=(Get-Content $f -Raw).Trim()}};try{Invoke-WebRequest http://127.0.0.1:8100/health -Headers @{'X-Sidecar-Token'=$t} -TimeoutSec 3 -UseBasicParsing|Out-Null;Write-Host '     bereit.';exit 0}catch{Start-Sleep 2}};exit 1"
if errorlevel 1 goto FAIL

:RUN
echo.
echo [3/3] Auto-Boxing laeuft (erster Frame ~20-30s)...
"%PY%" autobox_pilot.py 2>&1 | powershell -NoProfile -Command "$input | Tee-Object -FilePath '%PLOG%'"
echo.
echo FERTIG. Bilder: C:\KI_BRAIN\training\autobox_pilot\viz
pause
exit /b 0

:FAIL
echo.
echo Sidecar antwortet nicht (auch nicht mit Token).
echo Bitte sag mir "fehler" - ich lese %SLOG% aus.
pause
