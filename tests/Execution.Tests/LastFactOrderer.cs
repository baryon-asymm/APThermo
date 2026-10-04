using Xunit.Abstractions;
using Xunit.Sdk;

namespace APThermo.Execution.Tests;

/// <summary>
/// Orders the test cases of a class by method name, with the cases of methods whose name ends with <see cref="LastSuffix"/> after all the
/// others: the run-wide check of <see cref="StepShareLedger"/> must see every family of its class (xunit 2.9 runs one class's cases
/// one after the other, in the order its orderer gives).
/// </summary>
internal sealed class LastFactOrderer : ITestCaseOrderer
{
    /// <summary>The end of the name of a method that runs after every other method of its class.</summary>
    public const string LastSuffix = "OverTheWholeRun";

    /// <summary>The cases in the order they run: the others by method name, then the last ones.</summary>
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        return testCases
            .OrderBy(c => c.TestMethod.Method.Name.EndsWith(LastSuffix, StringComparison.Ordinal))
            .ThenBy(c => c.TestMethod.Method.Name, StringComparer.Ordinal);
    }
}
