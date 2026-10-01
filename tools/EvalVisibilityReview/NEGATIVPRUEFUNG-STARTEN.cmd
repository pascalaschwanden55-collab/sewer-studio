@echo off
rem Pruefplatz: Ist auf diesem Bild ein Bogen sichtbar?
rem Doppelklick genuegt. Fenster offen lassen, solange geprueft wird.
rem
rem Bewusst als .cmd und nicht als .ps1: Windows fuehrt PowerShell-Skripte
rem beim Doppelklick standardmaessig nicht aus.

setlocal
set WURZEL=C:\Sewer-Studio_KI_4.5
set PYTHON=%WURZEL%\sidecar\.venv\Scripts\python.exe
set SERVER=%WURZEL%\tools\EvalVisibilityReview\bcc_negativ_review_server.py
set QUEUE=C:\KI_BRAIN\training\diagnostics\bcc_negativpruefung_v1
set AUSGABE=C:\KI_BRAIN\eval_review\bcc_negativpruefung_v1_review.json
set PORT=8776

title Bogen-Negativpruefung

if not exist "%PYTHON%" (
    echo FEHLER: Python nicht gefunden:
    echo   %PYTHON%
    pause
    exit /b 1
)
if not exist "%QUEUE%\queue.json" (
    echo FEHLER: Die Pruef-Stichprobe fehlt:
    echo   %QUEUE%
    pause
    exit /b 1
)

echo.
echo   Bogen-Negativpruefung
echo   ---------------------
echo   30 Bilder. Pro Bild eine Taste:
echo      1 = Bogen sichtbar     2 = kein Bogen     3 = unsicher
echo.
echo   Der Stand wird nach jedem Urteil gespeichert.
echo   Abbrechen und spaeter weitermachen ist jederzeit moeglich.
echo.

"%PYTHON%" "%SERVER%" --queue "%QUEUE%" --output "%AUSGABE%" --reviewer "Pascal" --port %PORT%

echo.
echo   Pruefplatz beendet. Ergebnis:
echo   %AUSGABE%
echo.
pause
