@echo off
chcp 65001 >nul
echo ============================================================
echo   Sidecar stoppen (fuer das Training)
echo ============================================================
echo.
echo Suche laufenden Sidecar auf Port 8100...
set "FOUND=0"
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":8100" ^| findstr LISTENING') do (
    echo   Gefunden - stoppe Prozess %%a ...
    taskkill /PID %%a /F >nul 2>&1
    set "FOUND=1"
)
if "%FOUND%"=="0" (
    echo   Kein laufender Sidecar gefunden - alles gut, nichts zu stoppen.
) else (
    echo   Sidecar gestoppt. Grafikspeicher ist jetzt frei.
)
echo.
echo Du kannst jetzt Schritt 3 ^(Training^) starten.
echo.
pause
