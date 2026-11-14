using ILGPU;

namespace APThermo.Equilibrium;

/// <summary>Recovery-owned double views: two bracket ends and the exact best-candidate moles and multipliers.</summary>
internal readonly struct RecoverySlices(ArrayView<double> bracketEnds, ArrayView<double> bestMoles, ArrayView<double> bestMultipliers)
{
    public readonly ArrayView<double> BracketEnds = bracketEnds;
    public readonly ArrayView<double> BestMoles = bestMoles;
    public readonly ArrayView<double> BestMultipliers = bestMultipliers;

    /// <summary>Append the best-candidate bank after the existing bracket ends; empty views stay default at a buffer end.</summary>
    public static RecoverySlices Slice(ArrayView<double> doubles, long offset, int speciesCount, int elementCount) =>
        new(bracketEnds: speciesCount > 0 ? doubles.SubView(offset, 2L * speciesCount) : default,
            bestMoles: speciesCount > 0 ? doubles.SubView(offset + 2L * speciesCount, speciesCount) : default,
            bestMultipliers: elementCount > 0 ? doubles.SubView(offset + 3L * speciesCount, elementCount) : default);
}
