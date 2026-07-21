@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================================
echo   Schritt 1: Bilder aus den Videos schneiden
echo   Laeuft eine Weile (ffmpeg ueber alle Videos). Einfach warten.
echo   Abbruch jederzeit mit Strg+C - der naechste Start macht weiter.
echo ============================================================
echo.
echo [1 von 2] Katalog erstellen (liest die Inspektionsberichte)...
python pdf_ingest.py parse   --root "D:\Haltungen" --out "C:\KI_BRAIN\training\pdf_ingest"
echo.
echo [2 von 2] Frames aus den Videos schneiden...
python pdf_ingest.py extract --root "D:\Haltungen" --out "C:\KI_BRAIN\training\pdf_ingest"
echo.
echo FERTIG. Die Bilder liegen sortiert nach Schadenstyp in:
echo    C:\KI_BRAIN\training\pdf_ingest\frames
echo.
pause
