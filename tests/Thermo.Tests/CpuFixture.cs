using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Fixtures;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace AerospacePropellantThermodynamics.Thermo.Tests;

/// <summary>One ILGPU context with the CPU accelerator, the loaded database and the tolerance table, shared by a test class.</summary>
public sealed class CpuFixture : IDisposable
{
    public CpuFixture()
    {
        Context = Context.Create(builder => builder.CPU());
        Accelerator = Context.CreateCPUAccelerator(0);
        Database = SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"));
        Tolerances = ToleranceTable.Load();
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

    public Context Context { get; }

    public Accelerator Accelerator { get; }

    public SpeciesDatabase Database { get; }

    public ToleranceTable Tolerances { get; }

    /// <summary>A table of the given species with exactly the elements their formulas use, uploaded to the CPU accelerator.</summary>
    public SpeciesTableBuffers Upload(params string[] species)
    {
        var elements = species.SelectMany(s => Database[s].Formula.Select(p => p.Symbol)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return SpeciesTableBuffers.Upload(Accelerator, SpeciesTable.Build(Database, elements, species));
    }

    public void Dispose()
    {
        Accelerator.Dispose();
        Context.Dispose();
    }
}
