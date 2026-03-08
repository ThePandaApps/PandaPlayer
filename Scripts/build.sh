#!/bin/bash
# Codex Player - Build Script for macOS/Linux
# Cross-platform build using .NET SDK

set -e

echo "========================================"
echo "Codex Player - Build Script"
echo "========================================"

# Check for .NET 8 SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET 8 SDK not found. Please install .NET 8 SDK from https://dotnet.microsoft.com/download"
    exit 1
fi

echo ""
echo "Step 1: Cleaning previous build artifacts..."
dotnet clean CodexPlayer.sln -c Release 2>/dev/null || true

echo "Step 2: Restoring NuGet packages..."
dotnet restore CodexPlayer.sln

echo ""
echo "Step 3: Building solution in Release configuration..."
dotnet build CodexPlayer.sln -c Release

echo ""
echo "Step 4: Running unit tests..."
dotnet test CodexPlayer/CodexPlayer.Tests/CodexPlayer.Tests.csproj -c Release --verbosity normal

echo ""
echo "Step 5: Publishing application..."
dotnet publish CodexPlayer/CodexPlayer.UI/CodexPlayer.UI.csproj -c Release -o ./publish/ui

echo ""
echo "========================================"
echo "Build completed successfully!"
echo "Output: ./publish/ui"
echo "========================================"
