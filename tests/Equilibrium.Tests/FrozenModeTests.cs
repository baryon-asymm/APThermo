using System.Text.Json;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>L1: frozen mode against the frozen stations of the reference rocket cases and against the equilibrium solve itself.</summary>
[Collection(CpuCollection.Name)]
public sealed class FrozenModeTests(CpuFixture fixture)
{
    /// <summary>
    /// Station outputs not compared: the reference computes no Cv at a frozen station (it prints zero or the previous station's
    /// value; see the fixtures node), its equilibrium Cp is the frozen one there (compared separately), and the flow speed is
    /// the performance node's.
    /// </summary>
    private static readonly HashSet<string> NotFrozenFields = ["cpEquilibrium", "cvEquilibrium", "cvFrozen", "mach"];

    /// <summary>The rocket fixtures whose flow is frozen somewhere.</summary>
    public static IEnumerable<object[]> FrozenRocketCases() =>
        FixtureFiles.Enumerate("rocket")
            .Select(path => (Name: Path.GetFileNameWithoutExtension(path), Case: CeaFixtures.Load(path)))
            .Where(x => x.Case.Outputs.GetProperty("stations").EnumerateArray().Any(s => s.GetProperty("frozen").GetBoolean()))
            .Select(x => new object[] { x.Name });

    [Theory]
    [MemberData(nameof(FrozenRocketCases))]
    public void Frozen_stations_of_the_reference_are_reproduced_from_the_frozen_composition(string name)
    {
        var c = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", name + ".json"));
        var stations = c.Outputs.GetProperty("stations").EnumerateArray().ToList();
        var source = stations.Last(s => !s.GetProperty("frozen").GetBoolean());
        var table = SpeciesTable.Build(fixture.Database, HostSolver.ElementsOf(c), HostSolver.ProductsOf(c));

        // The composition is frozen at the last equilibrium station: moles per kilogram from its mole fractions and M = 1/n.
        var totalMoles = 1.0 / source.GetProperty("molarMass").GetDouble();
        var moles = new double[table.SpeciesCount];
        foreach (var species in source.GetProperty("moleFractions").EnumerateObject())
        {
            var index = table.IndexOf(species.Name);
            Assert.True(index >= 0, $"{species.Name} is not in the table");
            Assert.True(index < table.GasCount || species.Value.GetDouble() == 0.0, "a condensed species in the frozen composition is outside this test");
            moles[index] = species.Value.GetDouble() * totalMoles;
        }

        var entropy = source.GetProperty("entropy").GetDouble();
        var elementMoles = HostSolver.ElementMolesOf(c);
        var mismatches = new List<string>();
        var compared = 0;
        foreach (var station in stations.Where(s => s.GetProperty("frozen").GetBoolean()))
        {
            var label = station.GetProperty("station").GetString();
            var pressure = station.GetProperty("pressure").GetDouble();
            var solution = HostSolver.Solve(fixture.Accelerator, table, ProblemKind.AssignedEntropyPressure, pressure, 0.0, entropy, elementMoles, moles, frozen: true);
            Assert.True(solution.Status == CaseStatus.Ok, $"{label}: status {solution.Status}");
            foreach (var (field, expected, info) in StateComparison.StateFields(station, strict: false))
            {
                if (NotFrozenFields.Contains(field))
                {
                    continue;
                }

                var actual = (double)info.GetValue(solution.State)!;
                if (!fixture.Tolerances.Matches(field, expected, actual))
                {
                    mismatches.Add($"{label} {field}: reference {expected:R}, tree {actual:R}");
                }

                compared++;
            }

            // In frozen flow the reference's equilibrium heat capacity is the frozen one.
            var cpEquilibrium = station.GetProperty("cpEquilibrium").GetDouble();
            if (!fixture.Tolerances.Matches("cpEquilibrium", cpEquilibrium, solution.State.CpEquilibrium))
            {
                mismatches.Add($"{label} cpEquilibrium: reference {cpEquilibrium:R}, tree {solution.State.CpEquilibrium:R}");
            }
        }

        Assert.True(compared > 0, "no frozen station field was compared");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }

    [Theory]
    [InlineData("tp", "rp1311-example1_r1.0_p1.0atm_T3000")]
    [InlineData("hp", "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber")]
    [InlineData("sp", "nto-udmh_of2.2_pc2MPa_shiftingEquilibrium_exit2")]
    public void Frozen_mode_at_the_equilibrium_composition_recovers_the_equilibrium_temperature(string kind, string name)
    {
        var c = HostSolver.Load(kind, name);
        var equilibrium = HostSolver.Solve(fixture, c);
        Assert.Equal(CaseStatus.Ok, equilibrium.Status);
        var pressure = equilibrium.State.Pressure;
        var elementMoles = HostSolver.ElementMolesOf(c);

        var byEnthalpy = HostSolver.Solve(fixture.Accelerator, equilibrium.Table, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0,
                                          equilibrium.State.Enthalpy, elementMoles, equilibrium.Moles, frozen: true);
        var byEntropy = HostSolver.Solve(fixture.Accelerator, equilibrium.Table, ProblemKind.AssignedEntropyPressure, pressure, 0.0,
                                         equilibrium.State.Entropy, elementMoles, equilibrium.Moles, frozen: true);
        var atTemperature = HostSolver.Solve(fixture.Accelerator, equilibrium.Table, ProblemKind.AssignedTemperaturePressure, pressure,
                                             equilibrium.State.Temperature, 0.0, elementMoles, equilibrium.Moles, frozen: true);

        foreach (var frozen in new[] { byEnthalpy, byEntropy, atTemperature })
        {
            Assert.Equal(CaseStatus.Ok, frozen.Status);
            Assert.Equal(equilibrium.State.Temperature, frozen.State.Temperature, equilibrium.State.Temperature * 1e-9);
            Assert.Equal(equilibrium.State.Enthalpy, frozen.State.Enthalpy, Math.Abs(equilibrium.State.Enthalpy) * 1e-9 + 1e-3);
            Assert.Equal(equilibrium.State.Entropy, frozen.State.Entropy, equilibrium.State.Entropy * 1e-9);
            Assert.Equal(equilibrium.State.CpFrozen, frozen.State.CpFrozen, equilibrium.State.CpFrozen * 1e-9);
            Assert.Equal(equilibrium.State.CvFrozen, frozen.State.CvFrozen, equilibrium.State.CvFrozen * 1e-9);
            Assert.Equal(equilibrium.State.MolarMass, frozen.State.MolarMass, equilibrium.State.MolarMass * 1e-12);
            Assert.Equal(frozen.State.CpFrozen, frozen.State.CpEquilibrium);
            Assert.Equal(frozen.State.CvFrozen, frozen.State.CvEquilibrium);
            Assert.Equal(1.0, frozen.State.DlnVdlnT);
            Assert.Equal(-1.0, frozen.State.DlnVdlnP);
            Assert.Equal(frozen.State.CpFrozen / frozen.State.CvFrozen, frozen.State.GammaS, 1e-12);
        }
    }
}
