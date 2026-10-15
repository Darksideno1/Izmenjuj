@echo off
setlocal
cd /d "%~dp0"
set IZMENJUJ_PREVIEW=1
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://127.0.0.1:5080

echo.
echo =====================================================
echo   IZMENJUJ.SI - LOKALNI PREDOGLED
echo =====================================================
echo   Naslov: http://127.0.0.1:5080
echo   Demo e-mail: demo@izmenjuj.local
echo   Demo geslo:  Demo1234
echo =====================================================
echo.
echo Brskalnik se bo odprl samodejno.
echo Tega okna ne zapri, dokler pregledujes stran.
echo.

start "" powershell -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Seconds 3; Start-Process 'http://127.0.0.1:5080'"
Izmenjuj.Web.exe

echo.
echo Predogled je ustavljen.
pause
