@echo off
echo ====================================================
echo Instalando SuperCopier Shell Extension...
echo ====================================================
echo.
echo Registrando la DLL en Windows (requiere admin)...
regasm /codebase "%~dp0SuperCopierShellExt.dll"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Hubo un problema al registrar la DLL.
    echo Asegurate de estar ejecutando este archivo como Administrador.
) else (
    echo.
    echo [OK] Extension registrada correctamente.
)

echo.
echo Reinicia el Explorador de Windows o la PC para ver los cambios.
pause
