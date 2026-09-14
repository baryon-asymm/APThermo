using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>
/// The reference's documented caveats <see cref="ReferenceComparison"/> applies (Fixtures BOOT.md; this node's BOOT.md,
/// invariants): which station outputs are neither state, performance nor transport fields, which fields carry transport, which
/// are skipped at a frozen station or under a singular or defective reference. The trace threshold below which the reference
/// lists a mole fraction as trace is the fixtures node's own <c>ToleranceTable.MoleFractionField</c>, not retyped here.
/// </summary>
internal static class ReferenceCaveats
{
    /// <summary>Station outputs that are neither state, performance nor transport fields.</summary>
    public static readonly IReadOnlySet<string> Labels = new HashSet<string>(["station", "index", "frozen", "moleFractions", "converged"], StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, Func<TransportFigures, double>> TransportFields = new Dictionary<string, Func<TransportFigures, double>>(StringComparer.Ordinal)
    {
        ["viscosity"] = f => f.Viscosity,
        ["frozenConductivity"] = f => f.FrozenConductivity,
        ["reactingConductivity"] = f => f.ReactingConductivity,
        ["frozenPrandtl"] = f => f.FrozenPrandtl,
        ["reactingPrandtl"] = f => f.ReactingPrandtl,
    };

    /// <summary>Fields that carry the reaction term: skipped where the reference's value is known to be defective (Fixtures BOOT.md).</summary>
    public static readonly IReadOnlySet<string> ReactingFields = new HashSet<string>(["reactingConductivity", "reactingPrandtl"], StringComparer.Ordinal);

    /// <summary>The reference computes no Cv at a frozen station (Fixtures BOOT.md).</summary>
    public static readonly IReadOnlySet<string> NotAtFrozenStations = new HashSet<string>(["cvFrozen", "cvEquilibrium"], StringComparer.Ordinal);

    /// <summary>The second-order response: skipped where the reference's derivative matrix was singular (the singular-tp defect, Fixtures BOOT.md).</summary>
    public static readonly IReadOnlySet<string> SecondOrderFields = new HashSet<string>(["cpEquilibrium", "cvEquilibrium", "gammaS", "dlnVdlnT", "dlnVdlnP", "soundSpeed"], StringComparer.Ordinal);

    /// <summary>With transport on, the reference's frozen heat capacities of a station with condensed species are those of the transport set (Fixtures BOOT.md).</summary>
    public static readonly IReadOnlySet<string> GasPhaseWithTransport = new HashSet<string>(["cpFrozen", "cvFrozen"], StringComparer.Ordinal);

    /// <summary>
    /// The signature of the singular-tp defect (Fixtures BOOT.md): a tp assigned exactly at a bound two records of one
    /// substance share makes the reference's derivative matrix singular, and it prints its convention (cp_eq = 0,
    /// gamma_s = -1/dlnVdlnP) instead of derivatives. No real tp state has a zero equilibrium heat capacity.
    /// </summary>
    public static bool SingularTp(CeaCase c) => c.Kind == "tp" && c.Outputs.GetProperty("cpEquilibrium").GetDouble() == 0.0;
}
