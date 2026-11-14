using APThermo.Data;
using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>The solved throat, including the post-decision refinement, agrees with an independent sonic reference.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class SonicTailExecutionTests
{
    private const double ChamberTemperature = 3000.0;
    private const double ChamberPressure = 1.0e6;
    private const int ReferencePanels = 256;

    /// <summary>The independent quadrature reproduces the analytic monoatomic gas result.</summary>
    [Fact]
    public void TheArgonReferenceAndSolvedThroatMatchTheAnalyticSonicState()
    {
        var species = CpuFixture.Shared.Database["Ar"];
        const double temperature = 600.0;
        var fit = Assert.Single(species.Intervals, interval => interval.TLow <= temperature / 2.0 && interval.THigh >= temperature);
        Assert.All(fit.Coefficients.Select((coefficient, index) => (coefficient, exponent: fit.Exponents[index])), term =>
            Assert.Equal(term.exponent == 0.0 ? 2.5 : 0.0, term.coefficient));
        var gamma = 5.0 / 3.0;
        var ratio = 2.0 / (gamma + 1.0);
        var reference = Reference(species, temperature);
        AssertReference(temperature * ratio, reference.Temperature, "analytic reference temperature");
        AssertReference(Math.Pow(ratio, gamma / (gamma - 1.0)), reference.PressureRatio, "analytic reference pressure ratio");
        AssertSolvedState(species, reference, temperature);
    }

    /// <summary>Variable Cp puts the decision before the sonic answer; skipping the tail must fail the physical state comparison.</summary>
    [Fact]
    public void TheNitrogenThroatMatchesTheIndependentVariableHeatCapacitySonicState()
    {
        var species = CpuFixture.Shared.Database["N2"];
        Assert.NotEqual(SingleGasSonicReference.CpOverR(species, ChamberTemperature),
            SingleGasSonicReference.CpOverR(species, ChamberTemperature / 2.0));
        AssertSolvedState(species, Reference(species));
    }

    private static SingleGasSonicReference Reference(Species species, double temperature = ChamberTemperature)
    {
        _ = Assert.Single(species.Intervals, fit => fit.TLow <= temperature / 2.0 && fit.THigh >= temperature);
        var coarse = new SingleGasSonicReference(species, temperature, ReferencePanels);
        var fine = new SingleGasSonicReference(species, temperature, ReferencePanels * 2);
        AssertReference(fine.Temperature, coarse.Temperature, "quadrature refinement temperature");
        AssertReference(fine.PressureRatio, coarse.PressureRatio, "quadrature refinement pressure ratio");
        return fine;
    }

    private static void AssertSolvedState(Species species, SingleGasSonicReference reference, double temperature = ChamberTemperature)
    {
        var fixture = CpuFixture.Shared;
        var element = Assert.Single(species.Formula);
        var table = SpeciesTable.Build(fixture.Database, [element.Symbol], [species.Name]);
        using var buffers = SpeciesTableBuffers.Upload(fixture.Accelerator, table);
        // Production H is used only to prescribe the chamber input; the sonic reference integrates raw Cp independently.
        var view = buffers.View;
        var enthalpy = SpeciesFunctions.HOverRT(in view, 0, temperature) * PhysicalConstants.R * temperature / species.MolarMass;
        var inputs = new RocketInputs(new ChemicalSystem([element.Symbol], [species.Name]),
            new Mixture([element.Count / species.MolarMass], enthalpy), ChamberPressure, FlowModel.FrozenAtChamber, new ExitPlan([], []));
        var solution = RocketHost.Solve(fixture, inputs);
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.All(solution.Outcome.StationStatus, status => Assert.Equal(CaseStatus.Ok, status));
        AssertRelative(temperature, solution.Outcome.Stations[RocketSolver.Chamber].Temperature, "prescribed chamber temperature");
        var throat = solution.Outcome.Stations[RocketSolver.Throat];
        AssertRelative(reference.Temperature, throat.Temperature, species.Name + " sonic throat temperature");
        AssertRelative(reference.PressureRatio, throat.Pressure / ChamberPressure, species.Name + " sonic throat pressure ratio");
    }

    private static void AssertReference(double expected, double actual, string field) =>
        Assert.True(Math.Abs(actual / expected - 1.0) <= RocketInvariants.PressureRatioTolerance,
            $"{field}: reference {expected:R}, measured {actual:R}");

    private static void AssertRelative(double expected, double actual, string field) =>
        Assert.True(CpuFixture.Shared.Tolerances.Matches("polishThresholdRelative", expected, actual),
            $"{field}: independent {expected:R}, measured {actual:R}; relative error {Math.Abs(actual / expected - 1.0):R}");
}
