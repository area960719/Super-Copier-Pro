@echo off
title Instalador SuperCopier Pro
color 0B

:: 1. Solicitar permisos de Administrador automáticamente
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Solicitando permisos de administrador...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo ====================================================
echo   INSTALADOR DE SUPERCOPIER PRO
echo ====================================================
echo.

:: 2. Crear la carpeta de instalación
set "INSTALL_DIR=C:\Apps\SuperCopier"
echo [1/4] Creando carpeta en %INSTALL_DIR%...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
echo.

:: 3. Copiar los archivos
echo [2/4] Copiando archivos...
copy /Y "%~dp0Super-Copier-Pro.exe" "%INSTALL_DIR%\" >nul
copy /Y "%~dp0SuperCopierShellExt.dll" "%INSTALL_DIR%\" >nul
echo Archivos copiados correctamente.
echo.

:: 4. Registrar la extensión de Shell
echo [3/4] Registrando extension en Windows...
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe" (
    "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe" /codebase "%INSTALL_DIR%\SuperCopierShellExt.dll"
) else (
    "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\regasm.exe" /codebase "%INSTALL_DIR%\SuperCopierShellExt.dll"
)

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Hubo un problema al registrar la DLL.
    pause
    exit /b
)
echo.

:: 5. Crear acceso directo en el Escritorio
echo [4/4] Creando acceso directo en el Escritorio...
powershell -Command "$WS = New-Object -ComObject WScript.Shell; $SC = $WS.CreateShortcut([System.IO.Path]::Combine([System.Environment]::GetFolderPath('Desktop'), 'SuperCopier Pro.lnk')); $SC.TargetPath = '%INSTALL_DIR%\Super-Copier-Pro.exe'; $SC.WorkingDirectory = '%INSTALL_DIR%'; $SC.Save()"
echo.

echo ====================================================
echo   ¡INSTALACION COMPLETADA CON EXITO!
echo ====================================================
echo.
echo Reinicia tu PC (o el Explorador de Windows) para que 
echo el arrastrar y soltar funcione.
echo.
pause
