@echo off
REM Codex Player - Build Script for Windows
REM Builds and packages the Codex Player application

setlocal enabledelayedexpansion

echo ========================================
echo Codex Player - Build Script
echo ========================================

REM Check for .NET 8 SDK
dotnet --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo Error: .NET 8 SDK not found. Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo.
echo Step 1: Cleaning previous build artifacts...
dotnet clean CodexPlayer.sln -c Release >nul 2>&1

echo Step 2: Restoring NuGet packages...
dotnet restore CodexPlayer.sln
if %ERRORLEVEL% neq 0 (
    echo Error: Package restore failed
    exit /b 1
)

echo.
echo Step 3: Building solution in Release configuration...
dotnet build CodexPlayer.sln -c Release
if %ERRORLEVEL% neq 0 (
    echo Error: Build failed
    exit /b 1
)

echo.
echo Step 4: Running unit tests...
dotnet test CodexPlayer\CodexPlayer.Tests\CodexPlayer.Tests.csproj -c Release --verbosity normal
if %ERRORLEVEL% neq 0 (
    echo Error: Tests failed
    exit /b 1
)

echo.
echo Step 5: Publishing application...
dotnet publish CodexPlayer\CodexPlayer.UI\CodexPlayer.UI.csproj -c Release -o ./publish/ui
if %ERRORLEVEL% neq 0 (
    echo Error: Publishing failed
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo Output: ./publish/ui
echo ========================================

pause
