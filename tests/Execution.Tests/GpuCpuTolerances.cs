using System.Reflection;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>The one table of CUDA against the CPU accelerator, with a derivation per entry (Execution.Tests BOOT.md).</summary>
public static class GpuCpuTolerances
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

    public static readonly IReadOnlyDictionary<string, (double Relative, string Derivation)> Entries = new Dictionary<string, (double, string)>(StringComparer.Ordinal)
    {
        ["temperature"] = (1e-10, "The Newton iteration is polished until its corrections are below 1e-11, the rounding floor of the linear solves; a few ULP in exp and log move the converged iterate by 1e-12, and one polish step more on one accelerator by 1e-11 (measured 3.4e-13 and 1.4e-11 in the sweep)."),
        ["moleFraction"] = (1e-10, "As temperature, for mole fractions not below 1e-8 at stations where both accelerators stopped after the same number of Newton steps (measured 9e-12); below 1e-8 the logarithms of trace species are not converged to the same digits."),
        ["state"] = (1e-9, "Every other field of MixtureState: sums of species functions and solutions of the derivative systems over the same converged composition; the sums accumulate the ULP differences of the functions."),
        ["figures"] = (1e-9, "Performance figures: velocities from enthalpy differences and the throat and area-ratio iterations, which stop at 1e-10 on both accelerators."),
        ["transport"] = (1e-9, "Transport figures: sums of exp fits and the reaction systems, over the composition given to both accelerators alike."),
        ["functions"] = (1e-10, "Cp/R, H/RT and S/R of one species, on the larger of 1 and the value: an eight-term polynomial in T plus log T (and pow for a non-integer exponent), whose terms cancel by up to five decades in the condensed fits (the liquid-water coefficients reach 1e8; measured 1.7e-11 on its Cp/R at 298.15 K) and cancel to zero by construction in H/RT of a reference element at 298.15 K, where a relative bound would be meaningless."),
    };

    /// <summary>
    /// Mole fractions below this are not compared: their logarithms are not converged to the same digits. Not an entry of this
    /// node's own table (F-TF-05, BOOT.md): two paths of the tree's own code reaching the same mole fraction is not a comparison
    /// with the reference, so the floor is the fixtures node's <c>moleFractionFloor</c>, which the front door tests node's
    /// union-batch comparison also reads.
    /// </summary>
    public static double MoleFractionFloor(ToleranceTable tolerances) => tolerances.For("moleFractionFloor").Absolute;

    /// <summary>
    /// The mole-fraction tolerance of a station, by whether both accelerators stopped after the same number of Newton steps. The
    /// different-step tier is not an entry of this node's own table either (F-TF-05, BOOT.md): it is the fixtures node's
    /// <c>polishThresholdRelative</c>, for the same reason as the floor.
    /// </summary>
    public static double MoleFractionRelative(ToleranceTable tolerances, bool sameSteps) =>
        sameSteps ? Entries["moleFraction"].Relative : tolerances.For("polishThresholdRelative").Relative;

    /// <summary>The tolerance of a field of one of the result structs.</summary>
    public static double RelativeFor(Type owner, string field) =>
        owner == typeof(MixtureState) && field == nameof(MixtureState.Temperature) ? Entries["temperature"].Relative
        : owner == typeof(MixtureState) ? Entries["state"].Relative
        : owner == typeof(PerformanceFigures) ? Entries["figures"].Relative
        : owner == typeof(TransportFigures) ? Entries["transport"].Relative
        : throw new ArgumentException($"no tolerance for {owner.Name}", nameof(owner));

    public static bool Matches(double relative, double expected, double actual) =>
        Math.Abs(expected - actual) <= relative * Math.Abs(expected);

    /// <summary>The mismatches between two structs of the same type, field by field; ints must be equal, doubles within the field's tolerance.</summary>
    public static IEnumerable<string> Compare<T>(T cpu, T cuda, string label, Action<string, double>? record = null) where T : struct
    {
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var a = field.GetValue(cpu)!;
            var b = field.GetValue(cuda)!;
            if (a is double x && b is double y)
            {
                var relative = RelativeFor(typeof(T), field.Name);
                var deviation = x == 0.0 ? Math.Abs(y) : Math.Abs(x - y) / Math.Abs(x);
                record?.Invoke($"{typeof(T).Name}.{field.Name}", deviation);
                if (!Matches(relative, x, y))
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

    public static long UlpDistance(double a, double b)
    {
        if (a == b)
        {
            return 0;
        }

        if (double.IsNaN(a) || double.IsNaN(b) || double.IsInfinity(a) || double.IsInfinity(b))
        {
            return long.MaxValue;
        }

        var x = BitConverter.DoubleToInt64Bits(a);
        var y = BitConverter.DoubleToInt64Bits(b);
        if ((x < 0) != (y < 0))
        {
            return long.MaxValue;
        }

        return Math.Abs(x - y);
    }
}
