using Xunit.Sdk;

namespace APThermo.Execution.Tests;

/// <summary>
/// The step-share rule of the tolerance table without a GPU (Execution.Tests BOOT.md, 2026-10-04): a bound on the whole run, with a
/// coarse guard on each family, driven through a ledger of its own so that these facts do not touch the run's.
/// </summary>
public sealed class StepShareLedgerTests
{
    /// <summary>A family of one station with one forced difference passes the coarse guard, where the old per-family share (1e-3 of one) would have failed it.</summary>
    [Fact]
    public void AFamilyOfOneStationMayDifferInThatStation()
    {
        Assert.Equal(1, StepShareLedger.Allowed(1));
        Assert.Null(StepShareLedger.CoarseViolation(1, 1));
        Assert.Null(StepShareLedger.CoarseViolation(1, 999));
        Assert.Equal(2, StepShareLedger.Allowed(2000));
    }

    /// <summary>A family with more differing stations than the coarse guard allows fails, however small the family.</summary>
    [Fact]
    public void AFamilyOverTheCoarseGuardFails()
    {
        Assert.NotNull(StepShareLedger.CoarseViolation(2, 3));
        Assert.NotNull(StepShareLedger.CoarseViolation(3, 2000));
        Assert.Null(StepShareLedger.CoarseViolation(2, 2000));
    }

    /// <summary>The run-wide share is the table's: a run of 1999 stations with 2 differing fails, with 1 it holds, and the sweep's stations dilute it.</summary>
    [Fact]
    public void ARunOverTheTablesShareFails()
    {
        var ledger = new StepShareLedger();
        ledger.Add(1, 1);
        ledger.Add(0, 998);
        Assert.NotNull(ledger.RunViolation());
        ledger.Add(0, 1);
        Assert.Null(ledger.RunViolation());
        ledger.Add(1, 3);
        Assert.NotNull(ledger.RunViolation());
        ledger.Add(0, 400_000);
        Assert.Null(ledger.RunViolation());
        Assert.Equal(2, ledger.Different);
        Assert.Equal(401_003, ledger.Stations);
    }

    /// <summary>A run that added no station is not a pass: the check walked an empty set.</summary>
    [Fact]
    public void AnEmptyRunFails() => Assert.NotNull(new StepShareLedger().RunViolation());

    /// <summary>The orderer puts the methods named for the whole run after every other method of the class, whatever the names sort to.</summary>
    [Fact]
    public void TheOrdererRunsTheWholeRunCheckLast()
    {
        var type = typeof(OrderSubject);
        var collection = new TestCollection(new TestAssembly(Reflector.Wrap(type.Assembly)), Reflector.Wrap(type), "ordering");
        var testClass = new TestClass(collection, Reflector.Wrap(type));
        var cases = new[] { nameof(OrderSubject.ZLast), nameof(OrderSubject.ZStepShareOverTheWholeRun), nameof(OrderSubject.ALast), nameof(OrderSubject.BFirst) }
            .Select(name => new XunitTestCase(new NullMessageSink(), TestMethodDisplay.Method, TestMethodDisplayOptions.None,
                                              new TestMethod(testClass, Reflector.Wrap(type.GetMethod(name)!))))
            .ToList();
        var ordered = new LastFactOrderer().OrderTestCases(cases).Select(c => c.TestMethod.Method.Name).ToList();
        Assert.Equal([nameof(OrderSubject.ALast), nameof(OrderSubject.BFirst), nameof(OrderSubject.ZLast), nameof(OrderSubject.ZStepShareOverTheWholeRun)], ordered);
    }

    /// <summary>The ledger is thread-safe: concurrent additions lose none.</summary>
    [Fact]
    public void ConcurrentAdditionsLoseNothing()
    {
        var ledger = new StepShareLedger();
        _ = Parallel.For(0, 1000, _ => ledger.Add(1, 10));
        Assert.Equal(1000, ledger.Different);
        Assert.Equal(10_000, ledger.Stations);
    }

    /// <summary>Method names for the orderer fact: reflected, never run.</summary>
    private static class OrderSubject
    {
        /// <summary>Sorts after the first and before the last.</summary>
        public static void ALast()
        {
        }

        /// <summary>Sorts first.</summary>
        public static void BFirst()
        {
        }

        /// <summary>Sorts last by name, and is not a whole-run method.</summary>
        public static void ZLast()
        {
        }

        /// <summary>A whole-run method: runs after every other.</summary>
        public static void ZStepShareOverTheWholeRun()
        {
        }
    }
}
