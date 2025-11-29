@echo off
title Deteniendo PrestamistaRD
echo ===============================================
echo   Deteniendo PrestamistaRD y MySQL Portable
echo ===============================================

echo Cerrando la aplicación PrestamistaRD...
taskkill /f /im PrestamistaRD.exe >nul 2>&1

echo Deteniendo MySQL Portable...
taskkill /f /im mysqld.exe >nul 2>&1

echo ===============================================
echo        Todo detenido correctamente.
echo ===============================================
pause
