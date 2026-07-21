@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "PY=C:\Sewer-Studio_KI_4.5\sidecar\.venv\Scripts\python.exe"
if not exist "%PY%" set "PY=python"
set "LOG=C:\KI_BRAIN\training\schritt3_log.txt"

echo ============================================================
echo   Schritt 3: Tuersteher trainieren  (das erste KI-Modell)
echo ------------------------------------------------------------
echo   * Der Sidecar muss AUS sein (sonst Grafikspeicher-Streit).
echo   * Nutzt die RTX 5090. Dauer: ca. 20-40 Minuten.
echo   * WICHTIG: Fenster NICHT schliessen, bis unten FERTIG steht.
echo     Es laeuft bis "Runde 30 von 30" (epoch 30/30).
echo ============================================================
echo.
echo Bitte JETZT den Sidecar stoppen (falls noch an),
echo dann eine beliebige Taste druecken zum Start...
pause >nul
echo.
echo [1 von 2] Training laeuft - bitte geduldig warten (pro Runde eine Zeile)...
"%PY%" train_cls.py train --data "C:\KI_BRAIN\training\datasets\cls_v1" --out "C:\KI_BRAIN\training\runs" 2>&1 | powershell -NoProfile -Command "$input | Tee-Object -FilePath '%LOG%'"
echo.
echo [2 von 2] Ehrliche Bewertung auf dem versiegelten Gold-Stapel...
"%PY%" train_cls.py eval --weights "C:\KI_BRAIN\training\runs\cls_v1\weights\best.pt" --split "C:\KI_BRAIN\training\datasets\cls_v1\gold" 2>&1 | powershell -NoProfile -Command "$input | Tee-Object -FilePath '%LOG%' -Append"
echo.
echo ============================================================
echo   FERTIG. Modell: C:\KI_BRAIN\training\runs\cls_v1\weights\best.pt
echo   Protokoll gespeichert: %LOG%
echo ============================================================
pause
