using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// An equilibrium family of gas-participating plateau states (the 0.2.2 families, StateRecord BOOT.md, "The gas-participating
/// plateau"): one table, one batch of hp and sp cases inside one plateau, the labels of its cases and the plateau temperature.
/// </summary>
internal sealed record PlateauFamily(string Name, SpeciesTable Table, EquilibriumBatch Batch, IReadOnlyList<string> Labels, double PlateauTemperature);

/// <summary>
/// The 0.2.2 equilibrium families, built from inputs the tree computes (nothing is typed but the system and the fractions): the
/// plateau temperature of a univariant system at a pressure, found by bisection on the condensed set of host tp solves of its own
/// mixture; the enthalpy of its reaction from the species functions; and hp and sp targets a fraction of the way through the
/// transition above the tp state a hair over the plateau. A batch carries a temperature estimate and no seed moles, so only
/// states whose cold start reaches the plateau end <c>Ok</c> on the CPU accelerator; the fractions of each family are those that do.
/// </summary>
internal static class GasPlateauFamilies
{
    /// <summary>K: how far above the plateau the tp state sits that stands for the upper end of the transition.</summary>
    private const double Bracket = 1.0e-6;

    private const double LowestBisection = 200.0;
    private const double HighestBisection = 3000.0;
    private const int BisectionSteps = 60;

    private static readonly Dictionary<string, PlateauSystem> Systems = new()
    {
        ["gas-plateau-water"] = new(
            new(["H", "O"], [2, 1], 1), "H2O(L)", [("H2O(L)", -1), ("H2O", 1)], 1.0e5,
            [(ProblemKind.AssignedEntropyPressure, 0.7), (ProblemKind.AssignedEnthalpyPressure, 0.9), (ProblemKind.AssignedEntropyPressure, 0.9)]),
        ["gas-plateau-ammonium-chloride"] = new(
            new(["N", "H", "CL"], [1, 4, 1], 0), "NH4CL", [("NH4CL(III)", -1), ("NH3", 1), ("HCL", 1)], 1.0e5,
            [(ProblemKind.AssignedEntropyPressure, 0.5), (ProblemKind.AssignedEnthalpyPressure, 0.7), (ProblemKind.AssignedEntropyPressure, 0.7),
             (ProblemKind.AssignedEnthalpyPressure, 0.9), (ProblemKind.AssignedEntropyPressure, 0.9)]),
        ["gas-plateau-calcium-hydroxide"] = new(
            new(["CA", "O", "H"], [1, 3, 4], 0), "Ca(OH)2", [("Ca(OH)2(cr)", -1), ("CaO(cr)", 1), ("H2O", 1)], 1.0e5,
            [(ProblemKind.AssignedEntropyPressure, 0.1), (ProblemKind.AssignedEntropyPressure, 0.3), (ProblemKind.AssignedEntropyPressure, 0.7),
             (ProblemKind.AssignedEntropyPressure, 0.9)]),
        ["gas-plateau-calcite"] = new(
            new(["CA", "C", "O"], [1, 2, 5], 0), "CaCO3", [("CaCO3(cr)", -1), ("CaO(cr)", 1), ("CO2", 1)], 1.0e7,
            [(ProblemKind.AssignedEntropyPressure, 0.3), (ProblemKind.AssignedEntropyPressure, 0.9)]),
    };

    /// <summary>
    /// The plateaus whose hp and sp states a cold start reaches only through the 0.2.2 temperature bracket (the 0.2.1 solver left the
    /// states seeded on the one-condensed side of the plateau at <c>TemperatureOutOfRange</c>, StateRecord BOOT.md): calcium and
    /// magnesium carbonate under their own carbon dioxide at 1e5 Pa, at the fractions of the transition the families above leave out.
    /// </summary>
    private static readonly Dictionary<string, PlateauSystem> BracketedSystems = new()
    {
        ["bracket-calcite-1e5"] = new(
            new(["CA", "C", "O"], [1, 2, 5], 0), "CaCO3", [("CaCO3(cr)", -1), ("CaO(cr)", 1), ("CO2", 1)], 1.0e5,
            [(ProblemKind.AssignedEnthalpyPressure, 0.1), (ProblemKind.AssignedEntropyPressure, 0.1), (ProblemKind.AssignedEnthalpyPressure, 0.3),
             (ProblemKind.AssignedEntropyPressure, 0.3), (ProblemKind.AssignedEnthalpyPressure, 0.5), (ProblemKind.AssignedEntropyPressure, 0.5)]),
        ["bracket-magnesite-1e5"] = new(
            new(["MG", "C", "O"], [1, 2, 5], 0), "MgCO3", [("MgCO3(cr)", -1), ("MgO(cr)", 1), ("CO2", 1)], 1.0e5,
            [(ProblemKind.AssignedEnthalpyPressure, 0.1), (ProblemKind.AssignedEntropyPressure, 0.1), (ProblemKind.AssignedEnthalpyPressure, 0.3),
             (ProblemKind.AssignedEntropyPressure, 0.3), (ProblemKind.AssignedEnthalpyPressure, 0.5), (ProblemKind.AssignedEntropyPressure, 0.5)]),
    };

    /// <summary>The names of the plateau families that end <c>Ok</c> only through the bracket (<see cref="RecoveryFamilies"/> keeps their <c>Ok</c> cases).</summary>
    public static IReadOnlyCollection<string> BracketedNames => BracketedSystems.Keys;

    /// <summary>The names of the 0.2.2 equilibrium families as theory data.</summary>
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var name in Systems.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>One of the 0.2.2 families by its name, as one batch with its table.</summary>
    public static PlateauFamily Family(SpeciesDatabase database, string name)
    {
        ArgumentNullException.ThrowIfNull(database);
        var system = Systems.TryGetValue(name, out var known) ? known : BracketedSystems[name];
        var table = TableOver(database, system.Mixture.Elements);
        var moles = ElementMolesOf(database, system.Mixture.Elements, system.Mixture.Ratio);
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var plateau = PlateauTemperature(tables, system, moles);
        var upper = TpAt(tables, system, moles, plateau + Bracket);
        var enthalpy = ReactionEnthalpy(tables, system, plateau);
        var extent = moles[system.Mixture.ExtentElement];
        var batch = new EquilibriumBatch(system.States.Length, table.ElementCount);
        var labels = new List<string>();
        for (var k = 0; k < system.States.Length; k++)
        {
            var (kind, fraction) = system.States[k];
            batch.Kind[k] = kind;
            batch.Pressure[k] = system.Pressure;
            batch.Temperature[k] = 0.0;
            batch.Target[k] = kind == ProblemKind.AssignedEnthalpyPressure
                ? upper.State.Enthalpy - (1.0 - fraction) * enthalpy * extent
                : upper.State.Entropy - (1.0 - fraction) * enthalpy / plateau * extent;
            Array.Copy(moles, 0, batch.ElementMoles, k * table.ElementCount, table.ElementCount);
            labels.Add($"{name} {kind} fraction {fraction}");
        }

        return new PlateauFamily(name, table, batch, labels, plateau);
    }

    private static HostEquilibriumCase TpAt(UploadedTables tables, PlateauSystem system, double[] moles, double temperature)
    {
        var batch = new EquilibriumBatch(1, moles.Length);
        batch.Kind[0] = ProblemKind.AssignedTemperaturePressure;
        batch.Pressure[0] = system.Pressure;
        batch.Temperature[0] = temperature;
        Array.Copy(moles, batch.ElementMoles, moles.Length);
        return HostSolves.Equilibrium(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, batch, 0);
    }

    /// <summary>Bisection on the condensed set of tp states: the low-temperature species is present below the plateau and absent above.</summary>
    private static double PlateauTemperature(UploadedTables tables, PlateauSystem system, double[] moles)
    {
        var table = tables.SpeciesBuffers.Table;
        var low = LowestBisection;
        var high = HighestBisection;
        for (var step = 0; step < BisectionSteps; step++)
        {
            var middle = 0.5 * (low + high);
            var state = TpAt(tables, system, moles, middle);
            var hasLow = Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount)
                .Any(j => state.Moles[j] > 0.0 && table.Species[j].StartsWith(system.LowSpecies, StringComparison.Ordinal));
            if (state.Status == CaseStatus.Ok && !hasLow)
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

    /// <summary>J/kmol of reaction: the stoichiometric numbers times h at the plateau temperature, from the species functions.</summary>
    private static double ReactionEnthalpy(UploadedTables tables, PlateauSystem system, double temperature)
    {
        var table = tables.SpeciesBuffers.Table;
        var view = tables.SpeciesBuffers.View;
        var sum = 0.0;
        foreach (var (species, nu) in system.Reaction)
        {
            sum += nu * SpeciesFunctions.HOverRT(view, table.PieceOf(species, temperature), temperature);
        }

        return sum * PhysicalConstants.R * temperature;
    }

    /// <summary>The table of every gaseous and condensed product of the database made of the given elements, ions excluded.</summary>
    internal static SpeciesTable TableOver(SpeciesDatabase database, string[] elements)
    {
        var allowed = new HashSet<string>(elements, StringComparer.OrdinalIgnoreCase);
        var names = database.Products
            .Where(s => s.Formula.All(f => allowed.Contains(f.Symbol)) && !s.Formula.Any(f => string.Equals(f.Symbol, "E", StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Name).Distinct().ToList();
        return SpeciesTable.Build(database, elements, names);
    }

    /// <summary>The element moles per kilogram of a mixture whose elements are in the given ratio of moles.</summary>
    internal static double[] ElementMolesOf(SpeciesDatabase database, string[] elements, double[] ratio)
    {
        var mass = elements.Select((element, i) => ratio[i] * database.AtomicWeight(element)).Sum();
        return [.. ratio.Select(r => r / mass)];
    }

    /// <summary>A univariant system at one pressure: its mixture, the reaction through the plateau and the states of the family.</summary>
    private sealed record PlateauSystem(
        Mixture Mixture, string LowSpecies, (string Species, double Nu)[] Reaction, double Pressure, (ProblemKind Kind, double Fraction)[] States);

    /// <summary>The elements of a system, their ratio of moles and the one whose moles per kilogram are the moles of the reaction (one mole of it per mole of reaction).</summary>
    private sealed record Mixture(string[] Elements, double[] Ratio, int ExtentElement);
}
