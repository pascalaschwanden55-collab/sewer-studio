@echo off
rem Video-Scrub Label-Werkzeug starten. Danach im Browser http://localhost:8200 oeffnen.
cd /d "%~dp0"
python server.py %*
pause
