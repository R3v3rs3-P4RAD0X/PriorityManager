#!/bin/bash

# Build script for Priority Manager mod

echo "Building Priority Manager mod..."

cd "$(dirname "$0")/Source"

# Try dotnet first
if command -v dotnet &> /dev/null; then
    echo "Using dotnet to build..."
    dotnet build PriorityManagerMod.csproj -c Release
    exit $?
fi

# Try msbuild
if command -v msbuild &> /dev/null; then
    echo "Using msbuild to build..."
    msbuild PriorityManagerMod.csproj /p:Configuration=Release
    exit $?
fi

# Try xbuild (older Mono)
if command -v xbuild &> /dev/null; then
    echo "Using xbuild to build..."
    xbuild PriorityManagerMod.csproj /p:Configuration=Release
    exit $?
fi

echo "ERROR: No build tool found. Please install one of the following:"
echo "  - .NET SDK (dotnet)"
echo "  - Mono (msbuild/xbuild)"
exit 1

