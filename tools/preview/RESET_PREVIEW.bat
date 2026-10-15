@echo off
setlocal
cd /d "%~dp0"
if exist izmenjuj-preview.db del /q izmenjuj-preview.db
if exist izmenjuj-preview.db-shm del /q izmenjuj-preview.db-shm
if exist izmenjuj-preview.db-wal del /q izmenjuj-preview.db-wal
echo Predogled je ponastavljen. Ob naslednjem zagonu se bodo vzorcni podatki ustvarili znova.
pause
