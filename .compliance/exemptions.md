# Coding Standard Exemptions

**Document Version**: 1.0
**Last Updated**: 2025-11-20

---

## Purpose

This document tracks approved exemptions from the Alaris High-Integrity Coding Standard. All exemptions must be:
1. Technically justified
2. Documented with rationale
3. Reviewed and approved
4. Re-evaluated periodically

---

## Active Exemptions

### EX-001: PriceOptionSync Method Length

**Rule**: Rule 13 (Small Functions - Max 60 lines)

**Location**: `Alaris.Strategy/Bridge/UnifiedPricingEngine.cs:567`

**Method**: `private double PriceOptionSync(OptionParameters parameters)`

**Current Length**: 66 lines

**Violation**: Exceeds 60-line limit by 6 lines

**Justification**:
- Method is critical for memory safety (Rule 16 compliance)
- ~21 lines (32%) are disposal calls for 14 QuantLib objects
- Disposal order is critical (reverse of creation) and must be explicit
- Breaking into helper methods would:
  - Obscure the disposal pattern
  - Increase risk of missing disposal calls
  - Reduce clarity for maintainers
  - Create false sense of complexity reduction
- **Actual logic**: ~45 lines (within threshold)
- **Boilerplate disposal**: ~21 lines (not complexity)

**Decision**: **APPROVED** - Accept as-is

**Rationale**: This method is the reference implementation for Rule 16 (Deterministic Cleanup), which is **more critical** than Rule 13. Memory safety takes precedence over line count.

**Alternative Considered**: Extract disposal into helper method
**Rejected Because**: Would hide critical cleanup logic and increase maintenance risk

**Review Date**: 2025-11-20
**Approved By**: Initial Assessment
**Next Review**: 2026-02-20 (3 months)

---

## Exemption Request Template

```markdown
### EX-XXX: [Short Description]

**Rule**: [Rule number and name]

**Location**: [File:Line]

**Method/Class**: [Name]

**Violation**: [What rule is being violated]

**Justification**: [Why exemption is necessary]

**Decision**: [APPROVED / REJECTED / UNDER REVIEW]

**Rationale**: [Detailed reasoning]

**Alternative Considered**: [What was tried instead]

**Rejected Because**: [Why alternative doesn't work]

**Review Date**: [Date]
**Approved By**: [Name/Role]
**Next Review**: [Future date]
```

---

## Exemption Guidelines

### When to Request Exemption:

1. **Technical Necessity**: Following the rule would compromise safety, correctness, or performance
2. **Third-Party Constraints**: External library requirements conflict with rules
3. **Generated Code**: Auto-generated code that cannot be modified
4. **Temporary**: Time-limited exemption during refactoring

### When NOT to Request Exemption:

1. **Convenience**: "It's easier to write this way"
2. **Lack of Understanding**: "I don't understand why this rule exists"
3. **Time Pressure**: "We don't have time to fix this now" (use technical debt instead)
4. **Disagreement**: "I don't think this rule is important" (discuss with team first)

---

## Review Process

### Quarterly Review (Every 3 Months):

1. Re-evaluate all active exemptions
2. Check if technical constraints have changed
3. Determine if refactoring is now feasible
4. Update or remove exemptions as needed

### Annual Review (Every 12 Months):

1. Full re-assessment of exemption policy
2. Review exemption patterns for systemic issues
3. Update exemption guidelines based on experience
4. Report exemption statistics to stakeholders

---

## Exemption Statistics

| Period | Total Exemptions | Approved | Rejected | Expired |
|--------|-----------------|----------|----------|---------|
| 2025-Q4 | 1 | 1 | 0 | 0 |

---

## Expired Exemptions

### EX-TEMPLATE: Example Expired Exemption

**Rule**: [Rule number]

**Location**: [File:Line]

**Status**: **EXPIRED** - Refactored in commit `abc1234`

**Original Justification**: [Why it was needed]

**Resolution**: [How it was fixed]

**Expired Date**: [Date]

---

*All exemptions must be documented here. Undocumented violations will be treated as technical debt and flagged for remediation.*
