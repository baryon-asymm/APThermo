using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU.Runtime;

namespace AerospacePropellantThermodynamics.Equilibrium.Tests;

/// <summary>What one host call of the solver produced, copied out of the accelerator buffers.</summary>
internal sealed record HostSolution(
    SpeciesTable Table, double[] ElementMoles, double[] Moles, double[] Multipliers,
    MixtureState State, CaseStatus Status, int Iterations)
{
    /// <summary>Gaseous plus condensed moles per kilogram: the denominator of the reference's mole fractions.</summary>
    public double TotalMoles => Moles.Sum();

    public double MoleFraction(string species)
    {
        var index = Table.IndexOf(species);
        return index < 0 ? 0.0 : Moles[index] / TotalMoles;
    }
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
        c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Name).ToArray();

    public static double[] ElementMolesOf(CeaCase c) =>
        c.Inputs.GetProperty("elementMoles").EnumerateObject().Select(p => p.Value.GetDouble()).ToArray();

    public static string[] ProductsOf(CeaCase c) =>
        c.Inputs.GetProperty("products").EnumerateArray().Select(e => e.GetString()!).ToArray();

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

    public static HostSolution Solve(CpuFixture fixture, CeaCase c) =>
        Solve(fixture.Accelerator, BuildTable(fixture.Database, c), KindOf(c), PressureOf(c), TemperatureOf(c), TargetOf(c), ElementMolesOf(c));

    /// <summary>One solve; with <paramref name="estimate"/> the moles are the initial estimate, with <paramref name="frozen"/> the composition is held.</summary>
    public static HostSolution Solve(Accelerator accelerator, SpeciesTable table, ProblemKind kind, double pressure, double temperature,
                                     double target, double[] elementMoles, double[]? estimate = null, bool frozen = false)
    {
        using var buffers = SpeciesTableBuffers.Upload(accelerator, table);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        using var elements = accelerator.Allocate1D(elementMoles);
        using var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount));
        using var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount));
        using var moles = estimate is null ? accelerator.Allocate1D<double>(speciesCount) : accelerator.Allocate1D(estimate);
        using var multipliers = accelerator.Allocate1D<double>(elementCount);
        using var state = accelerator.Allocate1D<MixtureState>(1);
        using var status = accelerator.Allocate1D<int>(1);
        using var iterations = accelerator.Allocate1D<int>(1);
        if (estimate is null)
        {
            moles.MemSetToZero();
        }

        var problem = new EquilibriumProblem(kind, pressure, temperature, target, elements.View);
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
        var view = buffers.View;
        if (frozen)
        {
            EquilibriumSolver.SolveFrozen(in view, in problem, in scratch, in result);
        }
        else
        {
            EquilibriumSolver.Solve(in view, in problem, in scratch, in result, estimate is not null);
        }

        return new HostSolution(table, elementMoles, moles.GetAsArray1D(), multipliers.GetAsArray1D(), state.GetAsArray1D()[0],
                                (CaseStatus)status.GetAsArray1D()[0], iterations.GetAsArray1D()[0]);
    }

    /// <summary>The fixture files of a kind as theory data: the file name without extension; <see cref="Load"/> reads it back.</summary>
    public static IEnumerable<object[]> Cases(string kind) =>
        FixtureFiles.Enumerate(kind).Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    public static CeaCase Load(string kind, string name) =>
        CeaFixtures.Load(Path.Combine(FixtureFiles.Root, kind, name + ".json"));
}
