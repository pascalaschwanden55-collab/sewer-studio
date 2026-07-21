@echo off
chcp 65001 >nul
echo ============================================================
echo   Sidecar-Doktor - misst nur, startet nichts
echo ============================================================
echo.
echo [A] Laeuft etwas auf Port 8100?
netstat -ano | findstr :8100
echo.
echo [B] Python/uvicorn-Prozesse:
tasklist | findstr /I "python uvicorn"
echo.
echo [C] Health-Check MIT und OHNE Token (echter Statuscode):
powershell -NoProfile -Command ^
  "$f=Join-Path $env:LOCALAPPDATA 'SewerStudio\.sidecar_token';" ^
  "$t='';if($env:SEWER_SIDECAR_AUTH_TOKEN){$t=$env:SEWER_SIDECAR_AUTH_TOKEN}elseif(Test-Path $f){$t=(Get-Content $f -Raw).Trim()};" ^
  "Write-Host ('    Token-Datei : ' + $f);" ^
  "Write-Host ('    existiert    : ' + (Test-Path $f));" ^
  "Write-Host ('    Token-Laenge : ' + $t.Length);" ^
  "function probe($tok,$name){try{$r=Invoke-WebRequest http://127.0.0.1:8100/health -Headers @{'X-Sidecar-Token'=$tok} -TimeoutSec 5 -UseBasicParsing;Write-Host ('    ' + $name + ' -> HTTP ' + [int]$r.StatusCode);Write-Host ('      ' + $r.Content.Substring(0,[Math]::Min(200,$r.Content.Length)))}catch{$resp=$_.Exception.Response;if($resp){$sc=[int]$resp.StatusCode;$sr=New-Object System.IO.StreamReader($resp.GetResponseStream());$body=$sr.ReadToEnd();Write-Host ('    ' + $name + ' -> HTTP ' + $sc);Write-Host ('      ' + $body.Substring(0,[Math]::Min(200,$body.Length)))}else{Write-Host ('    ' + $name + ' -> KEINE ANTWORT: ' + $_.Exception.Message)}}}" ^
  "probe $t 'mit Token ';" ^
  "probe ''  'ohne Token';"
echo.
echo Bitte den GESAMTEN Text oben kopieren und mir schicken.
pause
