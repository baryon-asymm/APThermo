using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>L1: condensed species enter and leave the solution as in the reference.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class CondensedSpeciesTests
{
    private static readonly string[] Kinds = ["tp", "hp", "sp"];

    /// <summary>The fixture cases whose product list has condensed species, over all three kinds.</summary>
    public static TheoryData<string, string> CasesWithCondensedCandidates()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in Kinds)
        {
            foreach (var name in HostSolver.CaseNames(kind))
            {
                var c = HostSolver.Load(kind, name);
                if (HostSolver.ProductsOf(c).Any(IsCondensedName))
                {
                    data.Add(kind, name);
                }
            }
        }

        return data;
    }

    /// <summary>The condensed species in the solution are those of the reference.</summary>
    [Theory]
    [MemberData(nameof(CasesWithCondensedCandidates))]
    public void TheCondensedSpeciesInTheSolutionAreThoseOfTheReference(string kind, string name)
    {
        var c = HostSolver.Load(kind, name);
        var solution = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        var (present, absent) = StateComparison.CondensedSetOf(c, solution.Case.Table, CpuFixture.Shared.Tolerances);
        var missing = present.Where(s => !(solution.Case.Table.IndicesOf(s).Sum(j => solution.Moles[j]) > 0.0)).ToList();
        var spurious = absent.Where(s => solution.Case.Table.IndicesOf(s).Sum(j => solution.Moles[j]) != 0.0).ToList();
        Assert.True(missing.Count == 0 && spurious.Count == 0,
                    $"missing [{string.Join(", ", missing)}], spurious [{string.Join(", ", spurious)}]; reference present [{string.Join(", ", present)}]");
    }

    /// <summary>The aluminized propellant burns to liquid alumina in the chamber.</summary>
    [Fact]
    public void TheAluminizedPropellantBurnsToLiquidAluminaInTheChamber()
    {
        var c = HostSolver.Load("hp", "ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber");
        var solution = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        var expected = c.Outputs.GetProperty("moleFractions").GetProperty("AL2O3(L)").GetDouble();
        Assert.True(expected > 0.0, "the reference has no liquid alumina in the chamber");
        var index = solution.Case.Table.IndexOf("AL2O3(L)");
        Assert.True(index >= solution.Case.Table.GasCount && solution.Moles[index] > 0.0, "liquid alumina is not in the solution");
        Assert.True(CpuFixture.Shared.Tolerances.Matches("moleFraction", expected, solution.MoleFraction("AL2O3(L)")),
                    $"x(AL2O3(L)): reference {expected:R}, tree {solution.MoleFraction("AL2O3(L)"):R}");
    }

    /// <summary>Water condenses below its dew point in the low temperature example.</summary>
    [Fact]
    public void WaterCondensesBelowItsDewPointInTheLowTemperatureExample()
    {
        var withLiquid = new List<string>();
        var dry = new List<string>();
        foreach (var name in HostSolver.CaseNames("tp"))
        {
            if (!name.StartsWith("rp1311-example14", StringComparison.Ordinal))
            {
                continue;
            }

            var c = HostSolver.Load("tp", name);
            var solution = HostSolver.Solve(CpuFixture.Shared, c);
            Assert.Equal(CaseStatus.Ok, solution.Status);
            var liquid = solution.Moles[solution.Case.Table.IndexOf("H2O(L)")] > 0.0;
            var referenceLiquid = c.Outputs.GetProperty("moleFractions").GetProperty("H2O(L)").GetDouble() > 0.0;
            Assert.True(liquid == referenceLiquid, $"{name}: liquid water present {liquid}, reference {referenceLiquid}");
            (liquid ? withLiquid : dry).Add(name);
        }

        Assert.True(withLiquid.Count > 0 && dry.Count > 0, "the example must have cases on both sides of the dew point");
    }

    private static bool IsCondensedName(string species) => species.EndsWith(')') && species.Contains('(');
}
