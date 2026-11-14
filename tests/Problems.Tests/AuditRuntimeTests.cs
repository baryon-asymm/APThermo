using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Thermo;
using System.Runtime.CompilerServices;

namespace APThermo.Problems.Tests;

/// <summary>Public runtime witnesses preserved from the November audit.</summary>
[Collection(SolverFixture.CollectionName)]
public sealed class AuditRuntimeTests
{
    /// <summary>An unknown control-character name must not alias two distinct Omit names.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CacheHistoryCannotChangeTheRequestedProductSet(bool reverse)
    {
        var database = SolverFixture.Shared.Database;
        var elements = new Dictionary<string, double> { ["O"] = 1000.0 / database.AtomicWeight("O") };
        var first = ElementalMixture.Create(elements, omit: ["O2\u001fzz"]);
        var second = ElementalMixture.Create(elements, omit: ["O2", "zz"]);
        var problem = new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 100000.0, Temperature = 1100.0 };
        using var warmed = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var fresh = Solver.Create(database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var target = reverse ? first : second;
        _ = warmed.Solve(reverse ? second : first, problem);
        var actual = warmed.Solve(target, problem);
        var expected = fresh.Solve(target, problem);
        Assert.Equal(CaseStatus.Ok, actual.Status);
        Assert.Equal(expected.State.State.Enthalpy, actual.State.State.Enthalpy);
        Assert.Equal(expected.State.MoleFractions.GetValueOrDefault("O2"), actual.State.MoleFractions.GetValueOrDefault("O2"));
    }

    /// <summary>Scaling a singleton role's amount cannot erase the role from a supported mixture.</summary>
    [Fact]
    public void ASubnormalSingletonAmountPreservesItsGroupShare()
    {
        var solver = SolverFixture.Shared.Solver;
        var database = SolverFixture.Shared.Database;
        var normal = Propellant.From(database).Oxidizer("O2").Fuel("H2").OxidizerToFuelRatio(0.005).Build();
        var tiny = Propellant.From(database).Oxidizer("O2", amount: double.Epsilon).Fuel("H2").OxidizerToFuelRatio(0.005).Build();
        var expected = solver.MixtureOf(normal);
        var actual = solver.MixtureOf(tiny);
        Assert.Equal(expected.ElementMoles["O"], actual.ElementMoles["O"]);
        Assert.Equal(expected.ElementMoles["H"], actual.ElementMoles["H"]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Propellant>[] CachePropellants(Solver solver)
    {
        var references = new WeakReference<Propellant>[16];
        for (var index = 0; index < references.Length; index++)
        {
            var propellant = Propellant.From(solver.Database).Named("O2", 1.0).Build();
            _ = solver.MixtureOf(propellant);
            references[index] = new WeakReference<Propellant>(propellant);
        }

        return references;
    }
    /// <summary>A still-reachable disposed solver must release cached propellant definitions.</summary>
    [Fact]
    public void DisposingAReachableSolverReleasesCachedPropellants()
    {
        var solver = Solver.Create(SolverFixture.Shared.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var references = CachePropellants(solver);
        solver.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.All(references, reference => Assert.False(reference.TryGetTarget(out _)));
        GC.KeepAlive(solver);
    }

    /// <summary>Undefined role and amount units are rejected at the reactant boundary.</summary>
    [Fact]
    public void UndefinedReactantEnumsAreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() => Reactant.FromDatabase("O2", (ReactantRole)999, 1.0));
        _ = Assert.Throws<ArgumentException>(() => Reactant.FromDatabase("O2", ReactantRole.Named, 1.0, amountKind: (AmountKind)999));
    }
}
