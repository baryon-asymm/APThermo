using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>Everything one equilibrium solve needs, named rather than lined up as arguments.</summary>
internal sealed record EquilibriumCase(
    SpeciesTable Table, ProblemKind Kind, double Pressure, double Temperature, double Target, double[] ElementMoles);

/// <summary>The case solved and the five views of <see cref="EquilibriumResult"/> copied out of the accelerator buffers.</summary>
internal sealed record HostSolution(
    EquilibriumCase Case, double[] Moles, double[] Multipliers, MixtureState State, CaseStatus Status, int Iterations)
{
    /// <summary>Gaseous plus condensed moles per kilogram: the denominator of the reference's mole fractions.</summary>
    public double TotalMoles => Moles.Sum();

    /// <summary>The reference reports one fraction per database name: the pieces of a cut species sum under it.</summary>
    public double MoleFraction(string species) => Case.Table.IndicesOf(species).Sum(index => Moles[index]) / TotalMoles;
}

/// <summary>Runs the solver on the host over CPU-accelerator buffers, with the inputs of a fixture case or given directly.</summary>
internal static class HostSolver
{
    public static ProblemKind KindOf(CeaCase c) => c.Kind switch
    {
        "tp" => ProblemKind.AssignedTemperaturePressure,
        "hp" => ProblemKind.AssignedEnthalpyPressure,
        "sp" => ProblemKind.AssignedEntropyPressure,
        _ => throw new ArgumentException($"fixture kind {c.Kind} is not an equilibrium problem", nameof(c)),
    };

    public static string[] ElementsOf(CeaCase c) =>
        [.. c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name)];

    public static double[] ElementMolesOf(CeaCase c) =>
        [.. c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble())];

    public static string[] ProductsOf(CeaCase c) =>
        [.. c.Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!)];

    public static double PressureOf(CeaCase c) => c.Inputs.GetProperty("pressure").GetDouble();

    public static double TemperatureOf(CeaCase c) =>
        c.Kind == "tp" ? c.Inputs.GetProperty("temperature").GetDouble() : 0.0;

    public static double TargetOf(CeaCase c) => c.Kind switch
    {
        "hp" => c.Inputs.GetProperty("enthalpy").GetDouble(),
        "sp" => c.Inputs.GetProperty("entropy").GetDouble(),
        _ => 0.0,
    };

    public static SpeciesTable BuildTable(SpeciesDatabase database, CeaCase c) =>
        SpeciesTable.Build(database, ElementsOf(c), ProductsOf(c));

    /// <summary>A key identifying the table a case needs: cases with equal keys can share a batch.</summary>
    public static string TableKey(CeaCase c) =>
        string.Join(",", ElementsOf(c)) + "|" + string.Join(",", ProductsOf(c));

    /// <summary>The case a fixture describes, over a table built for it.</summary>
    public static EquilibriumCase Of(CpuFixture fixture, CeaCase c) => Of(BuildTable(fixture.Database, c), c);

    /// <summary>The case a fixture describes, over a table the caller chose (a shared one, or one with a species left out).</summary>
    public static EquilibriumCase Of(SpeciesTable table, CeaCase c) =>
        new(table, KindOf(c), PressureOf(c), TemperatureOf(c), TargetOf(c), ElementMolesOf(c));

    public static HostSolution Solve(CpuFixture fixture, CeaCase c) => Solve(fixture.Accelerator, Of(fixture, c));

    /// <summary>One solve; with <paramref name="estimate"/> the moles given are the initial estimate.</summary>
    public static HostSolution Solve(Accelerator accelerator, EquilibriumCase problem, double[]? estimate = null) =>
        Run(accelerator, problem, estimate, frozen: false);

    /// <summary>One frozen solve: the composition is held at <paramref name="composition"/> and the temperature follows from it.</summary>
    public static HostSolution SolveFrozen(Accelerator accelerator, EquilibriumCase problem, double[] composition) =>
        Run(accelerator, problem, composition, frozen: true);

    private static HostSolution Run(Accelerator accelerator, EquilibriumCase problem, double[]? moles, bool frozen)
    {
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(problem.ElementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var molesBuffer = moles is null ? accelerator.Allocate1D<double>(speciesCount) : accelerator.Allocate1D(moles);
        using var multipliers = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        if (moles is null)
        {
            molesBuffer.MemSetToZero();
        }

        var input = new EquilibriumProblem(problem.Kind, problem.Pressure, problem.Temperature, problem.Target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(molesBuffer.View, multipliers.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        if (frozen)
        {
            EquilibriumSolver.SolveFrozen(in view, in input, in scratch, in result);
        }
        else
        {
            EquilibriumSolver.Solve(in view, in input, in scratch, in result, moles is not null);
        }

        return new HostSolution(
            Case: problem, Moles: molesBuffer.GetAsArray1D(), Multipliers: multipliers.GetAsArray1D(),
            State: state.GetAsArray1D()[0], Status: (CaseStatus)status.GetAsArray1D()[0], Iterations: iterations.GetAsArray1D()[0]);
    }

    /// <summary>The fixture file names of a kind, without extension; <see cref="Load"/> reads one back.</summary>
    public static IEnumerable<string> CaseNames(string kind) =>
        FixtureFiles.Enumerate(kind).Select(Path.GetFileNameWithoutExtension)!;

    /// <summary>The fixture files of a kind as theory data: the file name without extension; <see cref="Load"/> reads it back.</summary>
    public static TheoryData<string> Cases(string kind)
    {
        var data = new TheoryData<string>();
        foreach (var name in CaseNames(kind))
        {
            data.Add(name);
        }

        return data;
    }

    public static CeaCase Load(string kind, string name) =>
        CeaFixtures.Load(Path.Combine(FixtureFiles.Root, kind, name + ".json"));
}
