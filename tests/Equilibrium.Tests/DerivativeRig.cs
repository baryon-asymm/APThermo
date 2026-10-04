using APThermo.Equilibrium.StateRecord;
using APThermo.Thermo;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// A table over CPU-accelerator buffers and a made-up composition for the derivative system to be solved at: the moles of the
/// gases a caller names, the condensed species it puts in the solution in its order, and the species functions at one
/// temperature. The derivative system reads the composition and the enthalpies only, so no iteration stands behind it.
/// </summary>
internal sealed class DerivativeRig : IDisposable
{
    private readonly SpeciesTableBuffers _tableBuffers;
    private readonly List<IDisposable> _buffers = [];
    private readonly int _condensedCount;
    private readonly double _temperature;

    private DerivativeRig(SpeciesTable table, SpeciesTableBuffers tableBuffers, EquilibriumScratch scratch, EquilibriumResult result,
                          int condensedCount, double temperature)
    {
        Table = table;
        _tableBuffers = tableBuffers;
        Scratch = scratch;
        Result = result;
        _condensedCount = condensedCount;
        _temperature = temperature;
    }

    public SpeciesTable Table { get; }

    public EquilibriumScratch Scratch { get; }

    public EquilibriumResult Result { get; }

    /// <summary>The table's view over the rig's buffers.</summary>
    public SpeciesTableView View => _tableBuffers.View;

    /// <summary>The element multipliers' tied element after <see cref="SolveTied"/>, or none.</summary>
    public ElementTie Tie { get; private set; }

    /// <summary>
    /// The rig over <paramref name="table"/> at <paramref name="temperature"/>: every element present, the gases at the moles
    /// <paramref name="gasMoles"/> gives for their index, and the condensed species <paramref name="inSolution"/> in that order at
    /// their moles.
    /// </summary>
    public static DerivativeRig Of(SpeciesTable table, double temperature, Func<int, double> gasMoles, (string Name, double Moles)[] inSolution)
    {
        var accelerator = CpuFixture.Shared.Accelerator;
        var tableBuffers = SpeciesTableBuffers.Upload(accelerator, table);
        var view = tableBuffers.View;
        var doubles = accelerator.Allocate1D<double>(ScratchLayout.DoublesPerCase(view.SpeciesCount, view.ElementCount));
        var ints = accelerator.Allocate1D<int>(ScratchLayout.IntsPerCase(view.SpeciesCount, view.ElementCount));
        var moles = accelerator.Allocate1D<double>(view.SpeciesCount);
        var multipliers = accelerator.Allocate1D<double>(view.ElementCount);
        var state = accelerator.Allocate1D<MixtureState>(1);
        var status = accelerator.Allocate1D<int>(1);
        var iterations = accelerator.Allocate1D<int>(1);
        doubles.MemSetToZero();
        ints.MemSetToZero();
        moles.MemSetToZero();
        var scratch = EquilibriumScratch.Slice(doubles.View, ints.View, view.SpeciesCount, view.ElementCount);
        var result = new EquilibriumResult(moles.View, multipliers.View, state.View, status.View, iterations.View);
        var rig = new DerivativeRig(table, tableBuffers, scratch, result, inSolution.Length, temperature);
        rig._buffers.AddRange([doubles, ints, moles, multipliers, state, status, iterations]);
        rig.Place(gasMoles, inSolution);
        return rig;
    }

    /// <summary>The derivative system of the composition, as <see cref="DerivativeSystem.Solve"/> solves it at the close.</summary>
    public Derivatives Solve()
    {
        var state = new IterationState { CondensedCount = _condensedCount, Temperature = _temperature };
        return DerivativeSystem.Solve(_tableBuffers.View, Scratch, Result, state, ScratchLayout.MaxUnknowns(Table.ElementCount), default);
    }

    /// <summary>The same through <see cref="TiedDerivatives.Solve"/>: the tie it found, if any, is <see cref="Tie"/>.</summary>
    public Derivatives SolveTied()
    {
        var state = new IterationState { CondensedCount = _condensedCount, Temperature = _temperature };
        var derivatives = TiedDerivatives.Solve(_tableBuffers.View, Scratch, Result, ref state, ScratchLayout.MaxUnknowns(Table.ElementCount), default);
        Tie = state.Tie;
        return derivatives;
    }

    public string[] NamesInSolution() =>
        [.. Enumerable.Range(0, _condensedCount).Select(c => Table.Species[Scratch.CondensedInSolution[c]])];

    public void Dispose()
    {
        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }

        _tableBuffers.Dispose();
    }

    private void Place(Func<int, double> gasMoles, (string Name, double Moles)[] inSolution)
    {
        var view = _tableBuffers.View;
        for (var i = 0; i < Table.ElementCount; i++)
        {
            Scratch.ElementActive[i] = 1;
        }

        for (var j = 0; j < Table.SpeciesCount; j++)
        {
            Scratch.HOverRT[j] = SpeciesFunctions.HOverRT(view, j, _temperature);
        }

        for (var j = 0; j < Table.GasCount; j++)
        {
            Result.Moles[j] = gasMoles(j);
        }

        for (var c = 0; c < inSolution.Length; c++)
        {
            var j = Table.IndexOf(inSolution[c].Name);
            Scratch.CondensedInSolution[c] = j;
            Result.Moles[j] = inSolution[c].Moles;
        }
    }
}
