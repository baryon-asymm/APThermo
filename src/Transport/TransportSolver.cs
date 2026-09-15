using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

namespace AerospacePropellantThermodynamics.Transport;

/// <summary>
/// Mixture viscosity, frozen and reacting thermal conductivity and Prandtl numbers of one station from the composition and the
/// temperature: NASA RP-1311 Part I, chapter 5, in the form the reference program (NASA CEA) applies it, see BOOT.md.
/// Kernel-compatible: no allocation, no exception, the listed Math functions only.
/// </summary>
public static class TransportSolver
{
    /// <summary>The largest transport set (the reference's limit).</summary>
    public const int MaxSpecies = TransportLayout.MaxSpecies;

    /// <summary>The set is complete when it carries this fraction of the gaseous moles (the reference's test).</summary>
    public const double CoverageFraction = 0.999999999;

    /// <summary>Relative slack on the coverage test.</summary>
    public const double CoverageTolerance = 1e-6;

    /// <summary>The selection stops descending below this fraction of the gaseous moles.</summary>
    public const double CutoffFraction = 1e-11;

    /// <summary>A species of the set below this mole fraction leaves the reaction set and the pair sums.</summary>
    public const double TraceFraction = 1e-10;

    /// <summary>Entries of the reduced basis below this magnitude are cleaned to zero.</summary>
    public const double BasisCleaningThreshold = 1e-5;

    /// <summary>Reaction coefficients below this magnitude are zero.</summary>
    public const double ReactionCoefficientThreshold = 1e-6;

    /// <summary>A trace species is eliminated through reactions where its coefficient exceeds this magnitude.</summary>
    public const double EliminationThreshold = 1e-5;

    /// <summary>A stoichiometric coefficient below this magnitude counts as zero in the component search.</summary>
    public const double StoichiometryThreshold = 1e-10;

    /// <summary>Tolerance on "one atom" in the search for the monatomic species of an element.</summary>
    public const double UnitCountTolerance = 1e-8;

    /// <summary>A* of the diffusion coefficients behind the reaction term (RP-1311, section 5.2.2).</summary>
    public const double AStar = 1.1;

    /// <summary>Boltzmann constant, J/K, the reference's value.</summary>
    public const double Boltzmann = 1.3806580e-23;

    /// <summary>Avogadro constant per kmol, the reference's value.</summary>
    public const double Avogadro = 6.0221367e26;

    /// <summary>Collision diameter of the hard-sphere estimate for a species without data, m.</summary>
    public const double CollisionDiameter = 1e-10;

    /// <summary>The row stride of every MaxSpecies × MaxSpecies matrix of the scratch; the stages share it.</summary>
    internal const int Stride = MaxSpecies;

    /// <summary>
    /// Evaluates the station: the transport set is chosen from <paramref name="moles"/> as the reference does, the species without
    /// data are estimated, and <paramref name="figures"/>[0] receives the result. Returns <see cref="CaseStatus.Ok"/>,
    /// <see cref="CaseStatus.InvalidInput"/> (non-positive temperature, negative or NaN moles, a table that does not match),
    /// <see cref="CaseStatus.NoTransportData"/> (no gaseous moles) or <see cref="CaseStatus.SingularMatrix"/> (the reaction
    /// system could not be solved; the frozen figures are written, the reacting ones equal them).
    /// </summary>
    public static CaseStatus Evaluate(in SpeciesTableView species, in TransportTableView transport, double temperature,
                                      ArrayView<double> moles, in TransportScratch scratch, ArrayView<TransportFigures> figures)
    {
        var inputs = new StationInputs(in species, in transport, in scratch, moles, temperature);
        return StationEvaluation.Run(in inputs, figures);
    }

    /// <summary>The fit of a run for the temperature, the reference's rule: the last fit whose predecessor's upper bound lies below T, else the first.</summary>
    public static int FitOf(in TransportTableView transport, int start, int count, double temperature)
    {
        var index = 0;
        for (var i = 1; i < count; i++)
        {
            var previousHigh = transport.Fits[(start + i - 1) * TransportTable.FitStride + 1];
            var high = transport.Fits[(start + i) * TransportTable.FitStride + 1];
            if (high > 0.0 && temperature > previousHigh)
            {
                index = i;
            }
        }

        return start + index;
    }

    /// <summary>exp(A ln T + B/T + C/T² + D) of one fit, SI.</summary>
    public static double FitValue(in TransportTableView transport, int fit, double temperature)
    {
        var offset = fit * TransportTable.FitStride;
        return Math.Exp(transport.Fits[offset + 2] * Math.Log(temperature) + transport.Fits[offset + 3] / temperature
                        + transport.Fits[offset + 4] / (temperature * temperature) + transport.Fits[offset + 5]);
    }

    /// <summary>The viscosity fit of a species at the temperature, Pa·s; zero without data.</summary>
    public static double PureViscosity(in TransportTableView transport, int species, double temperature)
    {
        var count = transport.ViscosityCount[species];
        return count > 0 ? FitValue(in transport, FitOf(in transport, transport.ViscosityStart[species], count, temperature), temperature) : 0.0;
    }

    /// <summary>The conductivity fit of a species at the temperature, W/(m·K); zero without data.</summary>
    public static double PureConductivity(in TransportTableView transport, int species, double temperature)
    {
        var count = transport.ConductivityCount[species];
        return count > 0 ? FitValue(in transport, FitOf(in transport, transport.ConductivityStart[species], count, temperature), temperature) : 0.0;
    }

    /// <summary>The interaction viscosity fit of a pair at the temperature, Pa·s.</summary>
    public static double PairViscosity(in TransportTableView transport, int pair, double temperature) =>
        FitValue(in transport, FitOf(in transport, transport.PairStart[pair], transport.PairCount[pair], temperature), temperature);
}
