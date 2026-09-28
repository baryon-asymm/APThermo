using APThermo.Fixtures;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Transport.Tests;

/// <summary>What one generated state of the sweeps below produced: the table it was solved on, the independent
/// equilibrium state, and the transport figures on that same composition (empty on a non-<c>Ok</c> status).</summary>
internal sealed record SolvedState(SpeciesTable Table, EquilibriumEvaluation Equilibrium, CaseStatus TransportStatus,
                                   TransportFigures Figures, IReadOnlyList<string> Violations);

/// <summary>
/// Cross-checks the transport set's reacting figures against an independently solved state from `Equilibrium` itself,
/// through the `InternalsVisibleTo` grant of 2026-09-28 (the orchestrator's decision under `AGENTS.md` §11,
/// `tests/Transport.Tests/BOOT.md`, the components-settled criterion). Two facts: order-invariance of the H2 + HF grid
/// the second hidden-defect audit found broken (finding F2), and a broader consistency sweep whose tolerance is
/// derived from the transport set's own coverage constants and never tuned to pass.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class EquilibriumConsistencyTests
{
    /// <summary>
    /// The relative slack a self-consistency comparison of this node allows. The transport set's own coverage rule
    /// (<see cref="TransportSolver.CoverageFraction"/>, <see cref="TransportSolver.CoverageTolerance"/>) may still
    /// leave out a fraction of the gaseous moles of about <c>1 - CoverageFraction + CoverageTolerance</c> at the
    /// moment it stops adding species — but a reacting figure is not linear in the moles left out: the Butler–Brokaw
    /// reaction term can weigh a minor species far out of proportion to its own mole fraction, which is exactly the
    /// mechanism this criterion exists to guard (<c>BOOT.md</c>, the ⚠ of 2026-09-28). The selection's own decade rule
    /// bounds how far short of full coverage a pass can stop: two more decades of the same slack is the margin this
    /// node allows a reacting figure to amplify that exclusion by, not a number tuned to this fact's own stations.
    /// </summary>
    private const double ConsistencyTolerance = 100.0 * (1.0 - TransportSolver.CoverageFraction + TransportSolver.CoverageTolerance);

    /// <summary>The audit's own grid: 300 to 3 000 K in steps of 270 K (11 points).</summary>
    private static readonly double[] GridTemperatures = [.. Enumerable.Range(0, 11).Select(i => 300.0 + i * 270.0)];

    /// <summary>The audit's own grid: 1 kPa, 0.1 MPa, 10 MPa.</summary>
    private static readonly double[] GridPressures = [1e3, 1e5, 1e7];

    /// <summary>The four verification propellants' fixture-file name prefixes (root `BOOT.md`, the acceptance criteria).</summary>
    private static readonly string[] VerificationPropellantPrefixes = ["lox-lh2", "lox-rp1", "nto-udmh", "ap-htpb-al"];

    /// <summary>
    /// H2 + HF (H:F = 2:1 by atoms) over the audit's grid, in both element orders: every reaction conserves, and the
    /// two orders agree field by field within <see cref="ConsistencyTolerance"/>. Before the fix, with H listed first,
    /// 20 of 29 converging records had the transport's equilibrium heat capacity off by more than 1 % (the second
    /// hidden-defect audit, finding F2); with F listed first every one agreed.
    /// </summary>
    [Fact]
    public void HydrogenFluorideAgreesBetweenElementOrdersOverTheAuditsGrid()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var violations = new List<string>();
        var compared = 0;
        foreach (var temperature in GridTemperatures)
        {
            foreach (var pressure in GridPressures)
            {
                var label = $"T={temperature:R} p={pressure:R}";
                var first = SolveHydrogenFluoride(accelerator, ["H", "F"], temperature, pressure);
                var second = SolveHydrogenFluoride(accelerator, ["F", "H"], temperature, pressure);
                violations.AddRange(first.Violations.Select(v => $"{label} H,F {v}"));
                violations.AddRange(second.Violations.Select(v => $"{label} F,H {v}"));
                if (first.TransportStatus != CaseStatus.Ok || second.TransportStatus != CaseStatus.Ok)
                {
                    continue;
                }

                compared++;
                violations.AddRange(CompareFigures(first.Figures, second.Figures, label));
            }
        }

        Assert.True(compared > 0, "no state of the grid converged in both element orders");
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>
    /// Over state sweeps of H/F, of N/O from N2O4 and of the four verification propellants' element sets, wherever no
    /// species is trace-eliminated and no condensed species is present, the transport set's `EquilibriumHeatCapacity`
    /// equals `Equilibrium`'s own `CpEquilibrium` of the same state within <see cref="ConsistencyTolerance"/>. The
    /// list of states is generated, and this fact fails on an empty list at either stage.
    /// </summary>
    [Fact]
    public void TransportsEquilibriumHeatCapacityMatchesEquilibriumOverStateSweeps()
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var states = HydrogenFluorideStates(accelerator)
            .Concat(NitrogenOxygenStates(accelerator))
            .Concat(VerificationPropellantChamberStates(accelerator))
            .Where(s => s.TransportStatus == CaseStatus.Ok)
            .ToList();
        Assert.NotEmpty(states);

        var violations = states.SelectMany(s => s.Violations).ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));

        var comparable = states.Where(s => s.Figures.TraceEliminations == 0 && !HasCondensedInSolution(s)).ToList();
        Assert.NotEmpty(comparable);

        var mismatches = new List<string>();
        foreach (var state in comparable)
        {
            var reference = state.Equilibrium.State.CpEquilibrium;
            var relativeDifference = Math.Abs(state.Figures.EquilibriumHeatCapacity - reference) / Math.Abs(reference);
            if (!(relativeDifference <= ConsistencyTolerance))
            {
                mismatches.Add($"T={state.Equilibrium.State.Temperature:R}: transport {state.Figures.EquilibriumHeatCapacity:R} " +
                               $"vs equilibrium {reference:R} ({relativeDifference:E} relative)");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    /// <summary>Solves one H2 + HF state (the audit's restricted list) in the given element order.</summary>
    private static SolvedState SolveHydrogenFluoride(Accelerator accelerator, string[] elements, double temperature, double pressure)
    {
        string[] products = ["H2", "HF", "H", "F", "F2", "H2F2"];
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, products);
        var transportTable = TransportTable.Build(CpuFixture.Shared.Transport, table);

        // H:F = 2:1 by atoms, the element masses normalised to sum to 1 kg of mixture; the atomic masses come from the
        // table's own data, the atom counts (2, 1) from the ratio itself.
        var massH = table.Arrays.MolarMass[table.IndexOf("H")];
        var massF = table.Arrays.MolarMass[table.IndexOf("F")];
        var scale = 1.0 / (2.0 * massH + massF);
        var elementMoles = new double[table.ElementCount];
        var hIndex = elements[0] == "H" ? 0 : 1;
        elementMoles[hIndex] = 2.0 * scale;
        elementMoles[1 - hIndex] = scale;

        var equilibrium = EquilibriumHost.SolveTp(accelerator, table, elementMoles, temperature, pressure);
        return Solved(table, transportTable, equilibrium, temperature);
    }

    /// <summary>The H2 + HF grid, both element orders, as a flat sequence of solved states.</summary>
    private static IEnumerable<SolvedState> HydrogenFluorideStates(Accelerator accelerator)
    {
        foreach (var temperature in GridTemperatures)
        {
            foreach (var pressure in GridPressures)
            {
                yield return SolveHydrogenFluoride(accelerator, ["H", "F"], temperature, pressure);
                yield return SolveHydrogenFluoride(accelerator, ["F", "H"], temperature, pressure);
            }
        }
    }

    /// <summary>The N2O4/NO2 grid (the full seven-species list, this time), one element order, over the audit's grid.</summary>
    private static IEnumerable<SolvedState> NitrogenOxygenStates(Accelerator accelerator)
    {
        string[] elements = ["N", "O"];
        string[] products = ["NO2", "N2O4", "N", "O", "N2", "O2", "NO"];
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, elements, products);
        var transportTable = TransportTable.Build(CpuFixture.Shared.Transport, table);
        var n2O4MolarMass = table.Arrays.MolarMass[table.IndexOf("N2O4")];
        foreach (var temperature in GridTemperatures)
        {
            foreach (var pressure in GridPressures)
            {
                var elementMoles = new double[table.ElementCount];
                elementMoles[0] = 2.0 / n2O4MolarMass;
                elementMoles[1] = 4.0 / n2O4MolarMass;
                var equilibrium = EquilibriumHost.SolveTp(accelerator, table, elementMoles, temperature, pressure);
                yield return Solved(table, transportTable, equilibrium, temperature);
            }
        }
    }

    /// <summary>The chamber state of one representative fixture per verification propellant, both element orders.</summary>
    private static IEnumerable<SolvedState> VerificationPropellantChamberStates(Accelerator accelerator)
    {
        foreach (var prefix in VerificationPropellantPrefixes)
        {
            var path = FixtureFiles.Enumerate("rocket").First(p => Path.GetFileName(p).StartsWith(prefix + "_", StringComparison.Ordinal));
            var c = CeaFixtures.Load(path);
            var elements = TransportHost.ElementsOf(c);
            var products = TransportHost.ProductsOf(c);
            var elementMolesByName = c.Inputs.GetProperty("elementMoles");
            var pressure = c.Inputs.GetProperty("chamberPressure").GetDouble();
            var enthalpy = c.Inputs.GetProperty("reactantEnthalpy").GetDouble();
            string[][] orders = [elements, [.. elements.Reverse()]];
            foreach (var order in orders)
            {
                var table = SpeciesTable.Build(CpuFixture.Shared.Database, order, products);
                var transportTable = TransportTable.Build(CpuFixture.Shared.Transport, table);
                var elementMoles = new double[table.ElementCount];
                for (var i = 0; i < order.Length; i++)
                {
                    elementMoles[i] = elementMolesByName.GetProperty(order[i]).GetDouble();
                }

                var equilibrium = EquilibriumHost.SolveHp(accelerator, table, elementMoles, enthalpy, pressure);
                yield return Solved(table, transportTable, equilibrium, equilibrium.State.Temperature);
            }
        }
    }

    /// <summary>Runs Transport on an equilibrium evaluation's own composition, or reports its failing status untried.</summary>
    private static SolvedState Solved(SpeciesTable table, TransportTable transportTable, EquilibriumEvaluation equilibrium, double temperature)
    {
        if (equilibrium.Status != CaseStatus.Ok)
        {
            return new SolvedState(table, equilibrium, equilibrium.Status, default, []);
        }

        var (status, figures, violations) = ReactionConservationTests.EvaluateAndCheckConservation(table, transportTable, equilibrium.Moles, temperature);
        return new SolvedState(table, equilibrium, status, figures, violations);
    }

    /// <summary>Whether the solved state has a condensed species present with positive moles.</summary>
    private static bool HasCondensedInSolution(SolvedState state)
    {
        for (var i = state.Table.GasCount; i < state.Table.SpeciesCount; i++)
        {
            if (state.Equilibrium.Moles[i] > 0.0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The reacting figures two element orders must agree on, by name, for the mismatch messages.</summary>
    private static (string Name, double Value)[] NamedFigures(TransportFigures figures) =>
    [
        ("Viscosity", figures.Viscosity),
        ("FrozenConductivity", figures.FrozenConductivity),
        ("ReactingConductivity", figures.ReactingConductivity),
        ("FrozenPrandtl", figures.FrozenPrandtl),
        ("ReactingPrandtl", figures.ReactingPrandtl),
        ("FrozenHeatCapacity", figures.FrozenHeatCapacity),
        ("EquilibriumHeatCapacity", figures.EquilibriumHeatCapacity),
    ];

    /// <summary>The reacting figures of two element orders, field by field, within <see cref="ConsistencyTolerance"/>.</summary>
    private static List<string> CompareFigures(TransportFigures first, TransportFigures second, string label)
    {
        var a = NamedFigures(first);
        var b = NamedFigures(second);
        var violations = new List<string>();
        for (var i = 0; i < a.Length; i++)
        {
            var reference = Math.Max(Math.Abs(a[i].Value), Math.Abs(b[i].Value));
            var difference = Math.Abs(a[i].Value - b[i].Value);
            if (reference > 0.0 && difference / reference > ConsistencyTolerance)
            {
                violations.Add($"{label} {a[i].Name}: {a[i].Value:R} vs {b[i].Value:R} ({difference / reference:E} relative)");
            }
        }

        return violations;
    }
}
