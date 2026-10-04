using System.Reflection;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>The one table of CUDA against the CPU accelerator, with a derivation per entry (Execution.Tests BOOT.md).</summary>
internal static class GpuCpuTolerances
{
    /// <summary>Largest ULP distance between the probe kernel's math functions on CUDA and on the CPU (3 measured on the reference machine).</summary>
    public const long MathUlp = 4;

    /// <summary>
    /// Largest share of the stations of a batch at which the two accelerators may stop after different numbers of Newton steps: the
    /// polish stops on a threshold (1e-11 on the corrections) at the rounding floor of the linear solves, so a last-ULP difference
    /// flips the decision now and then (91 of 400 000 stations in the sweep on the reference machine, 0.023 %); a share above this
    /// would mean the accelerators disagree before the floor, and the second tier of the mole-fraction tolerance would hide it.
    /// </summary>
    public const double DifferentStepShare = 1e-3;

    /// <summary>
    /// The relative step of the central differences that measure <c>D_ij = ∂ ln x_j / ∂ ln b_i</c> for the balance-remnant correction:
    /// large enough that the difference of two logarithms of 1e-16 relative accuracy keeps eight digits (the step's own truncation
    /// error is of the order of h times the curvature), small enough to stay inside one active set of condensed species.
    /// </summary>
    public const double SensitivityStep = 1e-8;

    /// <summary>
    /// The largest share of its own derivative at which the two one-sided differences of a species may disagree for the balance-remnant
    /// correction to be used however large its κ: half of the smallest kink measured (0.12, the threshold-flip cases), so that a curvature
    /// share <c>κ h</c> never admits a kink (Execution.Tests BOOT.md, 2026-10-04).
    /// </summary>
    public const double SensitivityGuardCap = 6e-2;

    /// <summary>
    /// How many times the CPU accelerator's own response to rounding-level noise a mole fraction may differ by between the accelerators
    /// where that response is above the tier: the largest ratio of the other accelerator's deviation to the response's maximum over 16
    /// replicates was 0.92 (153 quantities of the 28 plateau states, median 0.21, CUDA on the reference machine, 2026-10-04), and 2 leaves
    /// a factor of 2.2 above it.
    /// </summary>
    public const double NoiseFactor = 2.0;

    public static readonly IReadOnlyDictionary<string, (double Relative, string Derivation)> Entries = new Dictionary<string, (double, string)>(StringComparer.Ordinal)
    {
        ["temperature"] = (1e-10, "The Newton iteration is polished until its corrections are below 1e-11, the rounding floor of the linear solves; a few ULP in exp and log move the converged iterate by 1e-12, and one polish step more on one accelerator by 1e-11 (measured 3.4e-13 and 1.4e-11 in the sweep)."),
        ["moleFraction"] = (1e-10, "As temperature, for mole fractions not below 1e-8 at stations where both accelerators stopped after the same number of Newton steps (measured 9e-12); below 1e-8 the logarithms of trace species are not converged to the same digits."),
        ["condensedMoleFraction"] = (1e-9, "A condensed species' mole fraction not below the floor, whatever the Newton counts: an amount on a phase plateau is ill-conditioned, d ln x / d ln p of 59 to 290 measured at the throat fixtures' plateau stations (LiOH(L), AL2O3(a)) against 2 to 6 for gases, and the accelerators' throat pressures differ by up to 8.4e-13 in ln p, so 290 x 8.4e-13 = 2.4e-10; the solve's own floor for such an amount is about 1e-10 (1.31e-10 under injected noise on the CPU). Measured on CUDA on 2026-10-03: LiOH(L) 9.5e-11, AL2O3(a) 4.0e-11, every other condensed value at most 1.1e-12; 1e-9 is the polish-threshold tier's value, a decade above the worst."),
        ["balanceResidual"] = (1e-13, "The relative residual of the element balance, ρ_i = Σ_j a_ij n_j / b_i − 1 summed in double-double, of every compared station of every family on both accelerators: twice the worst measured over the rocket, throat and equilibrium fixture families, the sweep included (4.9e-14, the threshold-flip KClO4 family on CUDA, 2026-10-03). A species the balance sets as a small difference of large amounts carries this residual times its κ, so a defect that breaks conservation shows here before it shows in a mole fraction."),
        ["sensitivityDisagreement"] = (2e-2, "The guard of the balance-remnant correction (BOOT.md): a species is corrected only where the two one-sided differences of ln x_j with respect to ln b_i, D+ and D-, differ by at most this share of the species' largest |D_ij| (and of 1 where that is smaller). On a smooth solution D+ - D- is the curvature times the step, about 0.5 kappa h of the largest |D|, so the H2 remnant of three-element_rp1311-example1 (kappa 4.2e5, kappa h 4.2e-3) measured 2.1e-3, the worst of the smooth rows; at a kink of the solution (a condensed species at its threshold, the threshold-flip KClO4 and NaClO4 cases) the two differ by a share of the derivative itself, 0.12 to 0.60 measured. The bound lies between the two, 9.5 times above the worst smooth row and 5.8 times below the smallest kink; a remnant whose kappa h reached it would be dropped, which compares it uncorrected and fails loudly rather than corrects it wrongly."),
        ["state"] = (1e-9, "Every other field of MixtureState: sums of species functions and solutions of the derivative systems over the same converged composition; the sums accumulate the ULP differences of the functions."),
        ["figures"] = (1e-9, "Performance figures: velocities from enthalpy differences, the throat, which decides at |u²/a² − 1| of at most 1e-8 and then takes exactly two more momentum steps, and the area-ratio iteration, which stops at 1e-10, on both accelerators."),
        ["transport"] = (1e-9, "Transport figures: sums of exp fits and the reaction systems, over the composition given to both accelerators alike."),
        ["functions"] = (1e-10, "Cp/R, H/RT and S/R of one species, on the larger of 1 and the value: an eight-term polynomial in T plus log T (and pow for a non-integer exponent), whose terms cancel by up to five decades in the condensed fits (the liquid-water coefficients reach 1e8; measured 1.7e-11 on its Cp/R at 298.15 K) and cancel to zero by construction in H/RT of a reference element at 298.15 K, where a relative bound would be meaningless."),
    };

    /// <summary>
    /// Mole fractions below this are not compared: their logarithms are not converged to the same digits. Not an entry of this
    /// node's own table (F-TF-05, BOOT.md): two paths of the tree's own code reaching the same mole fraction is not a comparison
    /// with the reference, so the floor is the fixtures node's <c>moleFractionFloor</c>, which the front door tests node's
    /// union-batch comparison also reads.
    /// </summary>
    public static double MoleFractionFloor(ToleranceTable tolerances)
    {
        ArgumentNullException.ThrowIfNull(tolerances);
        return tolerances.For("moleFractionFloor").Absolute;
    }

    /// <summary>
    /// The mole-fraction tolerance of a species at a station: the condensed tier for a condensed species whatever the Newton counts,
    /// otherwise by whether both accelerators stopped after the same number of Newton steps. The different-step tier is not an entry
    /// of this node's own table either (F-TF-05, BOOT.md): it is the fixtures node's <c>polishThresholdRelative</c>, for the same
    /// reason as the floor.
    /// </summary>
    public static double MoleFractionRelative(ToleranceTable tolerances, bool sameSteps, bool condensed)
    {
        ArgumentNullException.ThrowIfNull(tolerances);
        return condensed ? Entries["condensedMoleFraction"].Relative
            : sameSteps ? Entries["moleFraction"].Relative
            : tolerances.For("polishThresholdRelative").Relative;
    }

    /// <summary>
    /// The bound on the disagreement of the one-sided differences of a species under which its balance-remnant correction is used: the
    /// entry's, or the curvature share <c>κ h</c> where that is larger (a smooth remnant's two differences disagree by one half of it times
    /// the species' largest derivative, measured 2.1e-3 at κ 4.2e5 and 1.6e-2 to 2.2e-2 at κ 3.2e6 to 4.3e6), capped by <see cref="SensitivityGuardCap"/>.
    /// </summary>
    public static double SensitivityGuard(double kappa) =>
        Math.Min(SensitivityGuardCap, Math.Max(Entries["sensitivityDisagreement"].Relative, kappa * SensitivityStep));

    /// <summary>The tolerance of a field of one of the result structs.</summary>
    public static double RelativeFor(Type owner, string field)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return owner == typeof(MixtureState) && field == nameof(MixtureState.Temperature) ? Entries["temperature"].Relative
            : owner == typeof(MixtureState) ? Entries["state"].Relative
            : owner == typeof(PerformanceFigures) ? Entries["figures"].Relative
            : owner == typeof(TransportFigures) ? Entries["transport"].Relative
            : throw new ArgumentException($"no tolerance for {owner.Name}", nameof(owner));
    }

    public static bool Matches(double relative, double expected, double actual) =>
        Math.Abs(expected - actual) <= relative * Math.Abs(expected);

    /// <summary>
    /// The mismatches between two structs of the same type, field by field; ints must be equal, doubles within the field's tolerance, or
    /// accepted by <paramref name="accepted"/> (field name, the two values) where the caller has a derived bound for that field.
    /// </summary>
    public static IEnumerable<string> Compare<T>(T cpu, T cuda, string label, Action<string, double>? record = null, Func<string, double, double, bool>? accepted = null) where T : struct
    {
        foreach (var field in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var a = field.GetValue(cpu)!;
            var b = field.GetValue(cuda)!;
            if (a is double x && b is double y)
            {
                var relative = RelativeFor(typeof(T), field.Name);
                var deviation = x == 0.0 ? Math.Abs(y) : Math.Abs(x - y) / Math.Abs(x);
                record?.Invoke($"{typeof(T).Name}.{field.Name}", deviation);
                if (!Matches(relative, x, y) && accepted?.Invoke(field.Name, x, y) != true)
                {
                    yield return $"{label} {field.Name}: cpu {x:R}, cuda {y:R}";
                }
            }
            else if (!a.Equals(b))
            {
                yield return $"{label} {field.Name}: cpu {a}, cuda {b}";
            }
        }
    }

    /// <summary>
    /// NaN counts as equal to NaN, and ±0 and ±∞ are compared exactly, with the sign (2026-09-27, the guards audit's F11): the
    /// probe's inputs now include them, and a plain <c>a == b</c> would call +0 and -0 equal, while <see cref="double.IsNaN"/>
    /// would call two NaNs (produced deterministically by the same code on both accelerators) an infinite mismatch.
    /// </summary>
    public static long UlpDistance(double a, double b)
    {
        if (double.IsNaN(a) && double.IsNaN(b))
        {
            return 0;
        }

        if (double.IsNaN(a) || double.IsNaN(b))
        {
            return long.MaxValue;
        }

        if (a == b)
        {
            return double.IsNegative(a) == double.IsNegative(b) ? 0 : long.MaxValue;
        }

        if (double.IsInfinity(a) || double.IsInfinity(b))
        {
            return long.MaxValue;
        }

        var x = BitConverter.DoubleToInt64Bits(a);
        var y = BitConverter.DoubleToInt64Bits(b);
        return (x < 0) != (y < 0) ? long.MaxValue : Math.Abs(x - y);
    }
}
