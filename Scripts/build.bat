@echo off
REM Panda Player - Build Script for Windows
REM Builds and packages the Panda Player application

setlocal enabledelayedexpansion

echo ========================================
echo Panda Player - Build Script
echo ========================================

REM Check for .NET 8 SDK
dotnet --version >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo Error: .NET 8 SDK not found. Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

echo.
echo Step 1: Cleaning previous build artifacts...
dotnet clean Panda Player.sln -c Release >nul 2>&1

echo Step 2: Restoring NuGet packages...
dotnet restore Panda Player.sln
if %ERRORLEVEL% neq 0 (
    echo Error: Package restore failed
    exit /b 1
)

echo.
echo Step 3: Building solution in Release configuration...
dotnet build Panda Player.sln -c Release
if %ERRORLEVEL% neq 0 (
    echo Error: Build failed
    exit /b 1
)

echo.
echo Step 4: Running unit tests...
dotnet test Panda Player\Panda Player.Tests\Panda Player.Tests.csproj -c Release --verbosity normal
if %ERRORLEVEL% neq 0 (
    echo Error: Tests failed
    exit /b 1
)

echo.
echo Step 5: Publishing application...
dotnet publish Panda Player\Panda Player.UI\Panda Player.UI.csproj -c Release -o ./publish/ui
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
