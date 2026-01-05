# Alaris Documentation

This directory contains the comprehensive documentation for the Alaris trading system. The documents are designed to be read in order, building from philosophical foundations through mathematical theory to practical operation.

## Document Overview

| Document | Purpose | Audience |
|----------|---------|----------|
| [Philosophy](philosophy.md) | Why the system exists; design principles | All readers |
| [Foundations](foundations.md) | Mathematical theory | Quantitative practitioners |
| [Types](types.md) | Type system, units, and domain modelling | Developers |
| [Specification](specification.md) | Formal system requirements | Implementers |
| [Guide](guide.md) | Practical operation | Operators |
| [Standard](standard.md) | Coding conventions | Developers |

## Reading Order

**For understanding the system:**
1. Philosophy: establishes the worldview and design rationale
2. Foundations: develops the mathematical machinery
3. Types: builds the type system from primitives to domain abstractions
4. Guide: explains practical operation

**For contributing to the codebase:**
1. Philosophy: understand the design principles
2. Types: understand the type system and invariants
3. Standard: learn the coding conventions
4. Specification: understand the formal requirements

**For operating the system:**
1. Guide: primary reference for daily operation
2. Specification: reference for configuration and limits

## Document Summaries

### Philosophy

Explores the philosophical foundations: market behaviour, edge, risk versus volatility, uncertainty, and the principles of high-integrity design. Reading this document illuminates why Alaris makes the design decisions it does.

### Foundations

Develops the mathematical theory from first principles: probability, stochastic processes, option pricing, volatility estimation, and spectral methods. This document provides the theoretical grounding for all computational methods.

### Types

Defines the type system from first principles through domain abstractions, with guidance on units, invariants, and boundary validation.

### Specification

Provides formal definitions of system behaviour: signal criteria, position sizing rules, risk limits, pricing engine requirements, and configuration parameters. This document serves as the authoritative reference for implementation correctness.

### Guide

Covers practical patterns for signal interpretation, position management, risk control, backtesting, and troubleshooting. This document bridges theory and operation.

### Standard

Codifies conventions for high-integrity trading system development: financial precision, error handling, immutability, testing, logging, and code organisation. This document establishes the quality standards for all code contributions.
