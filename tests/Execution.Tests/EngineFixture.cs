using APThermo.Data;
using APThermo.Fixtures;

namespace APThermo.Execution.Tests;

/// <summary>
/// The committed databases, a CPU engine, and the CUDA engine of the reference machine (null when CUDA is forbidden),
/// shared by every test class of this node. Held as the single <see cref="Shared"/> instance rather than through
/// <c>ICollectionFixture&lt;T&gt;</c>: xUnit requires a class fixture's consuming constructor to be the class's
/// single public constructor, which would force this internal-only helper public for no reason a consumer outside
/// this node has (CA1515, confirmed empirically: a public fixture with a <c>[CollectionDefinition]</c> wrapper fails
/// both CA1515 and CA1711 on a clean build of this project, regardless of whether a consuming class injects it). The
/// classes that shared one <c>EngineFixture</c> through the "engine" <c>ICollectionFixture&lt;T&gt;</c> collection at
/// 8375261 still run sequentially relative to each other, tagged <c>[Collection(CollectionName)]</c>, a string-named
/// xUnit collection that needs no public <c>[CollectionDefinition]</c> class to exist: this keeps the CUDA throughput
/// tripwire from measuring beside concurrent CPU load on the same shared engine.
/// </summary>
internal sealed class EngineFixture : IDisposable
{
    /// <summary>The xUnit collection name every consuming class of this node is tagged with.</summary>
    public const string CollectionName = "engine";

    /// <summary>
    /// The committed databases, loaded once for the theory data (member data is static) and for the fixture. Declared
    /// before <see cref="Shared"/>: static field and property initializers run in declaration order, and the
    /// constructor <see cref="Shared"/> triggers reads this property.
    /// </summary>
    public static SpeciesDatabase SharedDatabase { get; } =
        SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp"), Path.Combine(RepositoryPaths.Data, "trans.inp"));

    /// <summary>The one instance every test class of this node shares.</summary>
    public static readonly EngineFixture Shared = new();

    private readonly Lazy<Engine?> _cuda;
    private readonly Lazy<SweepRun> _sweep;

    public EngineFixture()
    {
        Database = SharedDatabase;
        Tolerances = ToleranceTable.Load();
        Cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        _cuda = new Lazy<Engine?>(() => Engine.CudaForbidden ? null : Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda }));
        _sweep = new Lazy<SweepRun>(() => SweepRun.Run(this, SweepRun.LongRunningCases));
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    /// <summary>The committed thermodynamic and transport database.</summary>
    public SpeciesDatabase Database { get; }

    /// <summary>The tolerance table this node compares against.</summary>
    public ToleranceTable Tolerances { get; }

    /// <summary>The CPU accelerator engine.</summary>
    internal Engine Cpu { get; }

    /// <summary>The CUDA engine, created on first use; null when the environment forbids CUDA.</summary>
    internal Engine? Cuda => _cuda.Value;

    /// <summary>The 100 000-case sweep on both accelerators, run once for the long-running tests.</summary>
    internal SweepRun Sweep => _sweep.Value;

    /// <summary>
    /// The CUDA engine for a CUDA-category test. When CUDA is forbidden by the environment the test verifies the refusal instead and
    /// returns null, so that the full suite passes under APTHERMO_NO_CUDA=1; on a machine without CUDA the creation fails loudly.
    /// </summary>
    internal Engine? RequireCuda()
    {
        if (!Engine.CudaForbidden)
        {
            return Cuda!;
        }

        var refused = Assert.Throws<AcceleratorUnavailableException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda }));
        Assert.Contains(EngineOptions.NoCudaVariable, refused.Message, StringComparison.Ordinal);
        return null;
    }

    public void Dispose()
    {
        if (_cuda.IsValueCreated)
        {
            _cuda.Value?.Dispose();
        }

        Cpu.Dispose();
    }
}
