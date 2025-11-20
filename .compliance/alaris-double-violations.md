# Alaris.Double Coding Standard Violations

**Initial Build Date**: 2025-11-20
**Build Command**: `dotnet build Alaris.Double/Alaris.Double.csproj`
**Initial Result**: **186 errors** (0 warnings - all treated as errors)

**Remediation Completed**: 2025-11-20
**Final Result**: ✅ **0 errors, 0 warnings** - 100% COMPLIANT
**Total Time**: ~2 hours

---

## ✅ REMEDIATION COMPLETE

All 186 violations have been successfully fixed. Alaris.Double now builds cleanly with `TreatWarningsAsErrors=true`.

**Commits**:
- `35b73cd` - "Complete Alaris.Double Coding Standard Compliance (186 → 0 violations)"
- `ae7cb61` - "Fix Remaining Violations in Alaris.Double (88 → 0)" - Fixed over-parenthesization

---

## Executive Summary

Alaris.Double has **186 violations** against the coding standard, categorized as follows:

| Category | Count | Priority | Effort |
|----------|-------|----------|--------|
| Generic Exception Catches (CA1031) | 3 | 🔴 **HIGH** | 1-2 hours |
| XML Documentation Errors (CS1570) | 2 | 🟡 **MEDIUM** | 15 mins |
| Design Issues (CA1819, CA1716, CA1051, CA2225, CA1805) | 7 | 🟡 **MEDIUM** | 2-3 hours |
| Code Style - Braces (IDE0011) | ~73 | 🟢 **LOW** | 1 hour (bulk fix) |
| Code Style - Explicit Types (IDE0008) | ~30 | 🟢 **LOW** | 30 mins (bulk fix) |
| Code Style - Parentheses (IDE0048) | ~66 | 🟢 **LOW** | 1 hour (bulk fix) |
| Code Style - Readonly (IDE0044) | 2 | 🟢 **LOW** | 5 mins |
| Code Style - Unnecessary Parens (IDE0047) | 1 | 🟢 **LOW** | 2 mins |

**Total Effort Estimate**: 6-8 hours

---

## Priority 1: Generic Exception Catches (CA1031) - HIGH PRIORITY

**Rule Violated**: LOC-5 Rule 10 (Specific Exceptions)

**Violations**: 3 instances in `DoubleBoundaryEngine.cs`

### 1. DoubleBoundaryEngine.cs:188 - CalculateVega
```csharp
catch (Exception ex)
{
    return 0.0;
}
```

**Fix**: Catch specific exceptions (ArgumentException, InvalidOperationException) or let it crash

### 2. DoubleBoundaryEngine.cs:226 - CalculateTheta
```csharp
catch (Exception ex)
{
    return 0.0;
}
```

**Fix**: Catch specific exceptions or let it crash

### 3. DoubleBoundaryEngine.cs:285 - CalculateRho
```csharp
catch (Exception ex)
{
    return 0.0;
}
```

**Fix**: Catch specific exceptions or let it crash

**Action**: Same pattern as UnifiedPricingEngine fixes - identify expected failure modes and catch only those.

---

## Priority 2: Design Issues (7 violations)

### CA1819: Properties Should Not Return Arrays (2 violations)

**Location**: `DoubleBoundarySolver.cs:271, 276`

**Issue**: Array properties can be accidentally mutated by callers

**Violations**:
```csharp
public double[] Results { get; set; }
public double[] Boundaries { get; set; }
```

**Fix Options**:
1. Return `IReadOnlyList<double>` instead
2. Return defensive copy
3. Use `ReadOnlySpan<double>` for performance

**Recommendation**: Use `IReadOnlyList<double>` for clarity

---

### CA1716: Namespace Conflicts with Reserved Keyword

**Location**: `DoubleBoundaryApproximation.cs:1` (entire namespace)

**Issue**: Namespace `Alaris.Double` conflicts with C# keyword `double`

**Current**:
```csharp
namespace Alaris.Double
```

**Fix Options**:
1. Rename to `Alaris.DoubleBoundary`
2. Rename to `Alaris.AmericanOption`
3. Suppress warning (not recommended)

**Recommendation**: Rename to `Alaris.DoubleBoundary` for consistency with class names

**Impact**: Breaking change - requires updating all references

---

### CA1051: Do Not Declare Visible Instance Fields

**Location**: `DoubleBoundaryKimSolver.cs:35`

**Issue**: Public field instead of property

**Violation**:
```csharp
public double FieldName;
```

**Fix**: Convert to property:
```csharp
public double FieldName { get; set; }
```

---

### CA2225: Provide Alternate Method for Operator

**Location**: `DoubleBoundaryEngine.cs:57`

**Issue**: Implicit operator without named method

**Violation**:
```csharp
public static implicit operator PricingEngine(DoubleBoundaryEngine engine)
```

**Fix**: Add named method:
```csharp
public static implicit operator PricingEngine(DoubleBoundaryEngine engine) => ToDoubleBoundaryEngine(engine);

public static PricingEngine ToDoubleBoundaryEngine(DoubleBoundaryEngine engine)
{
    return engine; // conversion logic
}
```

---

### CA1805: Explicit Initialization to Default

**Location**: `DoubleBoundaryKimSolver.cs:40`

**Issue**: Initializing field to its default value (redundant)

**Violation**:
```csharp
private int _currentIteration = 0;
```

**Fix**: Remove explicit initialization:
```csharp
private int _currentIteration;
```

---

### IDE0044: Make Field Readonly (2 violations)

**Location**: `DoubleBoundaryKimSolver.cs:38, 39`

**Issue**: Fields never reassigned after initialization

**Fix**: Add `readonly` modifier:
```csharp
private readonly double _tolerance;
private readonly int _maxIterations;
```

---

## Priority 3: XML Documentation Errors (2 violations)

### CS1570: Badly Formed XML

**Location**: `DoubleBoundaryKimSolver.cs:577` (2 instances)

**Issue**: XML comment has syntax error - whitespace or unclosed tag

**Example**:
```csharp
/// <param name="threshold.">Invalid format</param>
```

**Fix**: Remove trailing period from parameter name:
```csharp
/// <param name="threshold">Valid format</param>
```

---

## Priority 4: Code Style Issues (172 violations)

### IDE0011: Missing Braces (~73 violations)

**Issue**: Single-line if statements without braces

**Violation**:
```csharp
if (condition)
    DoSomething();
```

**Fix**: Add braces:
```csharp
if (condition)
{
    DoSomething();
}
```

**Bulk Fix**: Use IDE refactoring or find-replace pattern

---

### IDE0008: Use Explicit Type (~30 violations)

**Issue**: Using `var` when explicit type is preferred

**Violation**:
```csharp
var result = CalculateBoundary();
```

**Fix**: Use explicit type:
```csharp
double result = CalculateBoundary();
```

**Bulk Fix**: Use IDE refactoring

---

### IDE0048: Add Parentheses for Clarity (~66 violations)

**Issue**: Complex arithmetic without clarity parentheses

**Violation**:
```csharp
double result = a + b * c / d;
```

**Fix**: Add parentheses:
```csharp
double result = a + ((b * c) / d);
```

**Bulk Fix**: Manual review required - automated fix may over-parenthesize

---

### IDE0047: Remove Unnecessary Parentheses (1 violation)

**Location**: `DoubleBoundaryKimSolver.cs:442`

**Issue**: Redundant parentheses

**Fix**: Remove extra parentheses

---

## Remediation Plan

### Week 2 (2025-11-20 to 2025-11-27) - High Priority Fixes

1. **Fix 3 Generic Exception Catches** (CA1031) - 1-2 hours
   - DoubleBoundaryEngine.cs: CalculateVega, CalculateTheta, CalculateRho
   - Identify expected exceptions (ArgumentException, InvalidOperationException)
   - Let unexpected exceptions crash

2. **Fix XML Documentation Errors** (CS1570) - 15 mins
   - DoubleBoundaryKimSolver.cs:577 - Fix malformed parameter tags

### Week 3 (2025-11-27 to 2025-12-04) - Design Issues

3. **Fix Array Properties** (CA1819) - 1 hour
   - DoubleBoundarySolver.cs: Convert to IReadOnlyList<double>
   - Update all consumers

4. **Fix Visible Instance Field** (CA1051) - 5 mins
   - DoubleBoundaryKimSolver.cs:35 - Convert to property

5. **Fix Explicit Initialization** (CA1805) - 2 mins
   - DoubleBoundaryKimSolver.cs:40 - Remove `= 0`

6. **Add Readonly Modifiers** (IDE0044) - 5 mins
   - DoubleBoundaryKimSolver.cs:38, 39

7. **Add Alternate Operator Method** (CA2225) - 15 mins
   - DoubleBoundaryEngine.cs:57 - Add ToDoubleBoundaryEngine()

8. **DECISION REQUIRED: Namespace Rename** (CA1716)
   - Option A: Rename `Alaris.Double` → `Alaris.DoubleBoundary` (breaking change)
   - Option B: Suppress warning (not recommended)
   - **Recommendation**: Rename for long-term clarity

### Week 4 (2025-12-04 to 2025-12-11) - Code Style Bulk Fixes

9. **Add Braces to If Statements** (IDE0011) - 1 hour
   - ~73 violations across all files
   - Use IDE bulk refactoring

10. **Convert Var to Explicit Types** (IDE0008) - 30 mins
    - ~30 violations
    - Use IDE bulk refactoring

11. **Add Clarity Parentheses** (IDE0048) - 1 hour
    - ~66 violations
    - Manual review for each expression

12. **Remove Unnecessary Parentheses** (IDE0047) - 2 mins
    - 1 violation

---

## Impact Assessment

### Breaking Changes
- **Namespace Rename** (if approved): All consumers must update using statements
- **Array Properties → IReadOnlyList**: Callers using array indexer may need changes

### Non-Breaking Changes
- Exception handling improvements
- Code style fixes (no behavioral changes)
- XML documentation fixes

### Test Coverage
- All changes must pass existing 109 tests
- No new tests required (behavior unchanged)

---

## Success Criteria

**Phase 2 Complete** when:
- ✅ All 186 errors resolved
- ✅ `dotnet build Alaris.Double/Alaris.Double.csproj` succeeds with 0 errors
- ✅ All 109 tests pass
- ✅ No behavioral regressions

---

**Next Steps**: Begin Week 2 remediation with high-priority fixes (generic exceptions and XML documentation).
