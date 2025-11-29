@echo off
title Iniciando PrestamistaRD - Sistema de Préstamos
color 0A

echo =====================================
echo   Iniciando PrestamistaRD - Sistema de Préstamos
echo =====================================
echo.

REM === Ir al directorio donde está el instalador ===
cd /d "%~dp0"
cd ..

REM === Definir rutas relativas ===
set MYSQL_DIR=%cd%\MySQL
set DATA_DIR=%MYSQL_DIR%\data
set BIN_DIR=%MYSQL_DIR%\bin
set APP_DIR=%cd%\Publicaciones

REM === Verificar si mysqld existe ===
if not exist "%BIN_DIR%\mysqld.exe" (
    echo ❌ ERROR: No se encontró "%BIN_DIR%\mysqld.exe"
    echo Asegúrese de que MySQL esté correctamente instalado.
    pause
    exit /b
)

REM === Iniciar MySQL Portable ===
echo Iniciando MySQL Portable...
start "" "%BIN_DIR%\mysqld.exe" --defaults-file="%MYSQL_DIR%\my.ini" --console
timeout /t 5 >nul

REM === Iniciar aplicación ASP.NET local (Kestrel) ===
echo Iniciando la aplicación web...
cd "%APP_DIR%"
start "" dotnet PrestamistaRD.dll

echo ✅ PrestamistaRD iniciado correctamente.
echo Puede cerrar esta ventana si desea.
pause
exit
