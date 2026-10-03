namespace APThermo.Protocol.Tests;

/// <summary>
/// End-to-end level (root <c>BOOT.md</c>, Test time budgets, 2026-10-03): a fact that starts a process is in the end-to-end
/// set, <c>Category=EndToEnd</c>, so the fast set (<c>Category!=LongRunning&amp;Category!=EndToEnd</c>) stays inside its budget
/// by construction and a new process fact cannot join it unnoticed.
/// </summary>
public sealed class EndToEndTests
{
    private const string Category = "EndToEnd";

    /// <summary>
    /// Every test method that reaches a process start (<see cref="ProcessStarts"/>) carries <c>[Trait("Category", "EndToEnd")]</c>,
    /// on the method or a type around it. Fails when the walk finds no test method at all and when it finds none that starts a
    /// process: a walk that finds nothing would prove nothing.
    /// </summary>
    [Fact]
    public void EveryFactThatStartsAProcessCarriesEndToEnd()
    {
        var tests = ProcessStarts.TestMethods().ToList();
        Assert.NotEmpty(tests);
        var starting = tests.Where(ProcessStarts.StartsAProcess).ToList();
        Assert.NotEmpty(starting);
        var problems = starting
            .Where(test => !ProcessStarts.CarriesCategory(test, Category))
            .Select(test => $"{test.DeclaringType!.FullName}.{test.Name} starts a process and carries no [Trait(\"Category\", \"{Category}\")]")
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(problems.Count == 0,
            "root BOOT.md, Test time budgets: a fact that starts a process carries Category=EndToEnd.\n" + string.Join("\n", problems));
    }
}
