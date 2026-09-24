using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>L0: an element with zero abundance masks its species; the result equals a table without them, bit for bit.</summary>
[Collection(CpuCollection.Name)]
public sealed class AbsentElementTests(CpuFixture fixture)
{
    [Theory]
    [InlineData("tp", "rp1311-example1_r1.0_p1.0atm_T3000", "C")]
    [InlineData("hp", "rp1311-example3_p10bar", "AR")]
    [InlineData("sp", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium_throat", "C")]
    public void A_zero_abundance_equals_a_table_without_the_element(string kind, string name, string element)
    {
        var c = HostSolver.Load(kind, name);
        var elements = HostSolver.ElementsOf(c);
        var products = HostSolver.ProductsOf(c);
        var elementMoles = HostSolver.ElementMolesOf(c);
        var removed = Array.IndexOf(elements, element);
        Assert.True(removed >= 0, $"{element} is not an element of the case");

        var masked = (double[])elementMoles.Clone();
        masked[removed] = 0.0;
        var full = HostSolver.Solve(fixture.Accelerator,
                                    HostSolver.Of(HostSolver.BuildTable(fixture.Database, c), c) with { ElementMoles = masked });

        var keptElements = elements.Where(e => e != element).ToArray();
        var keptProducts = products.Where(p => fixture.Database[p].Formula.All(
            f => !string.Equals(f.Symbol, element, StringComparison.OrdinalIgnoreCase))).ToArray();
        Assert.True(keptProducts.Length < products.Length, "the element must remove at least one species");
        var reducedTable = SpeciesTable.Build(fixture.Database, keptElements, keptProducts);
        var reducedMoles = masked.Where((_, i) => i != removed).ToArray();
        var reduced = HostSolver.Solve(fixture.Accelerator, HostSolver.Of(reducedTable, c) with { ElementMoles = reducedMoles });

        Assert.Equal(CaseStatus.Ok, full.Status);
        Assert.Equal(reduced.Status, full.Status);
        Assert.Equal(reduced.Iterations, full.Iterations);
        foreach (var species in products)
        {
            var fullMoles = full.Moles[full.Case.Table.IndexOf(species)];
            var reducedIndex = reducedTable.IndexOf(species);
            var expected = reducedIndex < 0 ? 0.0 : reduced.Moles[reducedIndex];
            Assert.True(BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(fullMoles),
                        $"{species}: masked {fullMoles:R}, reduced {expected:R}");
        }

        foreach (var e in keptElements)
        {
            var fullValue = full.Multipliers[Array.IndexOf(elements, e)];
            var reducedValue = reduced.Multipliers[Array.IndexOf(keptElements, e)];
            Assert.True(BitConverter.DoubleToInt64Bits(fullValue) == BitConverter.DoubleToInt64Bits(reducedValue), $"multiplier of {e} differs");
        }

        Assert.Equal(0.0, full.Multipliers[removed]);
        foreach (var field in typeof(MixtureState).GetProperties())
        {
            var a = (double)field.GetValue(full.State)!;
            var b = (double)field.GetValue(reduced.State)!;
            Assert.True(BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b), $"{field.Name}: masked {a:R}, reduced {b:R}");
        }
    }
}
