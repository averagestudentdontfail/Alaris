# Alaris Coding Compliance

## Overview
This document defines the coding standards and compliance rules for the Alaris project.

## General Rules
1. **Language**: C# 12.0 or later.
2. **Framework**: .NET 9.0.
3. **Style**: Follow standard C# coding conventions (PascalCase for public members, camelCase for private fields with underscore prefix).
4. **Documentation**: All public members must have XML documentation comments.

## Numerical Libraries
- **MathNet.Numerics**: Use for all linear algebra, optimization, and integration tasks.
- **QuantLib**: Use via `Alaris.Quantlib` wrapper for financial instrument pricing.
- **Avoid Custom Implementations**: Do not implement numerical algorithms from scratch if a robust library implementation exists.

## Error Handling
- Use specific exception types.
- Validate arguments at the beginning of methods.
- Use `ArgumentNullException.ThrowIfNull`.

## Testing
- All public methods must have unit tests.
- Use xUnit for testing.
- Maintain high code coverage.

## Performance
- Use `Span<T>` and `Memory<T>` for hot paths.
- Avoid excessive allocations in critical loops.
- Use `ArrayPool<T>` for large temporary buffers.
