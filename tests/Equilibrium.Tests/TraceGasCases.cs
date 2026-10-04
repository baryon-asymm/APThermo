using System.Globalization;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// One tp state of a trace-gas family (TraceGas BOOT.md, <c>## Acceptance criteria</c>): a mixture of elements whose gas phase
/// is a trace beside condensed species, or one of whose directions of the multipliers only trace gases carry, at an assigned
/// temperature and pressure. The mixture is given as the ratio of its element moles, as the scans of the design were; the
/// <see cref="Name"/> identifies the state in a report and in the declared leftovers.
/// </summary>
internal sealed record TraceGasCase(string Name, string[] Elements, double[] Ratio, double Pressure, double Temperature)
{
    /// <summary>The element moles per kilogram of the mixture.</summary>
    public double[] ElementMoles => UnivariantRig.ElementMolesOf(Elements, Ratio);

    /// <summary>The tp problem of the state over every ion-free product of its elements.</summary>
    public EquilibriumCase AsCase() =>
        new(TraceGasCases.TableOver(Elements), ProblemKind.AssignedTemperaturePressure, Pressure, Temperature, 0.0, ElementMoles);

    /// <summary>The cold tp solve of the state over every ion-free product of its elements.</summary>
    public HostSolution Solve() => HostSolver.Solve(CpuFixture.Shared.Accelerator, AsCase());

    /// <summary>The hp or sp problem at the enthalpy or entropy of <paramref name="tp"/>, the converged tp state of this case.</summary>
    public EquilibriumCase CaseAtStateOf(HostSolution tp, ProblemKind kind) =>
        new(
            TraceGasCases.TableOver(Elements), kind, Pressure, 0.0,
            kind == ProblemKind.AssignedEnthalpyPressure ? tp.State.Enthalpy : tp.State.Entropy, ElementMoles);

    /// <summary>The cold hp or sp solve at the enthalpy or entropy of <paramref name="tp"/>, the converged tp state of this case.</summary>
    public HostSolution SolveAtStateOf(HostSolution tp, ProblemKind kind) =>
        HostSolver.Solve(CpuFixture.Shared.Accelerator, CaseAtStateOf(tp, kind));

    /// <summary>
    /// The hp or sp solve at the enthalpy or entropy of <paramref name="tp"/>, warm-started as the design's scans were: from the
    /// composition <paramref name="seed"/> with the temperature <paramref name="estimate"/> as the initial estimate.
    /// </summary>
    public HostSolution SolveWarmAtStateOf(HostSolution tp, ProblemKind kind, HostSolution seed, double estimate) =>
        HostSolver.Solve(
            CpuFixture.Shared.Accelerator,
            new EquilibriumCase(
                TraceGasCases.TableOver(Elements), kind, Pressure, estimate,
                kind == ProblemKind.AssignedEnthalpyPressure ? tp.State.Enthalpy : tp.State.Entropy, ElementMoles),
            seed.Moles);

    /// <summary>
    /// The hp or sp solve at the h or s of <paramref name="tp"/> in a <paramref name="mode"/>: <c>hp-cold</c> and <c>sp-cold</c> from the
    /// defaults, <c>hp-warm</c> and <c>sp-warm</c> from the composition and temperature of the tp state itself, <c>hp-warm5</c> and
    /// <c>sp-warm5</c> from the tp state 5 K above (the starts of the design's scans).
    /// </summary>
    public HostSolution SolveInMode(HostSolution tp, string mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        var kind = mode.StartsWith("hp", StringComparison.Ordinal) ? ProblemKind.AssignedEnthalpyPressure : ProblemKind.AssignedEntropyPressure;
        return mode.EndsWith("cold", StringComparison.Ordinal) ? SolveAtStateOf(tp, kind)
            : mode.EndsWith("warm", StringComparison.Ordinal) ? SolveWarmAtStateOf(tp, kind, tp, Temperature)
            : SolveWarmAtStateOf(tp, kind, At(Temperature + 5.0).Solve(), Temperature + 5.0);
    }

    /// <summary>The same state at another temperature: a seed for a warm start.</summary>
    public TraceGasCase At(double temperature) => this with { Temperature = temperature };

    public override string ToString() => Name;
}

/// <summary>
/// The families of trace-gas tp states the tests of this node walk: generated from their parameters here, not typed, with the
/// plateau temperatures found by the tree itself (<see cref="UnivariantRig"/>), as the design's scans were.
/// </summary>
internal static class TraceGasCases
{
    private static readonly Dictionary<string, SpeciesTable> Tables = [];
    private static readonly Dictionary<(string, double), double> Plateaus = [];
    private static readonly Dictionary<string, TraceGasCase> Registry = [];

    /// <summary>Every ion-free product of the elements, built once per element list.</summary>
    public static SpeciesTable TableOver(string[] elements)
    {
        var key = string.Join(",", elements);
        lock (Tables)
        {
            if (!Tables.TryGetValue(key, out var table))
            {
                table = UnivariantRig.TableOver(elements);
                Tables[key] = table;
            }

            return table;
        }
    }

    /// <summary>The cases of a family as theory data (their names); <see cref="Named"/> gives a case back, which keeps each state a test of its own.</summary>
    public static TheoryData<string> Names(IEnumerable<TraceGasCase> family)
    {
        var data = new TheoryData<string>();
        lock (Registry)
        {
            foreach (var state in family)
            {
                Registry[state.Name] = state;
                data.Add(state.Name);
            }
        }

        return data;
    }

    /// <summary>Registers a case under its name, as <see cref="Names"/> does, and returns the name.</summary>
    public static string Register(TraceGasCase state)
    {
        lock (Registry)
        {
            Registry[state.Name] = state;
        }

        return state.Name;
    }

    /// <summary>The case registered under a name by <see cref="Names"/>.</summary>
    public static TraceGasCase Named(string name)
    {
        lock (Registry)
        {
            return Registry[name];
        }
    }

    /// <summary>MgCO3 under CO2 at Mg:C:O = 1:2:5 and 10 MPa, from 700 to 900 K every 5 K: the band where the trace carriers CO and O2 walk one e-fold per step in the reduced iteration.</summary>
    public static IEnumerable<TraceGasCase> MagnesiteBand()
    {
        for (var temperature = 700.0; temperature <= 900.0; temperature += 5.0)
        {
            yield return new TraceGasCase(Name("magnesite-band", temperature), ["MG", "C", "O"], [1, 2, 5], 1.0e7, temperature);
        }
    }

    /// <summary>CaCO3 + d CO2 (d from 1e-8 to 1e-5) at 1 kPa to 10 MPa, from 0.01 to 300 K below the plateau.</summary>
    public static IEnumerable<TraceGasCase> CalciteBelowThePlateau()
    {
        foreach (var d in new[] { 1.0e-8, 1.0e-6, 1.0e-5 })
        {
            foreach (var pressure in new[] { 1.0e3, 1.0e5, 1.0e7 })
            {
                foreach (var below in new[] { 300.0, 50.0, 1.0, 0.01 })
                {
                    yield return new TraceGasCase(
                        Name("calcite", d, pressure, below), ["CA", "C", "O"], [1, 1 + d, 3 + 2 * d], pressure,
                        Plateau(UnivariantSystem.Calcite, pressure) - below);
                }
            }
        }
    }

    /// <summary>MgCO3 + 1e-6 CO2 below its plateau, tp: the state the reduced iteration closed hp and sp on with a gas that had not converged.</summary>
    public static IEnumerable<TraceGasCase> MagnesiteBelowThePlateau()
    {
        foreach (var pressure in new[] { 1.0e3, 1.0e4, 1.0e5, 1.0e6 })
        {
            foreach (var below in new[] { 200.0, 100.0, 20.0, 1.0 })
            {
                yield return new TraceGasCase(
                    Name("magnesite-below", pressure, below), ["MG", "C", "O"], [1.0, 1.0 + 1.0e-6, 3.0 + 2.0e-6], pressure,
                    Plateau(UnivariantSystem.Magnesite, pressure) - below);
            }
        }
    }

    /// <summary>
    /// MgCO3 under CO2 at Mg:C:O = 1:2:5 below its plateau, at 1 kPa to 1 MPa, from 100 to 1 K below it (not the 200 K below, which at 1 kPa
    /// is under the data of the MgCO3 record): the states whose hp and sp derivative system the element rows left singular, where the
    /// direction π_O − π_Mg − 2π_C is carried only by trace gases.
    /// </summary>
    public static IEnumerable<TraceGasCase> MagnesiteWithCarbonDioxide() =>
        MagnesitePressures.SelectMany(pressure => MagnesiteDistances.Select(below =>
            new TraceGasCase(Name("magnesite-co2", pressure, below), ["MG", "C", "O"], [1.0, 2.0, 5.0], pressure, Plateau(UnivariantSystem.Magnesite, pressure) - below)));

    /// <summary>Al2O3 + 1e-10 and 1e-8 O, KCl + 1e-10 Cl, CaCO3 + 1e-6 O: the excesses whose failed iterates hold no condensed species or the wrong ones.</summary>
    public static IEnumerable<TraceGasCase> DegenerateExcesses()
    {
        foreach (var pressure in new[] { 1.0e3, 1.0e5, 1.0e7 })
        {
            foreach (var temperature in new[] { 1000.0, 2000.0, 3000.0 })
            {
                foreach (var excess in new[] { 1.0e-10, 1.0e-8 })
                {
                    yield return new TraceGasCase(Name("alumina", excess, pressure, temperature), ["AL", "O"], [2.0, 3.0 * (1 + excess)], pressure, temperature);
                }
            }

            foreach (var temperature in new[] { 1000.0, 1200.0 })
            {
                yield return new TraceGasCase(Name("kcl", 1.0e-10, pressure, temperature), ["K", "CL"], [1.0, 1.0 * (1 + 1.0e-10)], pressure, temperature);
            }

            yield return new TraceGasCase(Name("calcite-oxygen", 1.0e-6, pressure, 300.0), ["CA", "C", "O"], [1.0, 1.0, 3.0 * (1 + 1.0e-6)], pressure, 300.0);
        }
    }

    /// <summary>
    /// The states with a gas of 1e-6 of the mixture or less that the reduced iteration closed <c>Ok</c> with the gas converged only to
    /// 3e-8 to 5e-8 (design D2): NaCl + 1e-6 Cl at 1 200 K and 10 MPa, KO2 + 1e-6 O at 1 000 K, and KCl + 1e-6 Cl at 1 000 K, each at the
    /// pressures the design found them at.
    /// </summary>
    public static IEnumerable<TraceGasCase> LooseOks() =>
    [
        Shifted("loose-nacl", ["NA", "CL"], [1.0, 1.0], 1.0e-6, 1.0e7, 1200.0),
        Shifted("loose-ko2", ["K", "O"], [1.0, 2.0], 1.0e-6, 1.0e3, 1000.0),
        Shifted("loose-ko2", ["K", "O"], [1.0, 2.0], 1.0e-6, 1.0e5, 1000.0),
        Shifted("loose-kcl", ["K", "CL"], [1.0, 1.0], 1.0e-6, 1.0e5, 1000.0),
        Shifted("loose-kcl", ["K", "CL"], [1.0, 1.0], 1.0e-6, 1.0e7, 1000.0),
    ];

    /// <summary>
    /// The systems of the design's scan of 17 two- and three-element mixtures, as (name, elements, ratio). The last element's moles carry
    /// the excess ε of a state: a mixture of exact stoichiometry at ε = 0.
    /// </summary>
    public static IReadOnlyList<(string Name, string[] Elements, double[] Ratio)> Systems { get; } =
    [
        ("KO2", ["K", "O"], [1.0, 2.0]), ("K2O2", ["K", "O"], [1.0, 1.0]), ("K2O", ["K", "O"], [2.0, 1.0]), ("K:O=1:1.5", ["K", "O"], [1.0, 1.5]),
        ("NaO2", ["NA", "O"], [1.0, 2.0]), ("H2O", ["H", "O"], [2.0, 1.0]), ("Al2O3", ["AL", "O"], [2.0, 3.0]), ("Al:O=1:1", ["AL", "O"], [1.0, 1.0]),
        ("KCl", ["K", "CL"], [1.0, 1.0]), ("NaCl", ["NA", "CL"], [1.0, 1.0]), ("KCl+KO2", ["K", "CL", "O"], [2.0, 1.0, 2.0]),
        ("Al(OH)3", ["AL", "H", "O"], [1.0, 3.0, 3.0]), ("thermite", ["AL", "FE", "O"], [2.0, 2.0, 3.0]), ("C:O=2:1", ["C", "O"], [1.0, 0.5]),
        ("Li2O", ["LI", "O"], [2.0, 1.0]), ("MgO", ["MG", "O"], [1.0, 1.0]), ("CaCO3", ["CA", "C", "O"], [1.0, 1.0, 3.0]),
    ];

    /// <summary>K: the junction of the data's two temperature ranges, where the NASA fits of the gases of most species meet.</summary>
    public const double JunctionTemperature = 1000.0;

    /// <summary>
    /// The nine all-gas systems at 1 kPa, 100 kPa and 10 MPa, each at the junction of the data's two temperature ranges, at the double below and
    /// the double above it, and at its temperature times 1 ± 1e-9: the tp states whose hp and sp states the iteration cannot reach inside the
    /// jump of the data (TraceGas BOOT.md, "The data junction"). 135 states.
    /// </summary>
    public static IEnumerable<TraceGasCase> JunctionStates() =>
        JunctionSystems.SelectMany(system => ScanPressures.SelectMany(pressure => JunctionTemperatures.Select(temperature =>
            new TraceGasCase(Name(system.Name, pressure, temperature), system.Elements, system.Ratio, pressure, temperature))));

    private static readonly double[] JunctionTemperatures =
        [JunctionTemperature, Math.BitDecrement(JunctionTemperature), Math.BitIncrement(JunctionTemperature), JunctionTemperature * (1.0 + 1.0e-9), JunctionTemperature * (1.0 - 1.0e-9)];

    private static readonly (string Name, string[] Elements, double[] Ratio)[] JunctionSystems =
    [
        ("h2o", ["H", "O"], [2.0, 1.0]), ("h4o", ["H", "O"], [4.0, 1.0]), ("h2o2", ["H", "O"], [2.0, 2.0]),
        ("co2", ["C", "O"], [1.0, 2.0]), ("co3", ["C", "O"], [1.0, 3.0]), ("air", ["N", "O"], [4.0, 1.0]),
        ("chon", ["C", "H", "O", "N"], [1.0, 4.0, 3.0, 2.0]), ("hcl", ["H", "CL"], [1.0, 1.0]), ("kcl3", ["K", "CL"], [1.0, 3.0]),
    ];

    /// <summary>The 17 systems at the excesses <paramref name="excesses"/> of their last element, at 300 to 3000 K and 1 kPa, 100 kPa and 10 MPa.</summary>
    public static IEnumerable<TraceGasCase> Scan(double[] excesses) =>
        Systems.SelectMany(system => ScanPressures.SelectMany(pressure => ScanTemperatures.SelectMany(temperature =>
            excesses.Select(excess => Shifted(system.Name, system.Elements, system.Ratio, excess, pressure, temperature)))));

    /// <summary>The excesses of the trace-excess scan: ± 1e-8 and ± 1e-10 of the last element, the states the design's scans did not walk.</summary>
    public static double[] TraceScanExcesses { get; } = [-1e-8, -1e-10, 1e-8, 1e-10];

    /// <summary>The 17 systems at <see cref="TraceScanExcesses"/>, 1 632 tp states (<see cref="Scan"/>).</summary>
    public static IEnumerable<TraceGasCase> TraceScan() => Scan(TraceScanExcesses);

    /// <summary>The 13 excesses of the design's binary scan.</summary>
    public static double[] BinaryExcesses { get; } = [-1e-2, -1e-4, -1e-6, -1e-8, -1e-10, -1e-12, 0.0, 1e-12, 1e-10, 1e-8, 1e-6, 1e-4, 1e-2];

    /// <summary>The five binary systems of the design's second scan (KO2, NaO2, H2O, Al2O3, KCl) at the given excesses of the last element (<see cref="BinaryExcesses"/> are the design's 13), each at its own temperatures and at 1 kPa, 100 kPa and 10 MPa.</summary>
    public static IEnumerable<TraceGasCase> Binary(double[] excesses) =>
        BinarySystems.SelectMany(system => ScanPressures.SelectMany(pressure => system.Temperatures.SelectMany(temperature =>
            excesses.Select(excess => Shifted(system.Name, system.Elements, system.Ratio, excess, pressure, temperature)))));

    /// <summary>The seven excesses of the design's scan of the 17 systems.</summary>
    public static double[] ScanExcesses { get; } = [-1e-2, -1e-6, -1e-12, 0.0, 1e-12, 1e-6, 1e-2];

    /// <summary>
    /// The tp states of the scan fact: the binary scan (1 170 states), the scan of the 17 systems (2 856), and the families of the other
    /// facts of this node, each state once.
    /// </summary>
    public static IEnumerable<TraceGasCase> ScanFamilies() =>
        Binary(BinaryExcesses).Concat(Scan(ScanExcesses)).Concat(CalciteBelowThePlateau()).Concat(MagnesiteBelowThePlateau())
            .Concat(MagnesiteWithCarbonDioxide()).Concat(DegenerateExcesses()).Concat(MagnesiteBand());

    private static readonly double[] MagnesitePressures = [1.0e3, 1.0e4, 1.0e5, 1.0e6];

    private static readonly double[] MagnesiteDistances = [100.0, 20.0, 1.0];

    private static readonly double[] ScanPressures = [1.0e3, 1.0e5, 1.0e7];

    private static readonly double[] ScanTemperatures = [300.0, 500.0, 800.0, 1200.0, 1600.0, 2000.0, 2500.0, 3000.0];

    private static readonly (string Name, string[] Elements, double[] Ratio, double[] Temperatures)[] BinarySystems =
    [
        ("binary-ko2", ["K", "O"], [1.0, 2.0], [300.0, 500.0, 700.0, 800.0, 900.0, 1000.0, 1200.0, 1500.0]),
        ("binary-nao2", ["NA", "O"], [1.0, 2.0], [300.0, 500.0, 700.0, 800.0, 900.0, 1000.0, 1200.0, 1500.0]),
        ("binary-h2o", ["H", "O"], [2.0, 1.0], [300.0, 350.0, 370.0, 400.0]),
        ("binary-al2o3", ["AL", "O"], [2.0, 3.0], [300.0, 1000.0, 2000.0, 2500.0, 3000.0]),
        ("binary-kcl", ["K", "CL"], [1.0, 1.0], [300.0, 700.0, 1000.0, 1200.0, 1500.0]),
    ];

    /// <summary>The system's ratio with the last element's moles raised by <paramref name="excess"/>.</summary>
    private static TraceGasCase Shifted(string name, string[] elements, double[] ratio, double excess, double pressure, double temperature)
    {
        var shifted = (double[])ratio.Clone();
        shifted[^1] *= 1 + excess;
        return new TraceGasCase(Name(name, excess, pressure, temperature), elements, shifted, pressure, temperature);
    }

    /// <summary>The plateau temperature of a univariant system at a pressure, found once by the tree's own bisection.</summary>
    private static double Plateau(UnivariantSystem system, double pressure)
    {
        lock (Plateaus)
        {
            if (!Plateaus.TryGetValue((system.Name, pressure), out var temperature))
            {
                temperature = UnivariantRig.Of(system, pressure).Temperature;
                Plateaus[(system.Name, pressure)] = temperature;
            }

            return temperature;
        }
    }

    private static string Name(string family, params double[] numbers) =>
        family + "|" + string.Join("|", numbers.Select(n => n.ToString("R", CultureInfo.InvariantCulture)));
}
