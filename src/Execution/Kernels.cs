using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;
using ILGPU;

namespace AerospacePropellantThermodynamics.Execution;

/// <summary>Device views of one equilibrium chunk. Public only because ILGPU requires kernel parameter types to be.</summary>
public readonly struct EquilibriumBatchViews(
    ArrayView<int> kinds, ArrayView<double> pressures, ArrayView<double> temperatures, ArrayView<double> targets,
    ArrayView<double> elementMoles, ArrayView<double> scratchDoubles, ArrayView<int> scratchInts,
    ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<MixtureState> states, ArrayView<int> status, ArrayView<int> iterations)
{
    public readonly ArrayView<int> Kinds = kinds;
    public readonly ArrayView<double> Pressures = pressures;
    public readonly ArrayView<double> Temperatures = temperatures;
    public readonly ArrayView<double> Targets = targets;
    public readonly ArrayView<double> ElementMoles = elementMoles;
    public readonly ArrayView<double> ScratchDoubles = scratchDoubles;
    public readonly ArrayView<int> ScratchInts = scratchInts;
    public readonly ArrayView<double> Moles = moles;
    public readonly ArrayView<double> Multipliers = multipliers;
    public readonly ArrayView<MixtureState> States = states;
    public readonly ArrayView<int> Status = status;
    public readonly ArrayView<int> Iterations = iterations;
}

/// <summary>Device views of one rocket chunk. Public only because ILGPU requires kernel parameter types to be.</summary>
public readonly struct RocketBatchViews(
    int exitCount, ArrayView<double> chamberPressures, ArrayView<double> reactantEnthalpies, ArrayView<double> temperatureEstimates,
    ArrayView<int> flows, ArrayView<double> elementMoles, ArrayView<double> exitValues, ArrayView<int> exitKinds,
    ArrayView<double> scratchDoubles, ArrayView<int> scratchInts,
    ArrayView<MixtureState> stations, ArrayView<double> moles, ArrayView<double> multipliers, ArrayView<PerformanceFigures> figures,
    ArrayView<int> stationStatus, ArrayView<int> iterations, ArrayView<int> status)
{
    public readonly int ExitCount = exitCount;
    public readonly ArrayView<double> ChamberPressures = chamberPressures;
    public readonly ArrayView<double> ReactantEnthalpies = reactantEnthalpies;
    public readonly ArrayView<double> TemperatureEstimates = temperatureEstimates;
    public readonly ArrayView<int> Flows = flows;
    public readonly ArrayView<double> ElementMoles = elementMoles;
    public readonly ArrayView<double> ExitValues = exitValues;
    public readonly ArrayView<int> ExitKinds = exitKinds;
    public readonly ArrayView<double> ScratchDoubles = scratchDoubles;
    public readonly ArrayView<int> ScratchInts = scratchInts;
    public readonly ArrayView<MixtureState> Stations = stations;
    public readonly ArrayView<double> Moles = moles;
    public readonly ArrayView<double> Multipliers = multipliers;
    public readonly ArrayView<PerformanceFigures> Figures = figures;
    public readonly ArrayView<int> StationStatus = stationStatus;
    public readonly ArrayView<int> Iterations = iterations;
    public readonly ArrayView<int> Status = status;
}

/// <summary>Device views of one transport chunk. Public only because ILGPU requires kernel parameter types to be.</summary>
public readonly struct TransportBatchViews(
    ArrayView<double> temperatures, ArrayView<double> moles, ArrayView<double> scratchDoubles, ArrayView<int> scratchInts,
    ArrayView<TransportFigures> figures, ArrayView<int> status)
{
    public readonly ArrayView<double> Temperatures = temperatures;
    public readonly ArrayView<double> Moles = moles;
    public readonly ArrayView<double> ScratchDoubles = scratchDoubles;
    public readonly ArrayView<int> ScratchInts = scratchInts;
    public readonly ArrayView<TransportFigures> Figures = figures;
    public readonly ArrayView<int> Status = status;
}

/// <summary>The probe of the root's math list: one value per function per input.</summary>
public static class MathProbe
{
    /// <summary>The functions of the root's list, in the order of the probe's outputs.</summary>
    public static readonly IReadOnlyList<string> Functions =
        ["Exp", "Log", "Log10", "Pow", "Sqrt", "Abs", "Min", "Max", "Floor", "Ceiling"];

    /// <summary>Outputs per input.</summary>
    public static int FunctionCount => Functions.Count;

    /// <summary>The exponent the probe passes to Pow.</summary>
    public const double PowExponent = 1.37;
}

/// <summary>The kernel entry points: each slices the views of its case and calls the numerical node. No formula lives here.</summary>
internal static class Kernels
{
    internal static void Equilibrium(Index1D index, SpeciesTableView table, EquilibriumBatchViews batch)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var doublesPerCase = ScratchLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = ScratchLayout.IntsPerCase(speciesCount, elementCount);
        var problem = new EquilibriumProblem((ProblemKind)batch.Kinds[index], batch.Pressures[index], batch.Temperatures[index],
                                             batch.Targets[index], batch.ElementMoles.SubView(index * elementCount, elementCount));
        var scratch = EquilibriumScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                               batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        var result = new EquilibriumResult(batch.Moles.SubView(index * speciesCount, speciesCount),
                                           batch.Multipliers.SubView(index * elementCount, elementCount),
                                           batch.States.SubView(index, 1), batch.Status.SubView(index, 1), batch.Iterations.SubView(index, 1));
        EquilibriumSolver.Solve(in table, in problem, in scratch, in result, false);
    }

    internal static void Rocket(Index1D index, SpeciesTableView table, RocketBatchViews batch)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var exitCount = batch.ExitCount;
        var stationCount = RocketLayout.StationCount(exitCount);
        var doublesPerCase = ScratchLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = ScratchLayout.IntsPerCase(speciesCount, elementCount);
        var problem = new RocketProblem(batch.ChamberPressures[index], batch.ReactantEnthalpies[index], batch.TemperatureEstimates[index],
                                        (FlowModel)batch.Flows[index],
                                        batch.ElementMoles.SubView(index * elementCount, elementCount),
                                        batch.ExitValues.SubView(index * exitCount, exitCount),
                                        batch.ExitKinds.SubView(0, exitCount));
        var scratch = EquilibriumScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                               batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        var result = new RocketResult(batch.Stations.SubView(index * stationCount, stationCount),
                                      batch.Moles.SubView(index * stationCount * speciesCount, stationCount * speciesCount),
                                      batch.Multipliers.SubView(index * stationCount * elementCount, stationCount * elementCount),
                                      batch.Figures.SubView(index * stationCount, stationCount),
                                      batch.StationStatus.SubView(index * stationCount, stationCount),
                                      batch.Iterations.SubView(index * stationCount, stationCount),
                                      batch.Status.SubView(index, 1));
        RocketSolver.Solve(in table, in problem, in scratch, in result);
    }

    internal static void Transport(Index1D index, SpeciesTableView species, TransportTableView transport, TransportBatchViews batch)
    {
        var speciesCount = species.SpeciesCount;
        var elementCount = species.ElementCount;
        var doublesPerCase = TransportLayout.DoublesPerCase(speciesCount, elementCount);
        var intsPerCase = TransportLayout.IntsPerCase(speciesCount, elementCount);
        var scratch = TransportScratch.Slice(batch.ScratchDoubles.SubView(index * doublesPerCase, doublesPerCase),
                                             batch.ScratchInts.SubView(index * intsPerCase, intsPerCase), speciesCount, elementCount);
        batch.Status[index] = (int)TransportSolver.Evaluate(in species, in transport, batch.Temperatures[index],
                                                             batch.Moles.SubView(index * speciesCount, speciesCount), in scratch,
                                                             batch.Figures.SubView(index, 1));
    }

    internal static void Probe(Index1D index, ArrayView<double> inputs, ArrayView<double> outputs)
    {
        var v = inputs[index];
        var o = index * 10;
        outputs[o] = Math.Exp(v);
        outputs[o + 1] = Math.Log(v);
        outputs[o + 2] = Math.Log10(v);
        outputs[o + 3] = Math.Pow(v, MathProbe.PowExponent);
        outputs[o + 4] = Math.Sqrt(v);
        outputs[o + 5] = Math.Abs(v - 1.0);
        outputs[o + 6] = Math.Min(v, 1.0);
        outputs[o + 7] = Math.Max(v, 1.0);
        outputs[o + 8] = Math.Floor(v);
        outputs[o + 9] = Math.Ceiling(v);
    }
}
