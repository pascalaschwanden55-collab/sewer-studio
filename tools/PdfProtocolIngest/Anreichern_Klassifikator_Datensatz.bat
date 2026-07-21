@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "PY=C:\Sewer-Studio_KI_4.5\sidecar\.venv\Scripts\python.exe"
if not exist "%PY%" set "PY=python"
echo ============================================================
echo   Datensatz anreichern (nicht-destruktiv)
echo   Kopiert deinen bal-Datensatz + fuegt unsere gepruefte
echo   PDF-Frames hinzu (nur bekannte Klassen, Eval sauber raus).
echo   Dein Original, val-Split und active.json bleiben unberuehrt.
echo ============================================================
echo.
"%PY%" enrich_vsa_dataset.py
echo.
echo FERTIG. Neuer Datensatz: C:\KI_BRAIN\yolo_vsa_cls_dataset_pdfplus
echo Danach: deinen Autopiloten darauf laufen lassen (Befehl steht im Chat).
echo.
pause
