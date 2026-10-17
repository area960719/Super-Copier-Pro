@echo off
title Instalador SuperCopier Pro
echo ====================================================
echo Instalando SuperCopier Shell Extension...
echo ====================================================
echo.

:: Verificar si se está ejecutando como administrador
net session >nul 2>&1
if %errorLevel% == 0 (
    echo [OK] Ejecutando con privilegios de administrador.
) else (
    echo [ADVERTENCIA] No se esta ejecutando como administrador.
    echo Es muy probable que la instalacion falle.
    echo.
    echo Por favor, cierra esta ventana, haz clic derecho en el archivo
    echo y selecciona "Ejecutar como administrador".
    echo.
    pause
    exit /b
)

echo.
echo Registrando la DLL en Windows...
echo.

:: Intentar usar regasm de 64 bits (lo normal en PCs modernas)
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe" (
    "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe" /codebase "%~dp0SuperCopierShellExt.dll"
) else (
    :: Si falla, usar el de 32 bits
    "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\regasm.exe" /codebase "%~dp0SuperCopierShellExt.dll"
)

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Hubo un problema al registrar la DLL.
    echo Asegurate de que el archivo SuperCopierShellExt.dll este en esta misma carpeta.
) else (
    echo.
    echo [OK] Extension registrada correctamente.
)

echo.
echo ====================================================
echo IMPORTANTE:
echo Debes reiniciar el Explorador de Windows o reiniciar
echo la PC para que los cambios surtan efecto.
echo ====================================================
echo.
pause
