using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// An equilibrium family whose cases reach the trace-gas pass: one table, one batch, the labels of its cases, how many candidate cases
/// the host solver ended otherwise and were left out, and how many of the kept cases end <c>NoGasPhase</c> (the verdict's side of the
/// same decision, kept beside the states the pass settles).
/// </summary>
internal sealed record TraceGasFamily(string Name, SpeciesTable Table, EquilibriumBatch Batch, IReadOnlyList<string> Labels, int Dropped)
{
    /// <summary>The kept cases that end <c>NoGasPhase</c>.</summary>
    public int Gasless { get; init; }
}

/// <summary>
/// The 0.2.2 families of the trace-gas pass (<see cref="CudaTests"/>, <see cref="TraceGasFamiliesTests"/>), built from inputs the tree
/// computes: a mixture given by its element ratio with a trace excess or deficit of one element (the excess is added to the ratio, so a
/// state is KCl and 1e-10 mole of chlorine per mole of KCl), the magnesite mixture of the carbon dioxide walk, a grid of temperatures
/// and pressures over each, and for the grids that ask for it the enthalpy and entropy of the converged tp states as the targets of hp
/// and sp cases. A batch carries a temperature estimate and no seed moles, so every hp and sp case starts cold. Which candidates are
/// kept is decided on the host, case by case (<see cref="HostSolves.EquilibriumProbed"/>): a tp case is kept when the pass settled it
/// (<see cref="ReachesThePass"/>) or the verdict ended it <c>NoGasPhase</c>; an hp or sp case when the temperature bracket settled it at the
/// temperature of the tp state its target came from (<see cref="BracketsToThePass"/>); every other candidate is counted in
/// <see cref="TraceGasFamily.Dropped"/>.
/// </summary>
internal static class TraceGasFamilies
{
    /// <summary>The family whose hp and sp cases the launch-budget facts run (<see cref="CudaTests"/>, <see cref="TraceGasFamiliesTests"/>): the data junction of KCl, where the bracket and the pass settle every hp and sp case.</summary>
    public const string LaunchFamily = "trace-gas-kcl";

    /// <summary>The relative distance from the tp state's temperature within which an hp or sp case is the state it was built from.</summary>
    private const double OnTargetTemperature = 1.0e-9;

    private static readonly double[] KclTemperatures = [700.0, 900.0, 1000.0, 1200.0, 1500.0];
    private static readonly double[] KclPressures = [1.0e3, 1.0e5];
    private static readonly double[] KclJunctionTemperatures = [950.0, 960.0, 970.0, 980.0, 990.0, 1000.0, 1010.0, 1020.0, 1030.0, 1040.0, 1050.0];
    private static readonly double[] CalciteTemperatures = [300.0, 500.0, 700.0, 900.0];
    private static readonly double[] CalciteColumnTemperatures = [830.0, 835.0, 840.0, 845.0, 850.0, 855.0, 860.0, 865.0, 870.0];
    private static readonly double[] LithiaTemperatures = [600.0, 700.0, 800.0, 900.0, 1000.0];
    private static readonly double[] ColdSaltTemperatures = [250.0, 260.0, 270.0, 280.0, 290.0, 300.0];
    private static readonly double[] ColdSaltPressures = [1.0e3, 1.0e4, 1.0e5, 1.0e6, 1.0e7];
    private static readonly double[] MagnesiteTemperatures = [.. Enumerable.Range(0, 41).Select(k => 700.0 + 5.0 * k)];
    private static readonly ProblemKind[] AllKinds = [ProblemKind.AssignedTemperaturePressure, ProblemKind.AssignedEnthalpyPressure, ProblemKind.AssignedEntropyPressure];
    private static readonly ProblemKind[] TpOnly = [ProblemKind.AssignedTemperaturePressure];

    private static readonly Dictionary<string, TraceSystem> Systems = new()
    {
        ["trace-gas-magnesite-walk"] = new(["MG", "C", "O"], [1, 2, 5], 2,
            [new([0.0], [1.0e7], MagnesiteTemperatures, TpOnly)]),
        ["trace-gas-kcl"] = new(["K", "CL"], [1, 1], 1,
            [new([1.0e-10, -1.0e-10], KclPressures, KclTemperatures, TpOnly),
             new([1.0e-6], [1.0e5], KclJunctionTemperatures, AllKinds)]),
        ["trace-gas-calcite"] = new(["CA", "C", "O"], [1, 1, 3], 2,
            [new([1.0e-8], [1.0e5, 1.0e7], CalciteTemperatures, TpOnly),
             new([-1.0e-7], [1.0e3], CalciteColumnTemperatures, TpOnly)]),
        ["trace-gas-lithia"] = new(["LI", "O"], [2, 1], 1,
            [new([1.0e-10], [1.0e3, 1.0e5], LithiaTemperatures, AllKinds)]),
        ["trace-gas-halite-cold"] = new(["NA", "CL"], [1, 1], 1,
            [new([1.0e-7, -1.0e-7], ColdSaltPressures, ColdSaltTemperatures, AllKinds)]),
    };

    /// <summary>The names of the families as theory data.</summary>
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var name in Systems.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>The kinds of problem a family's grids ask for, before the host solver decides which cases are kept.</summary>
    public static IReadOnlyCollection<ProblemKind> KindsAsked(string name) => [.. Systems[name].Grids.SelectMany(grid => grid.Kinds).Distinct()];

    /// <summary>One family by its name.</summary>
    public static TraceGasFamily Family(SpeciesDatabase database, string name)
    {
        ArgumentNullException.ThrowIfNull(database);
        var system = Systems[name];
        var table = GasPlateauFamilies.TableOver(database, system.Elements);
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var kept = new List<Candidate>();
        var dropped = 0;
        foreach (var grid in system.Grids)
        {
            var (settled, lost) = Settled(database, tables, name, system, grid);
            kept.AddRange(settled);
            dropped += lost;
        }

        return new TraceGasFamily(name, table, BatchOf(table, kept), [.. kept.Select(c => c.Label)], dropped) { Gasless = kept.Count(c => c.Gasless) };
    }

    /// <summary>The hp and sp cases of a batch, in order: the cases whose solve runs the temperature bracket beside the pass.</summary>
    public static EquilibriumBatch WithoutTp(EquilibriumBatch batch) => RecoveryFamilies.WithoutTp(batch);

    /// <summary>The batch repeated end to end until it holds <paramref name="count"/> cases: the family's own states, so that a launch of many is a launch of them.</summary>
    public static EquilibriumBatch Tiled(EquilibriumBatch batch, int count) => RecoveryFamilies.Tiled(batch, count);

    /// <summary>
    /// Whether a tp case that ended <c>Ok</c> was settled by the pass: the ordinary attempt failed and the gas-phase verdict ran (its
    /// anchor is in the scratch), and the case took more Newton steps than one reduced attempt does
    /// (<see cref="EquilibriumSolver.MaxNewtonSteps"/>). The pass is the attempt Recovery schedules after that verdict (seam (a)).
    /// </summary>
    public static bool ReachesThePass(HostEquilibriumCase solved) =>
        solved.Status == CaseStatus.Ok && solved.Anchored && solved.Iterations > EquilibriumSolver.MaxNewtonSteps;

    /// <summary>
    /// Whether an hp or sp case that ended <c>Ok</c> or <c>NoGasPhase</c> was settled by the temperature bracket: the ordinary iteration
    /// failed and the bracket wrote its ends, and the case took more Newton steps than one reduced attempt does. The bracket settles a
    /// trace-gas state through the pass (seams (b) and (b') of Recovery) or at a data junction of a trace-gas convergence.
    /// </summary>
    public static bool BracketsToThePass(HostEquilibriumCase solved) =>
        solved.Status is CaseStatus.Ok or CaseStatus.NoGasPhase && solved.Bracketed && solved.Iterations > EquilibriumSolver.MaxNewtonSteps;

    /// <summary>
    /// What is wrong with one launch of trace-gas cases, or null: a case that ended neither <c>Ok</c> nor <c>NoGasPhase</c> (the launch was
    /// not the family it stands for), or a kernel time above <paramref name="limit"/>.
    /// </summary>
    public static string? LaunchViolation(EquilibriumBatchResult run, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(run);
        var other = run.Status.Count(status => status is not (CaseStatus.Ok or CaseStatus.NoGasPhase));
        return other > 0 ? $"{other} of {run.Count} cases of the launch ended neither Ok nor NoGasPhase"
            : run.Timings.Kernel > limit ? $"{run.Count} trace-gas cases took {run.Timings.Kernel.TotalMilliseconds:F1} ms in one launch, above the budget of {limit.TotalMilliseconds:F0} ms"
            : null;
    }

    private static (List<Candidate> Kept, int Dropped) Settled(SpeciesDatabase database, UploadedTables tables, string name, TraceSystem system, Grid grid)
    {
        var tp = new List<Candidate>();
        foreach (var excess in grid.Excess)
        {
            var moles = MolesWith(database, system, excess);
            tp.AddRange(from pressure in grid.Pressure
                        from temperature in grid.Temperature
                        select new Candidate(ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles, $"{name} tp excess {excess:0e0} {pressure:0e0} Pa {temperature} K"));
        }

        var kept = new List<Candidate>();
        var dropped = 0;
        foreach (var candidate in tp)
        {
            var solved = Probe(tables, candidate);
            if (solved.Status == CaseStatus.NoGasPhase || ReachesThePass(solved))
            {
                kept.Add(candidate with { Gasless = solved.Status == CaseStatus.NoGasPhase });
            }
            else
            {
                dropped++;
            }

            if (solved.Status == CaseStatus.Ok)
            {
                var (energy, lost) = EnergyCases(tables, candidate, solved, grid.Kinds);
                kept.AddRange(energy);
                dropped += lost;
            }
        }

        return (kept, dropped);
    }

    /// <summary>The hp and sp cases of a converged tp state, those among the grid's kinds that the bracket settles at the state their target came from.</summary>
    private static (List<Candidate> Kept, int Dropped) EnergyCases(UploadedTables tables, Candidate tp, HostEquilibriumCase solved, ProblemKind[] kinds)
    {
        var kept = new List<Candidate>();
        var dropped = 0;
        foreach (var kind in kinds.Where(k => k != ProblemKind.AssignedTemperaturePressure))
        {
            var enthalpy = kind == ProblemKind.AssignedEnthalpyPressure;
            var candidate = tp with
            {
                Kind = kind,
                Temperature = 0.0,
                Target = enthalpy ? solved.State.Enthalpy : solved.State.Entropy,
                Intended = tp.Temperature,
                Label = tp.Label.Replace(" tp ", enthalpy ? " hp of tp " : " sp of tp ", StringComparison.Ordinal),
            };
            var energy = Probe(tables, candidate);
            var onTarget = energy.Status != CaseStatus.Ok || Math.Abs(energy.State.Temperature - tp.Temperature) <= OnTargetTemperature * tp.Temperature;
            if (BracketsToThePass(energy) && onTarget)
            {
                kept.Add(candidate with { Gasless = energy.Status == CaseStatus.NoGasPhase });
            }
            else
            {
                dropped++;
            }
        }

        return (kept, dropped);
    }

    private static HostEquilibriumCase Probe(UploadedTables tables, Candidate candidate) =>
        HostSolves.EquilibriumProbed(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, BatchOf(tables.SpeciesBuffers.Table, [candidate]), 0);

    private static double[] MolesWith(SpeciesDatabase database, TraceSystem system, double excess)
    {
        var ratio = (double[])system.Ratio.Clone();
        ratio[system.ExcessElement] += excess;
        return GasPlateauFamilies.ElementMolesOf(database, system.Elements, ratio);
    }

    private static EquilibriumBatch BatchOf(SpeciesTable table, IReadOnlyList<Candidate> cases)
    {
        var batch = new EquilibriumBatch(cases.Count, table.ElementCount);
        for (var k = 0; k < cases.Count; k++)
        {
            batch.Kind[k] = cases[k].Kind;
            batch.Pressure[k] = cases[k].Pressure;
            batch.Temperature[k] = cases[k].Temperature;
            batch.Target[k] = cases[k].Target;
            Array.Copy(cases[k].ElementMoles, 0, batch.ElementMoles, k * table.ElementCount, table.ElementCount);
        }

        return batch;
    }

    /// <summary>One case before the host solver has said what it ends: the inputs of an <see cref="EquilibriumBatch"/> row and the label.</summary>
    private sealed record Candidate(ProblemKind Kind, double Pressure, double Temperature, double Target, double[] ElementMoles, string Label)
    {
        /// <summary>K: the temperature of the tp state the target was taken from, 0 where the case was not built from one.</summary>
        public double Intended { get; init; }

        /// <summary>Whether the case ended <c>NoGasPhase</c>.</summary>
        public bool Gasless { get; init; }
    }

    /// <summary>A mixture by the ratio of its elements' moles, the element that carries the trace excess and the grids of states over it.</summary>
    private sealed record TraceSystem(string[] Elements, double[] Ratio, int ExcessElement, Grid[] Grids);

    /// <summary>A grid of states over a mixture: the excesses added to its ratio, the pressures, the temperatures and the kinds of problem asked at each.</summary>
    private sealed record Grid(double[] Excess, double[] Pressure, double[] Temperature, ProblemKind[] Kinds);
}
