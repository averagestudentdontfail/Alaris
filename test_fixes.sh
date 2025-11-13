#!/bin/bash
# Test script to verify Alaris.Double fixes

echo "================================"
echo "Building Alaris.Double..."
echo "================================"
dotnet build Alaris.Double/Alaris.Double.csproj

if [ $? -ne 0 ]; then
    echo "Build failed! Check compilation errors above."
    exit 1
fi

echo ""
echo "================================"
echo "Running Unit Tests..."
echo "================================"
dotnet test --filter "FullyQualifiedName~Alaris.Test.Unit" --logger "console;verbosity=normal"

echo ""
echo "================================"
echo "Running Diagnostic Tests..."
echo "================================"
dotnet test --filter "FullyQualifiedName~Alaris.Test.Diagnostic" --logger "console;verbosity=normal"

echo ""
echo "================================"
echo "Running Integration Tests..."
echo "================================"
dotnet test --filter "FullyQualifiedName~Alaris.Test.Integration" --logger "console;verbosity=normal"

echo ""
echo "================================"
echo "Running Benchmark Tests..."
echo "================================"
dotnet test --filter "FullyQualifiedName~Alaris.Test.Benchmark" --logger "console;verbosity=normal"

echo ""
echo "================================"
echo "Running Full Test Suite..."
echo "================================"
dotnet test

echo ""
echo "Test run complete!"
