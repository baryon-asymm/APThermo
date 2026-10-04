using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// A univariant system over every product of its elements: a mixture whose element moles fix, at one pressure, a plateau
/// temperature at which two condensed assemblages and a gas coexist (CaCO3 = CaO + CO2 under CO2, boiling water, and the
/// like). <see cref="LowSpecies"/> is the condensed species of the low-temperature side, which tells the sides apart,
/// <see cref="ExtentElement"/> the element whose moles per kilogram are the moles of the reaction that run through the plateau
/// (one mole of the element per mole of reaction), and <see cref="Reaction"/> the reaction with its stoichiometric numbers,
/// products positive, from which the enthalpy of the transition is computed.
/// </summary>
internal sealed record UnivariantSystem(string Name, string[] Elements, double[] Ratio, string LowSpecies, int ExtentElement, (string Species, double Nu)[] Reaction)
{
    /// <summary>Calcium carbonate beside carbon dioxide in excess: Ca:C:O = 1:2:5. A composition without the excess has no robust tp state below the plateau.</summary>
    public static UnivariantSystem Calcite { get; } =
        new("CaCO3", ["CA", "C", "O"], [1, 2, 5], "CaCO3", 0, [("CaCO3(cr)", -1), ("CaO(cr)", 1), ("CO2", 1)]);

    /// <summary>Water, the two-phase region of its boiling.</summary>
    public static UnivariantSystem Water { get; } =
        new("water", ["H", "O"], [2, 1], "H2O(L)", 1, [("H2O(L)", -1), ("H2O", 1)]);

    /// <summary>Ammonium chloride sublimation to NH3 and HCl.</summary>
    public static UnivariantSystem AmmoniumChloride { get; } =
        new("NH4Cl", ["N", "H", "CL"], [1, 4, 1], "NH4CL", 0, [("NH4CL(III)", -1), ("NH3", 1), ("HCL", 1)]);

    /// <summary>Calcium hydroxide dehydration to CaO and steam, with steam in excess.</summary>
    public static UnivariantSystem CalciumHydroxide { get; } =
        new("Ca(OH)2", ["CA", "O", "H"], [1, 3, 4], "Ca(OH)2", 0, [("Ca(OH)2(cr)", -1), ("CaO(cr)", 1), ("H2O", 1)]);

    /// <summary>Magnesite beside carbon dioxide in excess: MgCO3 = MgO + CO2.</summary>
    public static UnivariantSystem Magnesite { get; } =
        new("MgCO3", ["MG", "C", "O"], [1, 2, 5], "MgCO3", 0, [("MgCO3(cr)", -1), ("MgO(cr)", 1), ("CO2", 1)]);
}

/// <summary>
/// A univariant system at one pressure, solved by the tree itself: the plateau temperature found by bisecting on the
/// condensed set of tp states of the system's own mixture, the enthalpy of its transition per mole of reaction from the
/// species functions at that temperature (the entropy is that over the temperature, since the Gibbs energy of the reaction
/// vanishes at the plateau) and, for the mixture under test (the system's own or one near it), the tp state just above the
/// plateau and a seed above that. Everything a plateau state is compared with is computed from these; nothing is typed.
/// </summary>
internal sealed class UnivariantRig
{
    /// <summary>K: how far above the plateau the tp state sits that stands for its upper end.</summary>
    private const double Bracket = 1.0e-6;

    /// <summary>K: how far above the plateau the seed is, on the side whose assemblage the low-temperature one decomposes into.</summary>
    private const double SeedOffset = 20.0;

    private const double LowestBisection = 200.0;
    private const double HighestBisection = 3000.0;
    private const int BisectionSteps = 60;

    private UnivariantRig(UnivariantSystem system, SpeciesTable table, double[] elementMoles, double pressure)
    {
        System = system;
        Table = table;
        ElementMoles = elementMoles;
        Pressure = pressure;
        var plateau = PlateauTemperature(ElementMolesOf(system.Elements, system.Ratio));
        Temperature = plateau;
        TransitionEnthalpy = ReactionEnthalpy(plateau);
        TransitionEntropy = TransitionEnthalpy / plateau;
        Upper = Tp(elementMoles, plateau + Bracket);
        Seed = Tp(elementMoles, plateau + SeedOffset);
    }

    public UnivariantSystem System { get; }

    public SpeciesTable Table { get; }

    /// <summary>The element moles per kilogram of the mixture under test.</summary>
    public double[] ElementMoles { get; }

    public double Pressure { get; }

    /// <summary>K: the plateau temperature at this pressure.</summary>
    public double Temperature { get; }

    /// <summary>J/kmol of reaction: the enthalpy of the transition, the sum of the stoichiometric numbers times h at the plateau temperature, from the species functions.</summary>
    public double TransitionEnthalpy { get; }

    /// <summary>J/(kmol·K) of reaction: the entropy of the transition.</summary>
    public double TransitionEntropy { get; }

    /// <summary>The tp state of the mixture under test just above the plateau.</summary>
    public HostSolution Upper { get; }

    /// <summary>The tp state of the mixture under test well above the plateau, whose composition seeds the hp and sp solves.</summary>
    public HostSolution Seed { get; }

    /// <summary>kmol of reaction per kilogram of the mixture under test: its moles of the extent element.</summary>
    public double Extent => ElementMoles[System.ExtentElement];

    /// <summary>The rig of a system at a pressure, for the system's own mixture.</summary>
    public static UnivariantRig Of(UnivariantSystem system, double pressure) =>
        Of(system, system.Ratio, pressure);

    /// <summary>The rig for a mixture other than the system's own: a ratio of element moles near it.</summary>
    public static UnivariantRig Of(UnivariantSystem system, double[] ratio, double pressure) =>
        new(system, TableOver(system.Elements), ElementMolesOf(system.Elements, ratio), pressure);

    /// <summary>The element moles per kilogram of mixture for a ratio of element moles.</summary>
    public static double[] ElementMolesOf(string[] elements, double[] ratio)
    {
        var database = CpuFixture.Shared.Database;
        var mass = elements.Select((element, i) => ratio[i] * database.AtomicWeight(element)).Sum();
        return [.. ratio.Select(r => r / mass)];
    }

    /// <summary>Every ion-free product the database holds over <paramref name="elements"/>.</summary>
    public static SpeciesTable TableOver(string[] elements)
    {
        var database = CpuFixture.Shared.Database;
        var allowed = new HashSet<string>(elements, StringComparer.OrdinalIgnoreCase);
        var names = database.Products
            .Where(s => s.Formula.All(f => allowed.Contains(f.Symbol)) && !s.Formula.Any(f => string.Equals(f.Symbol, "E", StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Name).Distinct().ToList();
        return SpeciesTable.Build(database, elements, names);
    }

    /// <summary>The names of the condensed species with positive moles in a solution, joined; empty when there is none.</summary>
    public static string CondensedOf(HostSolution solution) =>
        string.Join("+", Enumerable.Range(solution.Case.Table.GasCount, solution.Case.Table.SpeciesCount - solution.Case.Table.GasCount)
            .Where(j => solution.Moles[j] > 0.0).Select(j => solution.Case.Table.Species[j]));

    /// <summary>A tp state of the mixture under test at its pressure.</summary>
    public HostSolution Tp(double temperature) => Tp(ElementMoles, temperature);

    /// <summary>The state of the mixture under test at the fraction <paramref name="fraction"/> of the way through the transition, at constant enthalpy and pressure.</summary>
    public HostSolution Hp(double fraction) =>
        Solve(ProblemKind.AssignedEnthalpyPressure, Pressure, Upper.State.Enthalpy - (1.0 - fraction) * TransitionEnthalpy * Extent, Seed.Moles);

    /// <summary>As <see cref="Hp"/>, at constant entropy.</summary>
    public HostSolution Sp(double fraction) =>
        SpAt(Pressure, Upper.State.Entropy - (1.0 - fraction) * TransitionEntropy * Extent);

    /// <summary>
    /// As <see cref="Hp"/>, seeded on the plateau itself (2026-10-03): the temperature estimate is the plateau's, and the moles are
    /// the lever mix of the two tp states a hair either side of it at the same fraction, the seed a temperature search that has
    /// bracketed the plateau hands the final attempt.
    /// </summary>
    public HostSolution HpFromLever(double fraction) =>
        Solve(ProblemKind.AssignedEnthalpyPressure, Pressure, Temperature, Upper.State.Enthalpy - (1.0 - fraction) * TransitionEnthalpy * Extent, LeverMoles(fraction));

    /// <summary>As <see cref="Sp"/>, from the cold start of the report's section 3.1: no estimate of the moles or of the temperature (2026-10-03).</summary>
    public HostSolution SpCold(double fraction) =>
        HostSolver.Solve(
            CpuFixture.Shared.Accelerator,
            new EquilibriumCase(Table, ProblemKind.AssignedEntropyPressure, Pressure, 0.0, Upper.State.Entropy - (1.0 - fraction) * TransitionEntropy * Extent, ElementMoles));

    /// <summary>As <see cref="HpFromLever"/>, at constant entropy.</summary>
    public HostSolution SpFromLever(double fraction) =>
        Solve(ProblemKind.AssignedEntropyPressure, Pressure, Temperature, Upper.State.Entropy - (1.0 - fraction) * TransitionEntropy * Extent, LeverMoles(fraction));

    /// <summary>The moles at <paramref name="fraction"/> of the way through the transition by the lever rule over the tp states at the plateau temperature ∓ <see cref="Bracket"/>.</summary>
    private double[] LeverMoles(double fraction)
    {
        var lower = Tp(ElementMoles, Temperature - Bracket);
        return [.. Upper.Moles.Zip(lower.Moles, (upper, below) => fraction * upper + (1.0 - fraction) * below)];
    }

    /// <summary>A state at constant entropy and an arbitrary pressure, seeded as the plateau's own are.</summary>
    public HostSolution SpAt(double pressure, double entropy) =>
        Solve(ProblemKind.AssignedEntropyPressure, pressure, entropy, Seed.Moles);

    /// <summary>One solve of the mixture under test, the temperature estimate above the plateau, <paramref name="estimate"/> the moles it starts from.</summary>
    public HostSolution Solve(ProblemKind kind, double pressure, double target, double[] estimate) =>
        Solve(kind, pressure, Temperature + SeedOffset, target, estimate);

    /// <summary>One solve of the mixture under test from a given temperature estimate (a march warm-starts from the previous station).</summary>
    public HostSolution Solve(ProblemKind kind, double pressure, double temperature, double target, double[] estimate) =>
        HostSolver.Solve(
            CpuFixture.Shared.Accelerator,
            new EquilibriumCase(Table, kind, pressure, temperature, target, ElementMoles),
            (double[])estimate.Clone());

    private double ReactionEnthalpy(double temperature)
    {
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, Table);
        var view = buffers.View;
        var sum = 0.0;
        foreach (var (species, nu) in System.Reaction)
        {
            sum += nu * SpeciesFunctions.HOverRT(view, Table.PieceOf(species, temperature), temperature);
        }

        return sum * PhysicalConstants.R * temperature;
    }

    private HostSolution Tp(double[] elementMoles, double temperature) =>
        HostSolver.Solve(
            CpuFixture.Shared.Accelerator,
            new EquilibriumCase(Table, ProblemKind.AssignedTemperaturePressure, Pressure, temperature, 0.0, elementMoles));

    /// <summary>Bisection on the tp states' condensed set: the low-temperature species is present below the plateau and absent above.</summary>
    private double PlateauTemperature(double[] elementMoles)
    {
        var low = LowestBisection;
        var high = HighestBisection;
        for (var step = 0; step < BisectionSteps; step++)
        {
            var middle = 0.5 * (low + high);
            var state = Tp(elementMoles, middle);
            if (state.Status == CaseStatus.Ok && !CondensedOf(state).Contains(System.LowSpecies, StringComparison.Ordinal))
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
}
