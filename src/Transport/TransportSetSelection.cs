namespace APThermo.Transport;

/// <summary>
/// Stages 4 and 5 of the evaluation: which species take part. The components seed the set, then every gaseous species of the
/// case above a threshold descending by decades joins it, until the set carries the coverage fraction of the gaseous moles, is
/// full, or the threshold falls under the cutoff (<c>BOOT.md</c>, Constraints). The thresholds count the gases of the case —
/// those whose every element is active — and not the gases of the table, which may hold the species of other cases.
/// Scratch: reads <c>RowActive</c>, <c>Component</c> and the "in the set" bit of <c>Mark</c>, cleared by
/// <see cref="TransportComponents"/>; writes <c>IndexList</c> and that bit.
/// </summary>
internal static class TransportSetSelection
{
    private const int MaxSpecies = TransportSolver.MaxSpecies;

    /// <summary>
    /// Chooses the set from the moles of the station, whose gaseous part is <paramref name="gasMoles"/>, and returns the moles
    /// the set carries; the size of the set and whether it was capped go into <paramref name="figures"/>. A zero return, or a
    /// set of no species, is <see cref="Thermo.CaseStatus.NoTransportData"/> for the caller.
    /// </summary>
    internal static double Select(in StationInputs inputs, double gasMoles, ref TransportFigures figures)
    {
        var caseGasCount = CaseGasCount(in inputs);
        var nm = 0;
        var total = Seed(in inputs, ref nm);
        var capped = Passes(in inputs, gasMoles, caseGasCount, ref nm, ref total);
        figures.SpeciesCount = nm;
        figures.Capped = capped;
        return total;
    }

    /// <summary>
    /// The gaseous species the case can form: the reference's ng. The reference's ng counts the gaseous products of the
    /// problem; a table shared by cases with different elements holds more, and they must not lower the thresholds.
    /// </summary>
    private static int CaseGasCount(in StationInputs inputs)
    {
        var count = 0;
        for (var j = 0; j < inputs.Species.GasCount; j++)
        {
            if (TransportComponents.OfCase(in inputs, j))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The components of the active rows, each taken once; returns the moles they carry.</summary>
    private static double Seed(in StationInputs inputs, ref int nm)
    {
        var scratch = inputs.Scratch;
        var total = 0.0;
        for (var i = 0; i < inputs.Species.ElementCount; i++)
        {
            var j = scratch.Component[i];
            if (scratch.RowActive[i] == 0 || j < 0 || (scratch.Mark[j] & 2) != 0)
            {
                continue;
            }

            if (nm >= MaxSpecies)
            {
                break;
            }

            scratch.IndexList[nm++] = j;
            scratch.Mark[j] |= 2;
            total += inputs.Moles[j];
        }

        return total;
    }

    /// <summary>The decade passes to the coverage, the cap or the cutoff; returns 1 when a species was refused as the set was full.</summary>
    private static int Passes(in StationInputs inputs, double gasMoles, int caseGasCount, ref int nm, ref double total)
    {
        var coverage = TransportSolver.CoverageFraction * gasMoles * (1.0 - TransportSolver.CoverageTolerance);
        var threshold = gasMoles / caseGasCount;
        var capped = 0;
        for (var pass = 0; pass < caseGasCount; pass++)
        {
            if (total >= coverage)
            {
                break;
            }

            if (nm >= MaxSpecies)
            {
                capped = 1;
                break;
            }

            threshold /= 10.0;
            if (TakeAbove(in inputs, threshold, ref nm, ref total) == 1)
            {
                capped = 1;
            }

            if (threshold < TransportSolver.CutoffFraction * gasMoles)
            {
                break;
            }
        }

        return capped;
    }

    /// <summary>One pass: every gaseous species not yet in the set whose moles are not below the threshold, in table order.</summary>
    private static int TakeAbove(in StationInputs inputs, double threshold, ref int nm, ref double total)
    {
        var scratch = inputs.Scratch;
        for (var j = 0; j < inputs.Species.GasCount; j++)
        {
            if (inputs.Moles[j] < threshold || (scratch.Mark[j] & 2) != 0)
            {
                continue;
            }

            if (nm >= MaxSpecies)
            {
                return 1;
            }

            total += inputs.Moles[j];
            scratch.IndexList[nm++] = j;
            scratch.Mark[j] |= 2;
        }

        return 0;
    }
}
