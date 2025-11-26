# Alaris Structure Compliance

## Overview
This document defines the structural compliance rules for the Alaris project. Adherence to these rules is mandatory for all components.

## Directory Structure
The project follows a domain-driven directory structure:

- **Alaris.Double**: Double boundary pricing domain.
- **Alaris.Strategy**: Trading strategy domain.
- **Alaris.Events**: Event sourcing and audit logging domain.
- **Alaris.Governance**: Project governance, compliance, and documentation.
- **Alaris.Test**: Unit and integration tests.
- **Alaris.Quantlib**: QuantLib bindings and extensions.

## Naming Convention
All components must follow the `NAMING-CONVENTION.md` located in the root directory.
- **Domain Code**: 2 letters (e.g., DB, ST).
- **Category Code**: 2 letters (e.g., AP, IV).
- **Sequence**: 3 digits (e.g., 001).
- **Variant**: 1 letter (e.g., A).

Example: `DBAP001A` (Double Boundary Approximation 001 Variant A).

## File Placement
- Core logic must reside in the appropriate domain project.
- Documentation must be placed in `Alaris.Governance/Documentation`.
- Compliance rules must be placed in `Alaris.Governance/Compliance`.
