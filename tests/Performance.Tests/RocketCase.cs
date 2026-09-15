using APThermo.Equilibrium;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Performance.Tests;

/// <summary>
/// One rocket case on an accelerator: its buffers, the context the node's stages take, and the solution read back. The whole
/// solve runs over it (<see cref="RocketHost"/>), and so does a single stage driven on its own.
/// </summary>
internal sealed class RocketCase : IDisposable
{
    private readonly List<IDisposable> _owned = [];
    private readonly MemoryBuffer1D<MixtureState, Stride1D.Dense> _stations;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _moles;
    private readonly MemoryBuffer1D<double, Stride1D.Dense> _multipliers;
    private readonly MemoryBuffer1D<PerformanceFigures, Stride1D.Dense> _figures;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _stationStatus;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _iterations;
    private readonly MemoryBuffer1D<int, Stride1D.Dense> _status;

    public RocketCase(Accelerator accelerator, SpeciesTable table, RocketInputs inputs)
    {
        Table = table;
        Inputs = inputs;
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var stationCount = RocketLayout.StationCount(inputs.ExitCount);
        var tableBuffers = Own(SpeciesTableBuffers.Upload(accelerator, table));
        var elements = Own(accelerator.Allocate1D(inputs.Mixture.ElementMoles));
        var exitValues = Own(accelerator.Allocate1D(inputs.Exits.Values.Length == 0 ? [0.0] : inputs.Exits.Values));
        var exitKinds = Own(accelerator.Allocate1D(inputs.Exits.Kinds.Length == 0 ? [0] : inputs.Exits.Kinds.Select(k => (int)k).ToArray()));
        var doubles = Own(accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(speciesCount, elementCount)));
        var ints = Own(accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(speciesCount, elementCount)));
        _stations = Own(accelerator.Allocate1D<MixtureState>(stationCount));
        _moles = Own(accelerator.Allocate1D<double>((long)stationCount * speciesCount));
        _multipliers = Own(accelerator.Allocate1D<double>((long)stationCount * elementCount));
        _figures = Own(accelerator.Allocate1D<PerformanceFigures>(stationCount));
        _stationStatus = Own(accelerator.Allocate1D<int>(stationCount));
        _iterations = Own(accelerator.Allocate1D<int>(stationCount));
        _status = Own(accelerator.Allocate1D<int>(1));
        _moles.MemSetToZero();
        _stations.MemSetToZero();

        var problem = new RocketProblem(
            chamberPressure: inputs.ChamberPressure, reactantEnthalpy: inputs.Mixture.ReactantEnthalpy, temperatureEstimate: 0.0,
            flow: inputs.Flow, elementMoles: elements.View,
            exitValues: exitValues.View.SubView(0, inputs.ExitCount), exitKinds: exitKinds.View.SubView(0, inputs.ExitCount));
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, speciesCount, elementCount);
        var result = new RocketResult(
            stations: _stations.View, moles: _moles.View, multipliers: _multipliers.View, figures: _figures.View,
            stationStatus: _stationStatus.View, iterations: _iterations.View, status: _status.View);
        Context = new RocketContext(tableBuffers.View, problem, scratch, result);
    }

    public SpeciesTable Table { get; }

    public RocketInputs Inputs { get; }

    /// <summary>The views of this case, as every stage of the node takes them.</summary>
    public RocketContext Context { get; }

    /// <summary>Downloads what the solver has written so far.</summary>
    public RocketSolution Read()
    {
        var outcome = new RocketOutcome(_stations.GetAsArray1D(), _moles.GetAsArray1D(), _multipliers.GetAsArray1D(), _figures.GetAsArray1D(),
                                        _stationStatus.GetAsArray1D().Select(s => (CaseStatus)s).ToArray(), _iterations.GetAsArray1D());
        return new RocketSolution(Table, Inputs, outcome, (CaseStatus)_status.GetAsArray1D()[0]);
    }

    public void Dispose()
    {
        for (var k = _owned.Count - 1; k >= 0; k--)
        {
            _owned[k].Dispose();
        }

        _owned.Clear();
    }

    private T Own<T>(T buffer)
        where T : IDisposable
    {
        _owned.Add(buffer);
        return buffer;
    }
}
