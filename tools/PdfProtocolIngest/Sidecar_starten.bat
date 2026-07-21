@echo off
chcp 65001 >nul
echo ============================================================
echo   Sidecar STARTEN (Vision-Pipeline: YOLO/DINO/SAM)
echo ------------------------------------------------------------
echo   Dieses Fenster OFFEN lassen, solange du DINO brauchst
echo   (z.B. fuers Auto-Boxing). Warte, bis unten sinngemaess
echo   "Application startup complete" / "Uvicorn running" steht.
echo   Stoppen: dieses Fenster schliessen oder Strg+C.
echo ============================================================
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Sewer-Studio_KI_4.5\sidecar\start_sidecar.ps1"
echo.
echo (Sidecar beendet.)
pause
