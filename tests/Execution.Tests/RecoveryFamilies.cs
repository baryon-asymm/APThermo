using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// An equilibrium family whose cases end <c>Ok</c> or <c>NoGasPhase</c> through the 0.2.2 gasless verdict and temperature bracket: one
/// table, one batch, the labels of its cases and how many candidate cases the CPU accelerator ended otherwise and were left out.
/// </summary>
internal sealed record BracketedFamily(string Name, SpeciesTable Table, EquilibriumBatch Batch, IReadOnlyList<string> Labels, int Dropped)
{
    /// <summary>The cases of the family that end <c>NoGasPhase</c>, counted by the CPU accelerator run that selected them.</summary>
    public int Gasless { get; init; }
}

/// <summary>
/// The 0.2.2 families of the gasless verdict and the Recovery bracket (<see cref="CudaTests"/>, <see cref="BatchTests"/>), built from
/// inputs the tree computes: the stable solid and liquid phases of KO2 and NaO2 at their exact 1:2 stoichiometry under 1e7 Pa, where
/// the gas vanishes, with the enthalpy and entropy of their tp states taken from the species functions over the condensed moles the
/// solver found, and a gasless melting plateau whose temperature is bisected on the condensed set of host tp solves; gas plateaus
/// of calcium and magnesium carbonate at 1e5 Pa (<see cref="GasPlateauFamilies"/>); and the AP/HTPB/Al table below the water band.
/// A batch carries a temperature estimate and no seed moles, so every hp and sp case starts cold; the cases kept are those the CPU
/// accelerator ends in the status the family stands for, and the rest are counted in <see cref="BracketedFamily.Dropped"/>. The
/// AP/HTPB/Al family keeps only the cases that end at the temperature of the tp state their target came from: below the water band
/// the condensed records out of their fit range are ineligible, so the entropy of the 200 K state is above that of the 300 K one and
/// its sp case ends, <c>Ok</c>, on the dehydration plateau at 415.9 K, an ill-conditioned state of another problem.
/// </summary>
internal static class RecoveryFamilies
{
    /// <summary>Pa: the pressure of the gasless families, high enough that no gas is stable over the condensed phases of the peroxides.</summary>
    private const double GaslessPressure = 1.0e7;

    /// <summary>K: how far either side of the melting temperature the tp states of the plateau sit.</summary>
    private const double MeltingOffset = 0.5;

    /// <summary>Pa: the pressure of the AP/HTPB/Al states below the water band.</summary>
    private const double ColdPropellantPressure = 2.0e7;

    /// <summary>The name of the AP/HTPB/Al family.</summary>
    private const string ColdPropellant = "bracket-ap-htpb-al-20mpa";

    /// <summary>The relative distance from the tp state's temperature within which an hp or sp case of the AP/HTPB/Al family is the state it was built from.</summary>
    private const double OnTargetTemperature = 1.0e-9;

    private const double LowestBisection = 300.0;
    private const double HighestBisection = 1500.0;
    private const int BisectionSteps = 60;

    private static readonly double[] GaslessTemperatures = [300.0, 400.0, 500.0, 600.0, 700.0, 800.0, 900.0, 1000.0, 1100.0, 1200.0, 1300.0, 1400.0, 1500.0];
    private static readonly double[] MeltingFractions = [0.25, 0.5, 0.75];
    private static readonly double[] ColdPropellantTemperatures = [200.0, 215.0, 230.0, 245.0, 260.0, 275.0];

    private static readonly Dictionary<string, GaslessSystem> Gasless = new()
    {
        ["gasless-ko2"] = new(["K", "O"], [1, 2], ("KO2(a)", "KO2(L)")),
        ["gasless-nao2"] = new(["NA", "O"], [1, 2], null),
    };

    /// <summary>The names of the families of the 0.2.2 gasless verdict and bracket as theory data.</summary>
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var name in Gasless.Keys.Concat(GasPlateauFamilies.BracketedNames).Append(ColdPropellant))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>One family by its name.</summary>
    public static BracketedFamily Family(SpeciesDatabase database, string name)
    {
        ArgumentNullException.ThrowIfNull(database);
        return Gasless.TryGetValue(name, out var system) ? GaslessFamily(database, name, system)
            : name == ColdPropellant ? ColdPropellantFamily(database)
            : PlateauFamily(database, name);
    }

    /// <summary>The batch repeated end to end until it holds <paramref name="count"/> cases: the same cases, so that a launch of many brackets is a launch of the family's own states.</summary>
    public static EquilibriumBatch Tiled(EquilibriumBatch batch, int count)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return Rows(batch, [.. Enumerable.Range(0, count).Select(k => k % batch.Count)]);
    }

    /// <summary>The hp and sp cases of a batch, in order: the cases that run the bracket when the attempts fail.</summary>
    public static EquilibriumBatch WithoutTp(EquilibriumBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return Rows(batch, [.. Enumerable.Range(0, batch.Count).Where(k => batch.Kind[k] != ProblemKind.AssignedTemperaturePressure)]);
    }

    /// <summary>
    /// What is wrong with one launch of cases that all bracket, or null: a case that did not end <c>NoGasPhase</c> (the launch was not
    /// the bracketing family it stands for), or a kernel time above <paramref name="limit"/>.
    /// </summary>
    public static string? LaunchViolation(EquilibriumBatchResult run, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(run);
        var other = run.Status.Count(status => status != CaseStatus.NoGasPhase);
        return other > 0 ? $"{other} of {run.Count} cases of the launch did not end NoGasPhase"
            : run.Timings.Kernel > limit ? $"{run.Count} cases that all bracket took {run.Timings.Kernel.TotalMilliseconds:F1} ms in one launch, above the budget of {limit.TotalMilliseconds:F0} ms"
            : null;
    }

    /// <summary>A batch of the given rows of another, in the given order.</summary>
    private static EquilibriumBatch Rows(EquilibriumBatch batch, int[] sources)
    {
        var rows = new EquilibriumBatch(sources.Length, batch.ElementCount);
        for (var k = 0; k < sources.Length; k++)
        {
            rows.Kind[k] = batch.Kind[sources[k]];
            rows.Pressure[k] = batch.Pressure[sources[k]];
            rows.Temperature[k] = batch.Temperature[sources[k]];
            rows.Target[k] = batch.Target[sources[k]];
            Array.Copy(batch.ElementMoles, sources[k] * batch.ElementCount, rows.ElementMoles, k * batch.ElementCount, batch.ElementCount);
        }

        return rows;
    }

    private static BracketedFamily PlateauFamily(SpeciesDatabase database, string name)
    {
        var plateau = GasPlateauFamilies.Family(database, name);
        var candidates = Enumerable.Range(0, plateau.Batch.Count)
            .Select(k => new Candidate(plateau.Batch.Kind[k], plateau.Batch.Pressure[k], 0.0, plateau.Batch.Target[k],
                                       [.. plateau.Batch.ElementMoles.Skip(k * plateau.Table.ElementCount).Take(plateau.Table.ElementCount)], plateau.Labels[k]));
        return Kept(name, plateau.Table, [.. candidates], (_, status, _) => status == CaseStatus.Ok);
    }

    private static BracketedFamily ColdPropellantFamily(SpeciesDatabase database)
    {
        var rocket = FixtureBatches.RocketFamilies(database, FixtureBatches.ThroatKind).First(f => f.Name.StartsWith("ap-htpb-al-throat", StringComparison.Ordinal));
        var moles = rocket.Inputs[0].Mixture.ElementMoles;
        var table = rocket.Table;
        var states = ColdPropellantTemperatures
            .Select(t => new Candidate(ProblemKind.AssignedTemperaturePressure, ColdPropellantPressure, t, 0.0, moles, $"tp {t} K"))
            .ToArray();
        var batch = BatchOf(table, states);
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        var candidates = new List<Candidate>();
        for (var k = 0; k < states.Length; k++)
        {
            if (result.Status[k] == CaseStatus.Ok)
            {
                candidates.Add(states[k] with { Kind = ProblemKind.AssignedEnthalpyPressure, Temperature = 0.0, Target = result.State[k].Enthalpy, Intended = states[k].Temperature, Label = $"hp of {states[k].Label}" });
                candidates.Add(states[k] with { Kind = ProblemKind.AssignedEntropyPressure, Temperature = 0.0, Target = result.State[k].Entropy, Intended = states[k].Temperature, Label = $"sp of {states[k].Label}" });
            }
        }

        var kept = Kept(ColdPropellant, table, candidates, (candidate, status, state) => status == CaseStatus.Ok && Math.Abs(state.Temperature - candidate.Intended) <= OnTargetTemperature * candidate.Intended);
        return kept with { Dropped = kept.Dropped + 2 * (states.Length - candidates.Count / 2) };
    }

    private static BracketedFamily GaslessFamily(SpeciesDatabase database, string name, GaslessSystem system)
    {
        var table = GasPlateauFamilies.TableOver(database, system.Elements);
        var moles = GasPlateauFamilies.ElementMolesOf(database, system.Elements, system.Ratio);
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var tp = GaslessTemperatures
            .Select(t => new Candidate(ProblemKind.AssignedTemperaturePressure, GaslessPressure, t, 0.0, moles, $"{name} tp {t} K"))
            .ToList();
        var melting = system.Melting is { } phases ? MeltingTemperature(tables, moles, phases.Solid) : (double?)null;
        if (melting is { } meltingTemperature)
        {
            tp.Add(new Candidate(ProblemKind.AssignedTemperaturePressure, GaslessPressure, meltingTemperature - MeltingOffset, 0.0, moles, $"{name} tp melting - {MeltingOffset} K"));
            tp.Add(new Candidate(ProblemKind.AssignedTemperaturePressure, GaslessPressure, meltingTemperature + MeltingOffset, 0.0, moles, $"{name} tp melting + {MeltingOffset} K"));
        }

        var tpRun = Kept(name, table, tp, (_, status, _) => status == CaseStatus.NoGasPhase);
        var candidates = new List<Candidate>(tpRun.Labels.Count * 3);
        var keptTp = tp.Where(c => tpRun.Labels.Contains(c.Label)).ToList();
        candidates.AddRange(keptTp);
        candidates.AddRange(keptTp.SelectMany(c => HpAndSpOf(tables, table, c)));
        if (melting is { } plateau && system.Melting is { } melt)
        {
            candidates.AddRange(MeltingPlateau(tables, name, moles, plateau, melt));
        }

        var all = Kept(name, table, candidates, (_, status, _) => status == CaseStatus.NoGasPhase);
        return all with { Dropped = tpRun.Dropped + all.Dropped, Gasless = all.Batch.Count };
    }

    /// <summary>The hp and sp cases of a gasless tp state: the enthalpy and the entropy of its condensed moles at its temperature, from the species functions.</summary>
    private static IEnumerable<Candidate> HpAndSpOf(UploadedTables tables, SpeciesTable table, Candidate state)
    {
        var batch = BatchOf(table, [state]);
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        var (enthalpy, entropy) = (0.0, 0.0);
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            var amount = result.Moles[j];
            if (amount > 0.0)
            {
                var (h, s) = SpeciesFigures(tables, table.Species[j], state.Temperature);
                enthalpy += amount * h;
                entropy += amount * s;
            }
        }

        yield return state with { Kind = ProblemKind.AssignedEnthalpyPressure, Temperature = 0.0, Target = enthalpy, Label = state.Label.Replace(" tp ", " hp of tp ", StringComparison.Ordinal) };
        yield return state with { Kind = ProblemKind.AssignedEntropyPressure, Temperature = 0.0, Target = entropy, Label = state.Label.Replace(" tp ", " sp of tp ", StringComparison.Ordinal) };
    }

    /// <summary>hp and sp cases inside the gasless melting plateau: a fraction of the way from the solid to the liquid at the melting temperature.</summary>
    private static IEnumerable<Candidate> MeltingPlateau(UploadedTables tables, string name, double[] moles, double temperature, (string Solid, string Liquid) phases)
    {
        var amount = moles[0];
        var (hSolid, sSolid) = SpeciesFigures(tables, phases.Solid, temperature);
        var (hLiquid, sLiquid) = SpeciesFigures(tables, phases.Liquid, temperature);
        foreach (var fraction in MeltingFractions)
        {
            yield return new Candidate(ProblemKind.AssignedEnthalpyPressure, GaslessPressure, 0.0, amount * (hSolid + fraction * (hLiquid - hSolid)), moles, $"{name} hp melting plateau fraction {fraction}");
            yield return new Candidate(ProblemKind.AssignedEntropyPressure, GaslessPressure, 0.0, amount * (sSolid + fraction * (sLiquid - sSolid)), moles, $"{name} sp melting plateau fraction {fraction}");
        }
    }

    /// <summary>The enthalpy and entropy per kmol of one condensed species at a temperature, J/kmol and J/(kmol·K), from the species functions.</summary>
    private static (double Enthalpy, double Entropy) SpeciesFigures(UploadedTables tables, string species, double temperature)
    {
        var table = tables.SpeciesBuffers.Table;
        var view = tables.SpeciesBuffers.View;
        var piece = table.PieceOf(species, temperature);
        return (SpeciesFunctions.HOverRT(view, piece, temperature) * PhysicalConstants.R * temperature, SpeciesFunctions.SOverR(view, piece, temperature) * PhysicalConstants.R);
    }

    /// <summary>Bisection on the condensed set of host tp solves: the solid is present below the melting temperature and absent above it.</summary>
    private static double MeltingTemperature(UploadedTables tables, double[] moles, string solid)
    {
        var table = tables.SpeciesBuffers.Table;
        var low = LowestBisection;
        var high = HighestBisection;
        for (var step = 0; step < BisectionSteps; step++)
        {
            var middle = 0.5 * (low + high);
            var batch = new EquilibriumBatch(1, moles.Length);
            batch.Kind[0] = ProblemKind.AssignedTemperaturePressure;
            batch.Pressure[0] = GaslessPressure;
            batch.Temperature[0] = middle;
            Array.Copy(moles, batch.ElementMoles, moles.Length);
            var state = HostSolves.Equilibrium(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, batch, 0);
            var hasSolid = Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount).Any(j => state.Moles[j] > 0.0 && table.Species[j] == solid);
            if (state.Status == CaseStatus.NoGasPhase && !hasSolid)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return 0.5 * (low + high);
    }

    /// <summary>The candidates as one batch, run once on the CPU accelerator, with the cases whose status the family accepts kept and the others counted.</summary>
    private static BracketedFamily Kept(string name, SpeciesTable table, IReadOnlyList<Candidate> candidates, Func<Candidate, CaseStatus, MixtureState, bool> accepted)
    {
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var result = EngineFixture.Shared.Cpu.Run(tables, BatchOf(table, candidates));
        var kept = Enumerable.Range(0, candidates.Count).Where(k => accepted(candidates[k], result.Status[k], result.State[k])).Select(k => candidates[k]).ToList();
        return new BracketedFamily(name, table, BatchOf(table, kept), [.. kept.Select(c => c.Label)], candidates.Count - kept.Count);
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

    /// <summary>One case before the CPU accelerator has said what it ends: the inputs of an <see cref="EquilibriumBatch"/> row and the label.</summary>
    private sealed record Candidate(ProblemKind Kind, double Pressure, double Temperature, double Target, double[] ElementMoles, string Label)
    {
        /// <summary>K: the temperature of the tp state the target was taken from, 0 where the case was not built from one.</summary>
        public double Intended { get; init; }
    }

    /// <summary>A peroxide at its exact 1:2 stoichiometry: the elements, their ratio of moles and, where it has one in range, the solid and liquid phases of its melting plateau.</summary>
    private sealed record GaslessSystem(string[] Elements, double[] Ratio, (string Solid, string Liquid)? Melting);
}
