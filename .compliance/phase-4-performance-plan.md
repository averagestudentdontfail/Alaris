# Phase 4: Performance Optimization Plan

**Created**: 2025-11-21
**Status**: READY FOR IMPLEMENTATION
**Target Completion**: 2025-12

---

## Executive Summary

Phase 4 focuses on **hot path optimization** to reduce memory allocation pressure and improve latency in critical pricing operations. Analysis identified **7 critical hot paths** with combined potential for **85-95% allocation reduction**.

---

## Hot Path Analysis Summary

| Priority | Component | Method | Current Allocations | Target |
|----------|-----------|--------|---------------------|--------|
| CRITICAL | UnifiedPricingEngine | Greek Calculations | 143/option | 5-10 |
| CRITICAL | DoubleBoundaryKimSolver | RefineUsingFpbPrime | 200+ arrays | 5-10 |
| HIGH | YangZhang | CalculateRolling | 252+ lists | 2-3 |
| MEDIUM | TermStructure | Analyze | 3 arrays | 0 heap |
| MEDIUM | SignalGenerator | Various LINQ | Multiple | Minimal |

---

## Critical Hot Path #1: Greek Calculations

**File**: `Alaris.Strategy/Bridge/UnifiedPricingEngine.cs`
**Impact**: Most expensive operation - creates 143 QuantLib objects per option pricing

### Problem

Each Greek calculation (Delta, Gamma, Vega, Theta, Rho) calls `PriceOptionSync` 2-3 times, totaling 11+ calls per option. Each call creates:
- 14 QuantLib infrastructure objects
- 1 OptionParameters clone

**Total per option**: 11 × 14 = 154 object creations + 11 clones = **165 allocations**

### Solution: Infrastructure Caching

```csharp
// Create infrastructure once, reuse for all bump scenarios
private sealed class QuantLibInfrastructureCache : IDisposable
{
    private readonly SimpleQuote _underlyingQuote;
    private readonly FlatForward _flatRateTs;
    private readonly FlatForward _flatDividendTs;
    private readonly BlackConstantVol _flatVolTs;
    private readonly VanillaOption _option;
    // ... other cached objects

    public double Price(double spot, double vol, double rate)
    {
        _underlyingQuote.setValue(spot);
        // Update other mutable parameters
        return _option.NPV();
    }
}
```

### Alternative: Parameter Pooling

```csharp
private static readonly ObjectPool<OptionParameters> _pool =
    new DefaultObjectPool<OptionParameters>(new ParameterPolicy());

private OptionParameters ClonePooled(OptionParameters original)
{
    var clone = _pool.Get();
    clone.CopyFrom(original);
    return clone;
}
```

### Expected Improvement

- **Allocation reduction**: 165 → 15 (90% reduction)
- **Latency reduction**: 30-50% for Greek calculations
- **GC pressure**: Significant reduction in Gen0/Gen1 collections

---

## Critical Hot Path #2: Kim Solver Array Operations

**File**: `Alaris.Double/DoubleBoundaryKimSolver.cs`
**Impact**: 200+ array allocations in Newton iteration loop

### Problem

```csharp
// Lines 145-146: NEW allocation every iteration (up to 100 iterations)
double[] upperNew = new double[m];
double[] lowerNew = new double[m];

// Line 165: CLONE in inner loop (up to 5,000 clones)
double[] tempUpper = (double[])upper.Clone();
```

**Total**: 200 arrays + 5,000 clones = **~5,200 allocations**

### Solution: ArrayPool<T>

```csharp
private (double[] Upper, double[] Lower) RefineUsingFpbPrime(...)
{
    double[] upper = ArrayPool<double>.Shared.Rent(_collocationPoints);
    double[] lower = ArrayPool<double>.Shared.Rent(_collocationPoints);
    double[] upperNew = ArrayPool<double>.Shared.Rent(_collocationPoints);
    double[] lowerNew = ArrayPool<double>.Shared.Rent(_collocationPoints);

    try
    {
        // Refinement logic...
        // Copy initial values
        Array.Copy(upperInitial, upper, _collocationPoints);
        Array.Copy(lowerInitial, lower, _collocationPoints);

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // Swap buffers instead of allocating
            (upper, upperNew) = (upperNew, upper);
            (lower, lowerNew) = (lowerNew, lower);

            // Calculate new values into upperNew, lowerNew
        }

        // Copy result to return array
        double[] resultUpper = new double[_collocationPoints];
        double[] resultLower = new double[_collocationPoints];
        Array.Copy(upper, resultUpper, _collocationPoints);
        Array.Copy(lower, resultLower, _collocationPoints);
        return (resultUpper, resultLower);
    }
    finally
    {
        ArrayPool<double>.Shared.Return(upper);
        ArrayPool<double>.Shared.Return(lower);
        ArrayPool<double>.Shared.Return(upperNew);
        ArrayPool<double>.Shared.Return(lowerNew);
    }
}
```

### Expected Improvement

- **Allocation reduction**: 5,200 → 6 (99.9% reduction)
- **Latency reduction**: 50-70% for Kim solver
- **Memory**: ~2 MB → ~320 bytes per solver call

---

## High Priority Hot Path #3: Yang-Zhang Rolling Volatility

**File**: `Alaris.Strategy/Core/YangZhang.cs`
**Impact**: 252 list allocations for annual volatility series

### Problem

```csharp
// Line 142: NEW list every iteration (252 times for annual data)
for (int i = window; i < priceBars.Count; i++)
{
    List<PriceBar> windowBars = priceBars.Skip(i - window).Take(window + 1).ToList();
    // ...
}
```

### Solution: Circular Buffer

```csharp
public IReadOnlyList<(DateTime Date, double Volatility)> CalculateRolling(
    IReadOnlyList<PriceBar> priceBars,
    int window,
    bool annualized = true)
{
    var results = new List<(DateTime Date, double Volatility)>(priceBars.Count - window);

    // Reusable arrays for returns calculation
    Span<double> openReturns = stackalloc double[window];
    Span<double> closeReturns = stackalloc double[window];
    Span<double> rogersReturns = stackalloc double[window];

    for (int i = window; i < priceBars.Count; i++)
    {
        // Calculate returns directly from priceBars[i-window..i]
        CalculateReturnsInPlace(priceBars, i - window, window,
            openReturns, closeReturns, rogersReturns);

        double variance = CalculateVariance(openReturns, closeReturns, rogersReturns);
        double volatility = annualized
            ? Math.Sqrt(variance * 252)
            : Math.Sqrt(variance);

        results.Add((priceBars[i].Date, volatility));
    }

    return results;
}
```

### Expected Improvement

- **Allocation reduction**: 756 → 1 (99.9% reduction)
- **Latency reduction**: 30-40% for rolling calculations

---

## Medium Priority Optimizations

### TermStructure.Analyze - Use Span<T>

```csharp
public TermStructureAnalysis Analyze(IReadOnlyList<TermStructurePoint> points)
{
    // Stack allocation instead of heap
    Span<(double dte, double iv)> sortedPoints =
        points.Count <= 32
            ? stackalloc (double, double)[points.Count]
            : new (double, double)[points.Count];

    // Sort in place
    // ...
}
```

### SignalGenerator - Eliminate LINQ Materialize

```csharp
// Replace TakeLast with direct loop
int startIdx = Math.Max(0, priceHistory.Count - 30);
double avgVolume = 0;
for (int i = startIdx; i < priceHistory.Count; i++)
    avgVolume += priceHistory[i].Volume;
signal.AverageVolume = (long)(avgVolume / (priceHistory.Count - startIdx));
```

---

## Implementation Phases

### Phase 4.1: Critical Infrastructure (Week 1)

| Task | File | Effort |
|------|------|--------|
| Implement QuantLib infrastructure caching | UnifiedPricingEngine.cs | 2 days |
| Add OptionParameters pooling | UnifiedPricingEngine.cs | 0.5 days |
| Validate Greek calculation accuracy | Test suite | 0.5 days |

### Phase 4.2: Solver Optimization (Week 2)

| Task | File | Effort |
|------|------|--------|
| Add ArrayPool to Kim solver | DoubleBoundaryKimSolver.cs | 1 day |
| Eliminate inner loop cloning | DoubleBoundaryKimSolver.cs | 0.5 days |
| Benchmark Kim solver performance | Benchmark project | 0.5 days |

### Phase 4.3: Analysis Optimization (Week 3)

| Task | File | Effort |
|------|------|--------|
| Implement circular buffer for YangZhang | YangZhang.cs | 1 day |
| Add Span<T> to TermStructure | TermStructure.cs | 0.5 days |
| Remove LINQ materialize in SignalGenerator | SignalGenerator.cs | 0.5 days |

### Phase 4.4: Validation (Week 4)

| Task | Effort |
|------|--------|
| Run full test suite | 0.5 days |
| BenchmarkDotNet profiling | 1 day |
| Document results | 0.5 days |

---

## Success Metrics

### Target Improvements

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Greeks allocations/option | 165 | <20 | Object allocation profiler |
| Kim solver allocations | 5,200 | <10 | ArrayPool monitoring |
| Rolling vol allocations | 756 | <5 | Memory profiler |
| Gen0 GC frequency | Baseline | -50% | dotnet-counters |
| Greek calculation latency | Baseline | -30% | BenchmarkDotNet |

### Validation Requirements

1. All 109 tests passing
2. Benchmark accuracy unchanged (0.00% error vs Healy Table 2)
3. No functional regressions

---

## Dependencies

### Required Packages

```xml
<!-- Add to Alaris.Strategy.csproj -->
<PackageReference Include="Microsoft.Extensions.ObjectPool" Version="8.0.0" />

<!-- For benchmarking -->
<PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
```

### Code Changes Required

1. **New file**: `Alaris.Strategy/Infrastructure/QuantLibCache.cs`
2. **New file**: `Alaris.Strategy/Infrastructure/OptionParametersPool.cs`
3. **Modified**: `UnifiedPricingEngine.cs` (Greek calculations)
4. **Modified**: `DoubleBoundaryKimSolver.cs` (ArrayPool integration)
5. **Modified**: `YangZhang.cs` (circular buffer)

---

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Greek accuracy regression | High | Low | Comprehensive test coverage |
| QuantLib disposal issues | High | Medium | Careful lifecycle management |
| ArrayPool buffer corruption | Medium | Low | Clear ownership semantics |
| Performance worse in edge cases | Medium | Low | Benchmark across scenarios |

---

## Approval

**Plan Created By**: Claude Code
**Review Required By**: Kiran K. Nath
**Target Start Date**: Upon approval

---

*Document Version: 1.0 | Created: 2025-11-21*
