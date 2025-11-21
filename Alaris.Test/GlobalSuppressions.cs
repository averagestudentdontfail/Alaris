// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Test code legitimately uses Console.WriteLine for diagnostic output
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters",
    Justification = "Test diagnostic output does not require localization")]

// Test code uses System.Random for generating test data (not security-sensitive)
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness",
    Justification = "Test data generation does not require cryptographic randomness")]

// Test helper classes don't need to be sealed
[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Scope = "type",
    Target = "~T:Alaris.Test.Integration.MockMarketDataProvider",
    Justification = "Test mock class, flexibility over performance")]

[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Scope = "type",
    Target = "~T:Alaris.Test.Integration.MockPricingEngine",
    Justification = "Test mock class, flexibility over performance")]

[assembly: SuppressMessage("Performance", "CA1852:Seal internal types",
    Scope = "type",
    Target = "~T:Alaris.Test.Benchmark.Performance+TestCase",
    Justification = "Test data class, flexibility over performance")]

// QuantLib Period objects are short-lived in tests and managed by QuantLib's internal lifecycle
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Justification = "QuantLib Period objects in test scenarios are short-lived and managed by QuantLib",
    Scope = "namespaceanddescendants",
    Target = "~N:Alaris.Test")]

// Date objects created in tests are managed by QuantLib Settings
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
    Scope = "member",
    Target = "~M:Alaris.Test.Integration.StrategyIntegrationTests.Control_WithUnifiedPricingEngine_FullWorkflow~System.Threading.Tasks.Task",
    Justification = "Date object managed by QuantLib Settings")]

// Native library loading requires catching general exceptions to handle platform-specific issues
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types",
    Scope = "member",
    Target = "~M:Alaris.Test.Assembly.LoadLibrary(System.String,System.String,System.String[])",
    Justification = "Native library loading must handle all exceptions to continue test execution")]
