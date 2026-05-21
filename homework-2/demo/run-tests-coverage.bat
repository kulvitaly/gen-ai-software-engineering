@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%.."
set "COVERAGE_FILE=%REPO_ROOT%\tests\Tests\coverage.cobertura.xml"
set "REPORT_DIR=%REPO_ROOT%\tests\Tests\coverage-report"
set "REPORT_INDEX=%REPORT_DIR%\index.html"

echo Intelligent Customer Support System - Tests and HTML Coverage
echo.
echo This script runs all tests with the 85%% coverage gate and generates an HTML coverage report.
echo.
echo Repository root:
echo   %REPO_ROOT%
echo.

pushd "%REPO_ROOT%" || exit /b 1

where reportgenerator >nul 2>nul
if errorlevel 1 (
    echo ReportGenerator was not found. Installing it as a global .NET tool...
    dotnet tool install -g dotnet-reportgenerator-globaltool
    if errorlevel 1 (
        echo Failed to install ReportGenerator.
        popd
        exit /b 1
    )
    echo.
    echo If this is the first time installing a .NET global tool, restart the terminal if reportgenerator is still not found.
    echo.
)

echo Running tests with coverage...
dotnet test "CustomerSupportSystem.slnx" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=85 /p:ThresholdType=line /p:ThresholdStat=total
if errorlevel 1 (
    echo Tests or coverage threshold failed.
    popd
    exit /b 1
)

if not exist "%COVERAGE_FILE%" (
    echo Coverage file was not found:
    echo   %COVERAGE_FILE%
    popd
    exit /b 1
)

echo.
echo Generating HTML coverage report...
reportgenerator -reports:"%COVERAGE_FILE%" -targetdir:"%REPORT_DIR%" -reporttypes:Html
if errorlevel 1 (
    echo Failed to generate HTML coverage report.
    popd
    exit /b 1
)

echo.
echo HTML coverage report generated:
echo   %REPORT_INDEX%
echo.
echo Opening report...
start "" "%REPORT_INDEX%"

popd
endlocal
