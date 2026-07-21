@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================================
echo   Schritt 2: Trainingspaket bauen  (Schaden vs. normal)
echo   Sortiert die Schadensbilder + zieht Normal-Bilder.
echo   Dauert nur ein paar Minuten. Fortsetzbar und absturzsicher.
echo ============================================================
echo.
python build_cls_dataset.py build ^
    --catalog "C:\KI_BRAIN\training\pdf_ingest\ingest.jsonl" ^
    --labels  "C:\KI_BRAIN\training\pdf_ingest\labels.jsonl" ^
    --root    "D:\Haltungen" ^
    --out     "C:\KI_BRAIN\training\datasets\cls_v1"
echo.
echo FERTIG. Das Trainingspaket liegt in:
echo    C:\KI_BRAIN\training\datasets\cls_v1
echo    (Unterordner train\ und val\ mit je schaden\ und normal\; gold\ ist versiegelt)
echo.
pause
