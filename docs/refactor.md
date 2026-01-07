# Alaris Refactor Specification

> "We are what we repeatedly do. Excellence, then, is not an act, but a habit."
> — Aristotle

## Preface

Refactoring in Alaris is a disciplined act of stewardship. The system exists to price, trade, and account for risk in real markets. It therefore demands clarity of intent, precision of meaning, and respect for physical execution. This specification provides a repeatable method that begins with first principles, rises through engineering practice, and ends with auditable change. It is both technical and philosophical. Code carries responsibility because it expresses a claim about reality.

This document complements [Alaris Coding Standard](standard.md) and [Alaris Type System](types.md). It defines how to change code while preserving integrity, performance, and auditability.

---

## Part I: First Principles

### 1.1 Purpose and Consequence

A refactor is justified when it improves correctness, performance, maintainability, or auditability without altering intended behaviour. Each change must trace to a concrete purpose. Refactoring is not aesthetic decoration. It is a method of preserving long term system integrity.

### 1.2 Meaning and Accountability

Every function is a statement about reality. Every type is a commitment to meaning. A refactor must preserve those commitments or replace them with stronger ones. The human operator inherits accountability for any change that weakens meaning.

### 1.3 Boundaries and Invariants

Invariants are enforced at boundaries, not in every line of interior logic. The refactor must preserve this boundary discipline. Validation is explicit and local. Internal logic assumes validated inputs and remains efficient.

### 1.4 Determinism and Auditability

Mission critical systems require deterministic behaviour. Refactors must reduce ambiguity, remove hidden costs, and increase traceability. A refactor that obscures the path of data is a regression, even if it shortens a file.

---

## Part II: Refactor Contract

### 2.1 Scope Statement

Define the target components, files, and boundaries. Record the allowed change type.

Allowed change types:
- **Mechanical:** code shape changes only, behaviour preserved.
- **Structural:** reorganisation within the same behaviour and contract.
- **Semantic:** clarifies meaning with stronger types or explicit invariants.
- **Behavioural:** changes output or timing. Requires explicit approval.

### 2.2 Behaviour Baseline

Document the current contract in practical terms:
- Inputs, outputs, and failure modes.
- Expected latency in hot paths, where known.
- External dependencies and data sources.

### 2.3 Performance Budget

Define the performance envelope for hot paths. A refactor should keep the envelope stable or improve it. Any measured regression must be surfaced as a risk.

### 2.4 Evidence Requirements

Each refactor should provide evidence proportional to risk:
- Unit tests for logic and invariants.
- Boundary tests for validation and failure paths.
- Benchmarks for hot paths, where relevant.
- Audit log notes when business behaviour changes.

---

## Part III: Workflow

### 3.1 Orientation

Identify the location of truth. Trace from data entry to output. Summarise the current flow in plain language. This step prevents refactoring the wrong surface.

### 3.2 Boundary Audit

List every boundary and its invariants. Confirm each validation is explicit, local, and complete. Confirm internal logic assumes validated inputs.

### 3.3 Hot Path Mapping

Identify loops, pricing kernels, and frequently called functions. Mark any allocations, virtual calls, and external IO. Use this map to guide mechanical sympathy work.

### 3.4 Refactor Design

Plan the transformation in a minimal sequence. Each step should be reversible and testable. Avoid broad changes that mingle behaviour with structure.

### 3.5 Implementation

Apply changes in small passes:
- First pass: clarify intent, explicit names, explicit types.
- Second pass: localise logic near data.
- Third pass: performance alignment and resource control.

### 3.6 Verification

Run tests or provide equivalent evidence. If tooling cannot run in the current environment, record the constraint and provide a precise command for the operator.

### 3.7 Review and Audit Trail

Record the changes, risks, and follow up actions. Provide a concise audit note for future investigation.

---

## Part IV: Refactor Principles

### 4.1 Mechanical Sympathy

Principles:
- Prefer contiguous data structures and span based access.
- Remove hidden allocations in hot paths.
- Reuse buffers and keep GC pressure predictable.

Example:
```csharp
// Before: repeated allocations inside loop
for (int i = 0; i < n; i++)
{
    var buffer = new double[m];
    Compute(buffer, i);
}

// After: reuse buffer with explicit lifetime
double[] buffer = ArrayPool<double>.Shared.Rent(m);
try
{
    for (int i = 0; i < n; i++)
    {
        Compute(buffer, i);
    }
}
finally
{
    ArrayPool<double>.Shared.Return(buffer);
}
```

### 4.2 Logic Proximity

Principles:
- Keep the calculation close to its data.
- Favour linear flow in complex maths and state transitions.
- Defer abstraction until a second concrete use exists.

Example:
```csharp
// Before: fragmented across multiple helpers
var a = ComputeA(x);
var b = ComputeB(a);
return ComputeC(b, y);

// After: localised computation
double a = x * x + k;
double b = a / (y + epsilon);
return b * factor;
```

### 4.3 Explicit Intent

Principles:
- Use named locals for predicates and thresholds.
- Replace sentinel values with types or explicit results.
- Reserve comments for external references and exceptions.

Example:
```csharp
bool isConverged = error < tolerance;
if (isConverged)
{
    return result;
}
```

### 4.4 Failure First

Principles:
- Validate inputs at the boundary.
- Use deterministic fallbacks with explicit limits.
- Prefer explicit error types or exceptions to silent nulls.

Example:
```csharp
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(steps);
if (iterations >= maxIterations)
{
    return fallback;
}
```

### 4.5 Deterministic Resources

Principles:
- Dispose created resources in the same scope.
- Avoid ambiguous ownership.
- Use clear lifetime boundaries for pooled or shared objects.

Example:
```csharp
using PooledBuffer buffer = new PooledBuffer(size);
Process(buffer);
```

---

## Part V: Evidence and Testing

### 5.1 Unit Tests

Every refactor that touches logic requires tests for:
- invariants at boundaries,
- failure cases, and
- stable outputs for known inputs.

### 5.2 Regression Safety

When behaviour must remain stable, capture it with:
- fixed input fixtures,
- deterministic random seeds,
- numeric tolerances that reflect model sensitivity.

### 5.3 Performance Evidence

For hot paths, add or update benchmarks and compare against the previous envelope. Record any expected deviation with a rationale.

---

## Part VI: Documentation and Knowledge

### 6.1 Alignment with Standards

Confirm that the refactor respects:
- [Alaris Coding Standard](standard.md),
- [Alaris Type System](types.md),
- component specific requirements under `docs/`.

### 6.2 Philosophical Integrity

Ask one question before finalising: does this change make the system more truthful. Truth here means correspondence between code, meaning, and physical execution.

## Appendix: Refactor Checklist

- Purpose and scope defined.
- Boundary invariants explicit.
- Hot paths mapped and protected.
- Logic close to data.
- Explicit naming and types.
- Resource lifetimes deterministic.
- Evidence recorded.
- Documentation aligned.
