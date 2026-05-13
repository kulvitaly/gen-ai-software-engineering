@echo off
REM Transaction API Launcher - lives in homework-1\demo; runs project under homework-1\src

pushd "%~dp0..\src"

if "%1"=="https" (
    echo Starting TransactionApi with HTTPS profile...
    dotnet run --project TransactionApi\TransactionApi.csproj --launch-profile https
    goto Done
)

if "%1"=="http" (
    echo Starting TransactionApi with HTTP profile...
    dotnet run --project TransactionApi\TransactionApi.csproj --launch-profile http
    goto Done
)

echo.
echo Transaction API Launcher
echo.
echo Usage: run.bat [profile]
echo.
echo Profiles:
echo   http     - HTTP only (default^) - http://localhost:5263
echo   https    - HTTPS enabled - https://localhost:7117
echo.
echo Starting with default HTTP profile...
echo.
dotnet run --project TransactionApi\TransactionApi.csproj

:Done
popd

pause
