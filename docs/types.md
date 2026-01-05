# Alaris Type System

> "The beginning of wisdom is the definition of terms."
> — Socrates

## Preface

Types are the vocabulary of Alaris. They express meaning, enforce constraints, and make code auditable. This document builds from fundamental ideas about values and identity, through numeric and temporal primitives, and into the domain types used in trading systems. It also carries the philosophical position that every type is a claim about reality; a careless type is a careless claim.

The goal is clarity: a type should state what a value means, how it behaves, and what it forbids. The goal is integrity: a type should make invalid states hard to represent and easy to detect.

---

## Part I: First Principles

### 1.1 Value, Identity, and State

**Value** is a datum that stands on its own. Examples include a volatility estimate, a strike price, and a timestamp.

**Identity** refers to an entity that persists across time. A position has an identity; its price and risk metrics are values that change.

**State** is the collection of values that describe an identity at a given time. State must be explicit and traceable; hidden state erodes auditability.

### 1.2 Invariants and Boundaries

An invariant is a rule that must remain true for a type. The system enforces invariants at boundaries. Inside a validated boundary, code can assume validity and remain efficient.

Example invariants:
- A price is positive.
- An expiry is after the valuation timestamp.
- A probability lies in [0, 1].

### 1.3 Meaning and Commitment

A type is a commitment to meaning. It is a promise about units, scale, and allowed operations. This commitment has ethical weight in a trading system; untrue promises can become financial errors.

---

## Part II: Primitive Types

### 2.1 Boolean

Boolean values represent decisions and predicates. They should be named to show intent, especially when a predicate is complex.

```csharp
bool isConverged = error < tolerance;
```

### 2.2 Integer

Integers represent counts, indices, and discrete quantities. They are appropriate for sizes, iterations, and contract quantities.

Use `int` for indices and sizes; use `long` for counters that can exceed two billion.

### 2.3 Floating Point

Floating types represent continuous mathematical quantities. Use `double` for dimensionless and model-centric values such as variance, drift, and numerical parameters.

Avoid floating types for money and prices. Monetary values are exact in decimal arithmetic.

### 2.4 Decimal

`decimal` represents base ten arithmetic. All monetary values and prices use `decimal`. This rule enforces financial integrity and prevents cumulative rounding error.

### 2.5 Character and String

Strings represent identifiers and human readable text. Avoid stringly typed domain values; prefer explicit types such as `Symbol`, `Exchange`, and `Currency`.

---

## Part III: Quantities and Units

### 3.1 Dimensioned and Dimensionless Values

Dimensioned values carry units; dimensionless values do not. A rate per annum carries time; a strike price carries currency. Use types and naming to make units explicit.

Example:

```csharp
public readonly record struct AnnualRate(decimal Value);
public readonly record struct Volatility(decimal Value);
public readonly record struct Years(decimal Value);
```

### 3.2 Money and Prices

Money represents currency quantities and uses `decimal`. Prices are money per unit and also use `decimal`.

Guidance:
- Monetary amounts: `decimal` with explicit rounding.
- Prices from external feeds: validate at the boundary.
- Currency: represent with a strong type or enum, not a string.

### 3.3 Rates and Volatility

Rates and volatilities are dimensioned values. Represent them as explicit types, and include scale in naming.

Examples:
- `AnnualRate` for yearly rate.
- `DailyVolatility` for daily volatility.
- `ImpliedVolatility` for market derived volatility.

### 3.4 Counts and Indices

Contract quantities, steps, and indices are discrete. Use integers and ensure that zero and negative values are handled deliberately.

### 3.5 Time, Date, and Duration

Time carries both chronology and calendar structure. Use `DateOnly` for dates, `TimeOnly` for time of day, `DateTimeOffset` for absolute time, and `TimeSpan` for durations.

Rules:
- Store market events in UTC with offsets.
- Use `DateOnly` for expiry dates.
- Use `TimeSpan` for intervals and tenors.

---

## Part IV: Domain Types

### 4.1 Symbols and Identifiers

Market identifiers represent identity and must be explicit types. A symbol is a domain object; a string is only a transport container.

```csharp
public readonly record struct Symbol(string Value);
public readonly record struct CorrelationId(Guid Value);
```

### 4.2 Option Contracts

An option contract has a strike, expiry, and option type. These are separate types with explicit invariants.

```csharp
public enum OptionSide { Call, Put }

public sealed record OptionContract(
    Symbol Underlying,
    decimal Strike,
    DateOnly Expiry,
    OptionSide Side
);
```

### 4.3 Pricing Inputs and Results

Pricing inputs are structured types that capture provenance. Pricing results preserve inputs and expose the model and timestamp.

```csharp
public sealed record PricingInputs(
    decimal Spot,
    decimal Strike,
    decimal Rate,
    decimal DividendYield,
    decimal Volatility,
    DateOnly Expiry
);

public sealed record PricingResult(
    decimal Price,
    PricingInputs Inputs,
    string Model,
    DateTimeOffset ComputedAt
);
```

### 4.4 Results and Errors

Failure is part of the type system. Use `Result<T, TError>` or explicit error types; reserve exceptions for boundary failures and programming errors.

```csharp
public readonly record struct PricingError(string Code, string Message);
public readonly record struct Result<T, TError>(T? Value, TError? Error);
```

### 4.5 Configuration Types

Configuration values are typed and validated at startup. Use `record` types for configuration sections; validate ranges with attributes or explicit checks.

### 4.6 Observability Types

Correlation IDs, run IDs, and session IDs are types. They are part of the data model and should be propagated through the system.

---

## Part V: Collection Types

### 5.1 Arrays and Spans

Arrays represent contiguous memory and support data locality. Use `Span<T>` and `ReadOnlySpan<T>` for hot paths and interop boundaries.

### 5.2 Lists and Dictionaries

Lists provide flexible storage; dictionaries provide keyed access. Use dictionaries for access patterns that require lookup by key; use lists for ordered data and iteration.

### 5.3 Sequences and Lazy Evaluation

Enumerable sequences are useful for clarity and composition. Avoid deferred execution in performance critical loops; evaluate explicitly when needed.

---

## Part VI: Concurrency and Mutability

### 6.1 Immutable Types

Immutability protects against concurrent corruption. Prefer `record` and `readonly` structs for value objects. Mutable state should live behind clear boundaries.

### 6.2 Thread Safe Collections

Use `ConcurrentDictionary` and other thread safe collections for shared mutable state. Do not share mutable state without synchronisation.

---

## Part VII: Serialization and Boundaries

### 7.1 DTOs and Domain Types

Transport types are distinct from domain types. Convert at boundaries; validate on entry; store domain types internally.

### 7.2 Versioning

Use explicit version fields in serialised formats. Readers should be tolerant of new fields; writers should be conservative with breaking changes.

---

## Part VIII: Type Rules

These rules align with the coding standard and are expressed for types. See [Standard](standard.md) for the full conventions.

| Rule | Level | Statement |
|------|-------|-----------|
| Monetary precision | MUST | Monetary values and prices use `decimal` with explicit rounding. |
| Boundary validation | MUST | External inputs validate invariants at entry. |
| Failure expression | MUST | Failures return a Result or raise an exception; silence is forbidden. |
| Immutable values | SHOULD | Domain value objects are immutable. |
| Strong identifiers | SHOULD | Domain identifiers use explicit types, not bare strings. |
| Hot path locality | SHOULD | Use contiguous collections and spans where performance matters. |
| Explicit conversions | SHOULD | Conversions between units and scales are explicit and documented. |

---

## Appendix A: Examples of Good Type Names

- `AnnualRate`
- `SpotPrice`
- `ImpliedVolatility`
- `ExpiryDate`
- `TradeQuantity`
- `CorrelationId`
- `PricingResult`

---

## Appendix B: Checklist

Before adding or modifying a type, verify:

- [ ] The name expresses meaning and units.
- [ ] Invariants are enforced at the boundary.
- [ ] Monetary values use `decimal`.
- [ ] Time and date types are explicit.
- [ ] Domain identifiers are strong types.
- [ ] Failure modes are encoded in the type or result.
