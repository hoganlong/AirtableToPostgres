@echo off
cd /d "%~dp0"
echo Building Query Explorer...
dotnet build --nologo -v quiet
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b %errorlevel%
)

cls
dotnet run --no-build -- query
