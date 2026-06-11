@echo off
echo Building Query Runner...
dotnet build
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo Starting Query Explorer...
echo.
dotnet run --project AirtableToPostgres.csproj -- query
pause
