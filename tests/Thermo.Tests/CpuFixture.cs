using APThermo.Data;
using APThermo.Fixtures;
using APThermo.Harness;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Thermo.Tests;

/// <summary>
/// The shared CPU host (the harness node's context, accelerator, database and tolerance table), plus what only this
/// node needs from it: the rounding bound of a self-consistency check and the table-upload helper. The harness holds
/// no tolerance (its own BOOT.md), so <see cref="RoundingBound"/> lives here, the node that owns the comparison.
/// </summary>
public sealed class CpuFixture : IDisposable
{
    private readonly CpuHost _host = new();

    /// <summary>
    /// The rounding floor for a comparison that is not against an independent reference but against a fit's own
    /// continuity: a polynomial evaluated on either side of its own interval bound (<c>IntervalRuleTests</c>), or a
    /// fit's enthalpy increment at 298.15 K, which the record's own H(298.15) − H(0) convention makes exactly zero
    /// (<c>JanafTests</c>). Double arithmetic over the polynomial's own terms leaves a residual at this scale; a
    /// real defect (a wrong exponent, a misread coefficient) is orders of magnitude larger. One named constant
    /// instead of the literal <c>1e-9</c> typed twice (the clean-code review's F-TK-10).
    /// </summary>
    public const double RoundingBound = 1e-9;

    public Context Context => _host.Context;

    public Accelerator Accelerator => _host.Accelerator;

    public SpeciesDatabase Database => _host.Database;

    public ToleranceTable Tolerances => _host.Tolerances;

    /// <summary>A table of the given species with exactly the elements their formulas use, uploaded to the CPU accelerator.</summary>
    internal SpeciesTableBuffers Upload(params string[] species)
    {
        var elements = species.SelectMany(s => Database[s].Formula.Select(p => p.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return SpeciesTableBuffers.Upload(Accelerator, SpeciesTable.Build(Database, elements, species));
    }

    public void Dispose() => _host.Dispose();
}
