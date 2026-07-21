@echo off
chcp 65001 >nul
cd /d "C:\Sewer-Studio_KI_4.5"
set "PY=C:\Sewer-Studio_KI_4.5\sidecar\.venv\Scripts\python.exe"
if not exist "%PY%" set "PY=python"
set "LOG=C:\KI_BRAIN\training\autopilot_v2_log.txt"
echo ============================================================
echo   Klassifikator-Autopilot auf angereichertem Datensatz
echo ------------------------------------------------------------
echo   * Sidecar muss AUS sein (Grafikspeicher).
echo   * 60 Runden auf der RTX 5090 - kann laenger dauern.
echo   * Ergebnis = KANDIDAT + Report. active.json bleibt unberuehrt.
echo   * Fenster offen lassen bis unten FERTIG steht.
echo ============================================================
echo.
echo Bitte JETZT den Sidecar stoppen, dann eine Taste druecken...
pause >nul
echo.
"%PY%" training\vsa_classifier\train_autopilot.py --name vsa_cls_pdfplus_v2 --data "C:\KI_BRAIN\yolo_vsa_cls_dataset_pdfplus" 2>&1 | powershell -NoProfile -Command "$input | Tee-Object -FilePath '%LOG%'"
echo.
echo ============================================================
echo   FERTIG. Kandidat: vsa_cls_pdfplus_v2
echo   Report unter docs\benchmarks\  ^|  Protokoll: %LOG%
echo ============================================================
pause
