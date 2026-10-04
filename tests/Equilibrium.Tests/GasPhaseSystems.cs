namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The systems whose equilibrium holds no gas phase, over every ion-free product of their elements (GasPhase BOOT.md,
/// acceptance criteria): a name, the elements and the ratio of their moles. The ratio of the last element is scaled by
/// <c>1 + ε</c> in a case, so that a composition slightly short of or beyond stoichiometry is one parameter.
/// </summary>
internal sealed record GaslessSystem(string Name, string[] Elements, double[] Ratio)
{
    /// <summary>Potassium superoxide beside its lower oxide K2O2, or oxygen when the oxygen is in excess.</summary>
    public static GaslessSystem Superoxide { get; } = new("KO2", ["K", "O"], [1.0, 2.0]);

    /// <summary>Water: liquid below its boiling point at the pressure, the gas phase holding nothing.</summary>
    public static GaslessSystem Water { get; } = new("H2O", ["H", "O"], [2.0, 1.0]);

    /// <summary>Alumina.</summary>
    public static GaslessSystem Alumina { get; } = new("Al2O3", ["AL", "O"], [2.0, 3.0]);

    /// <summary>Potassium chloride.</summary>
    public static GaslessSystem Chloride { get; } = new("KCl", ["K", "CL"], [1.0, 1.0]);

    /// <summary>Potassium chloride beside potassium superoxide: three elements, two salts.</summary>
    public static GaslessSystem ChlorideAndSuperoxide { get; } = new("KCl+KO2", ["K", "CL", "O"], [2.0, 1.0, 2.0]);

    /// <summary>A thermite, aluminium and iron oxide: Al:Fe:O = 2:2:3.</summary>
    public static GaslessSystem Thermite { get; } = new("thermite", ["AL", "FE", "O"], [2.0, 2.0, 3.0]);

    /// <summary>Calcium carbonate below its decomposition: Ca:C:O = 1:1:3.</summary>
    public static GaslessSystem Carbonate { get; } = new("CaCO3", ["CA", "C", "O"], [1.0, 1.0, 3.0]);

    /// <summary>Magnesia.</summary>
    public static GaslessSystem Magnesia { get; } = new("MgO", ["MG", "O"], [1.0, 1.0]);

    /// <summary>Lithium oxide.</summary>
    public static GaslessSystem LithiumOxide { get; } = new("Li2O", ["LI", "O"], [2.0, 1.0]);

    /// <summary>Every system above, for a test that names one by its <see cref="Name"/>.</summary>
    public static IReadOnlyList<GaslessSystem> All { get; } =
        [Superoxide, Water, Alumina, Chloride, ChlorideAndSuperoxide, Thermite, Carbonate, Magnesia, LithiumOxide];

    /// <summary>The system of that name.</summary>
    public static GaslessSystem Named(string name) => All.Single(system => system.Name == name);

    /// <summary>The element moles per kilogram at ε of the last element's ratio.</summary>
    public double[] ElementMoles(double epsilon)
    {
        var ratio = (double[])Ratio.Clone();
        ratio[^1] *= 1.0 + epsilon;
        return UnivariantRig.ElementMolesOf(Elements, ratio);
    }

    /// <summary>The tp case of the system at a temperature, a pressure and ε.</summary>
    public EquilibriumCase Case(double temperature, double pressure, double epsilon) =>
        new(UnivariantRig.TableOver(Elements), ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, ElementMoles(epsilon));
}
