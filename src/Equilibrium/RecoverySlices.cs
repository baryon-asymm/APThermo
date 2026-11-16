using APThermo.Thermo;
using ILGPU;

namespace APThermo.Equilibrium;

/// <summary>Recovery-owned bracket ends and exact best-candidate moles, multipliers and isolated typed state.</summary>
internal readonly struct RecoverySlices(ArrayView<double> bracketEnds, ArrayView<double> bestMoles,
                                        ArrayView<double> bestMultipliers, ArrayView<MixtureState> bestState, ArrayView<int> bestCondensedSet)
{
    public readonly ArrayView<double> BracketEnds = bracketEnds;
    public readonly ArrayView<double> BestMoles = bestMoles;
    public readonly ArrayView<double> BestMultipliers = bestMultipliers;
    public readonly ArrayView<MixtureState> BestState = bestState;
    public readonly ArrayView<int> BestCondensedSet = bestCondensedSet;

    /// <summary>Keep the existing double regions unchanged and attach one separately allocated state per case.</summary>
    public static RecoverySlices Slice(ArrayView<double> doubles, long offset, int speciesCount, int elementCount,
                                       ArrayView<MixtureState> bestState, ArrayView<int> bestCondensedSet) =>
        new(bracketEnds: speciesCount > 0 ? doubles.SubView(offset, 2L * speciesCount) : default,
            bestMoles: speciesCount > 0 ? doubles.SubView(offset + 2L * speciesCount, speciesCount) : default,
            bestMultipliers: elementCount > 0 ? doubles.SubView(offset + 3L * speciesCount, elementCount) : default,
            bestState: bestState, bestCondensedSet: bestCondensedSet);
}