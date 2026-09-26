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
/// Held by each consuming test class as a <c>private static readonly</c> field, not through
/// <c>IClassFixture&lt;T&gt;</c>: xUnit requires a class fixture's consuming constructor to be the class's single
/// public constructor, which would force this internal-only helper public for no reason a consumer outside this
/// node has (CA1515). Each class's own instance registers its <see cref="Dispose"/> on
/// <see cref="AppDomain.ProcessExit"/>, the same as the single-instance fixtures of the other test nodes.
///
/// ⚠ 2026-09-25: this comment stood "the CPU accelerator holds no resource a process exit does not already
/// reclaim, so nothing is lost by not calling Dispose at a class's end", and no instance registered a disposal.
/// Consistency with the other test nodes' fixtures, which all register on <see cref="AppDomain.ProcessExit"/>
/// regardless of whether the OS would reclaim the resource anyway, asked for the same discipline here.
/// </summary>
internal sealed class CpuFixture : IDisposable
{
    private readonly CpuHost _host = new();

    public CpuFixture()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

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
