using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

namespace AerospacePropellantThermodynamics.Transport;

/// <summary>
/// The composition root of the evaluation of one station: the order of the stages and the status, and no formula of its own
/// (<c>BOOT.md</c>, ## Structure, where it is named as the composition root the root's coupling rule allows above ten). The
/// stages run in this order even where two of them look independent, because it is the order the figures were recorded in.
/// Scratch: none of its own; each stage's summary names the slots it reads and writes.
/// </summary>
internal static class StationEvaluation
{
    /// <summary>Runs the stages over one station and writes <paramref name="figures"/>[0] on every status.</summary>
    internal static CaseStatus Run(in StationInputs inputs, ArrayView<TransportFigures> figures)
    {
        var result = default(TransportFigures);
        figures[0] = result;
        var inputStatus = TransportInput.Validate(in inputs, out var gasMoles);
        if (inputStatus != CaseStatus.Ok)
        {
            return inputStatus;
        }

        TransportComponents.Select(in inputs);
        var total = TransportSetSelection.Select(in inputs, gasMoles, ref result);
        var nm = result.SpeciesCount;
        if (nm == 0 || total <= 0.0)
        {
            return CaseStatus.NoTransportData;
        }

        SetSpeciesProperties.Fits(in inputs, nm, total, ref result);
        ReactionBasis.Reduce(in inputs, nm);
        var nr = ReactionSet.Build(in inputs, nm, ref result);
        SetSpeciesProperties.Estimates(in inputs, nm);
        var mixture = MixtureRules.Evaluate(in inputs, nm);
        var reaction = ReactionTerms.Evaluate(in inputs, nm, nr);
        SetProperties.Fill(in inputs, nm, in mixture, in reaction, ref result);
        figures[0] = result;
        return reaction.Status;
    }
}
