using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Transport.Tests;

/// <summary>Statuses are values: bad inputs are reported, never thrown.</summary>
[Collection(CpuCollection.Name)]
public sealed class InputTests(CpuFixture fixture)
{
    private (SpeciesTable Species, TransportTable Transport) Tables() => TransportHost.TablesOf(fixture, TransportHost.LoadRocket("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));

    private double[] ChamberMoles(SpeciesTable table)
    {
        var c = TransportHost.LoadRocket("lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        return TransportHost.MolesOf(table, TransportHost.StationsWithTransport(c)[0]);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-300.0)]
    [InlineData(double.NaN)]
    public void A_non_positive_temperature_is_invalid_input(double temperature)
    {
        var (species, transport) = Tables();
        var evaluation = TransportHost.Evaluate(fixture.Accelerator, species, transport, temperature, ChamberMoles(species));
        Assert.Equal(CaseStatus.InvalidInput, evaluation.Status);
        Assert.Equal(0.0, evaluation.Figures.Viscosity);
    }

    [Fact]
    public void A_negative_or_undefined_mole_number_is_invalid_input()
    {
        var (species, transport) = Tables();
        var moles = ChamberMoles(species);
        moles[species.IndexOf("H2O")] = -1e-3;
        Assert.Equal(CaseStatus.InvalidInput, TransportHost.Evaluate(fixture.Accelerator, species, transport, 3000.0, moles).Status);
        moles[species.IndexOf("H2O")] = double.NaN;
        Assert.Equal(CaseStatus.InvalidInput, TransportHost.Evaluate(fixture.Accelerator, species, transport, 3000.0, moles).Status);
    }

    [Fact]
    public void No_gaseous_moles_is_no_transport_data()
    {
        var (species, transport) = Tables();
        var evaluation = TransportHost.Evaluate(fixture.Accelerator, species, transport, 3000.0, new double[species.SpeciesCount]);
        Assert.Equal(CaseStatus.NoTransportData, evaluation.Status);
        Assert.Equal(0, evaluation.Figures.SpeciesCount);
    }

    [Fact]
    public void A_transport_table_of_another_species_table_is_invalid_input()
    {
        var (species, _) = Tables();
        var other = TransportHost.TablesOf(fixture, TransportHost.LoadRocket("nto-udmh_of2.2_pc2MPa_shiftingEquilibrium")).Transport;
        Assert.NotEqual(species.SpeciesCount, other.SpeciesCount);
        var evaluation = TransportHost.Evaluate(fixture.Accelerator, species, other, 3000.0, ChamberMoles(species));
        Assert.Equal(CaseStatus.InvalidInput, evaluation.Status);
    }

    [Fact]
    public void A_pure_gas_has_its_own_fit_values_and_no_reactions()
    {
        var (species, transport) = Tables();
        var moles = new double[species.SpeciesCount];
        var h2 = species.IndexOf("H2");
        moles[h2] = 1.0 / species.Arrays.MolarMass[h2];
        var evaluation = TransportHost.Evaluate(fixture.Accelerator, species, transport, 1500.0, moles);
        Assert.Equal(CaseStatus.Ok, evaluation.Status);
        using var buffers = TransportTableBuffers.Upload(fixture.Accelerator, transport);
        var view = buffers.View;
        Assert.Equal(TransportSolver.PureViscosity(in view, h2, 1500.0), evaluation.Figures.Viscosity);
        Assert.Equal(TransportSolver.PureConductivity(in view, h2, 1500.0), evaluation.Figures.FrozenConductivity);
        Assert.Equal(evaluation.Figures.FrozenConductivity, evaluation.Figures.ReactingConductivity);
        Assert.Equal(0, evaluation.Figures.ReactionCount);
        Assert.Equal(0, evaluation.Figures.EstimatedSpeciesCount);
    }

    [Fact]
    public void Scratch_layout_slices_the_declared_sizes()
    {
        var (species, _) = Tables();
        var doubles = TransportLayout.DoublesPerCase(species.SpeciesCount, species.ElementCount);
        var ints = TransportLayout.IntsPerCase(species.SpeciesCount, species.ElementCount);
        var m = TransportLayout.MaxSpecies;
        Assert.Equal(4 * m * m + species.ElementCount * m + 8 * m, doubles);
        Assert.Equal(species.SpeciesCount + 4 * m + 4 * species.ElementCount, ints);
        using var doubleBuffer = fixture.Accelerator.Allocate1D<double>(doubles);
        using var intBuffer = fixture.Accelerator.Allocate1D<int>(ints);
        var scratch = TransportScratch.Slice(doubleBuffer.View, intBuffer.View, species.SpeciesCount, species.ElementCount);
        Assert.Equal(m, (int)scratch.Stx.Length);
        Assert.Equal(species.SpeciesCount, (int)scratch.Mark.Length);
        Assert.Equal(species.ElementCount, (int)scratch.RowActive.Length);
    }
}
