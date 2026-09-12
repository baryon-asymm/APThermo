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

    private const int Stride = MaxSpecies;

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
        var result = default(TransportFigures);
        figures[0] = result;
        var speciesCount = species.SpeciesCount;
        var gasCount = species.GasCount;
        var elementCount = species.ElementCount;
        if (!(temperature > 0.0) || speciesCount <= 0 || gasCount <= 0 || elementCount <= 0 || transport.SpeciesCount != speciesCount)
        {
            return CaseStatus.InvalidInput;
        }

        var gasMoles = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            var nj = moles[j];
            if (!(nj >= 0.0))
            {
                return CaseStatus.InvalidInput;
            }

            if (j < gasCount)
            {
                gasMoles += nj;
            }
        }

        if (gasMoles <= 0.0)
        {
            return CaseStatus.NoTransportData;
        }

        // Active element rows and their default species: the monatomic gas, else the first gas containing the element.
        for (var i = 0; i < elementCount; i++)
        {
            scratch.RowActive[i] = 0;
            scratch.Default[i] = -1;
            scratch.Component[i] = -1;
            scratch.RowTaken[i] = 0;
        }

        for (var j = 0; j < speciesCount; j++)
        {
            if (moles[j] <= 0.0)
            {
                continue;
            }

            for (var i = 0; i < elementCount; i++)
            {
                if (species.Stoichiometry[i * speciesCount + j] != 0.0)
                {
                    scratch.RowActive[i] = 1;
                }
            }
        }

        var activeRows = 0;
        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            activeRows++;
            for (var j = 0; j < gasCount; j++)
            {
                if (Math.Abs(Math.Abs(species.Stoichiometry[i * speciesCount + j]) - 1.0) < UnitCountTolerance
                    && Math.Abs(AtomCount(in species, j) - 1.0) < UnitCountTolerance)
                {
                    scratch.Default[i] = j;
                    break;
                }
            }

            if (scratch.Default[i] < 0)
            {
                for (var j = 0; j < gasCount; j++)
                {
                    if (Math.Abs(species.Stoichiometry[i * speciesCount + j]) > StoichiometryThreshold)
                    {
                        scratch.Default[i] = j;
                        break;
                    }
                }
            }

            scratch.Component[i] = scratch.Default[i];
        }

        // Components: the gaseous species in decreasing moles, each given the first free row it can serve.
        for (var j = 0; j < speciesCount; j++)
        {
            scratch.Mark[j] = 0;
        }

        var assigned = 0;
        while (assigned < activeRows)
        {
            var candidate = -1;
            var best = 0.0;
            for (var j = 0; j < gasCount; j++)
            {
                if ((scratch.Mark[j] & 1) == 0 && moles[j] > best)
                {
                    best = moles[j];
                    candidate = j;
                }
            }

            if (candidate < 0)
            {
                break;
            }

            for (var i = 0; i < elementCount; i++)
            {
                if (scratch.RowActive[i] == 0 || scratch.RowTaken[i] == 1
                    || species.Stoichiometry[i * speciesCount + candidate] <= StoichiometryThreshold)
                {
                    continue;
                }

                var accept = true;
                for (var l = 0; l < elementCount && accept; l++)
                {
                    if (scratch.RowTaken[l] == 0 || scratch.Component[l] < 0)
                    {
                        continue;
                    }

                    accept = !SameColumn(in species, in scratch, candidate, scratch.Component[l]);
                }

                for (var k = 0; k < elementCount && accept; k++)
                {
                    if (k == i || scratch.RowActive[k] == 0 || scratch.Default[k] < 0)
                    {
                        continue;
                    }

                    var other = scratch.Default[k];
                    var determinant = species.Stoichiometry[i * speciesCount + candidate] * species.Stoichiometry[k * speciesCount + other]
                                      - species.Stoichiometry[i * speciesCount + other] * species.Stoichiometry[k * speciesCount + candidate];
                    accept = Math.Abs(determinant) > StoichiometryThreshold;
                }

                if (!accept)
                {
                    continue;
                }

                scratch.Component[i] = candidate;
                scratch.RowTaken[i] = 1;
                assigned++;
                break;
            }

            scratch.Mark[candidate] |= 1;
        }

        // The transport set: the components, then every gas above a threshold descending by decades until the set covers the gas.
        var nm = 0;
        var total = 0.0;
        for (var i = 0; i < elementCount; i++)
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
            total += moles[j];
        }

        var coverage = CoverageFraction * gasMoles * (1.0 - CoverageTolerance);
        var threshold = gasMoles / gasCount;
        var capped = 0;
        for (var pass = 0; pass < gasCount; pass++)
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
            for (var j = 0; j < gasCount; j++)
            {
                if (moles[j] < threshold || (scratch.Mark[j] & 2) != 0)
                {
                    continue;
                }

                if (nm >= MaxSpecies)
                {
                    capped = 1;
                    break;
                }

                total += moles[j];
                scratch.IndexList[nm++] = j;
                scratch.Mark[j] |= 2;
            }

            if (threshold < CutoffFraction * gasMoles)
            {
                break;
            }
        }

        if (nm == 0 || total <= 0.0)
        {
            return CaseStatus.NoTransportData;
        }

        // Per-species data of the set.
        var estimatedCount = 0;
        var estimatedFraction = 0.0;
        for (var a = 0; a < nm; a++)
        {
            var j = scratch.IndexList[a];
            scratch.Xs[a] = moles[j] / total;
            scratch.Cp[a] = SpeciesFunctions.CpOverR(in species, j, temperature);
            scratch.H[a] = SpeciesFunctions.HOverRT(in species, j, temperature);
            scratch.Cond[a] = transport.ConductivityCount[j] > 0 ? PureConductivity(in transport, j, temperature) : 0.0;
            if (transport.ViscosityCount[j] > 0)
            {
                scratch.Eta[a * Stride + a] = PureViscosity(in transport, j, temperature);
            }
            else
            {
                scratch.Eta[a * Stride + a] = 0.0;
                estimatedCount++;
                estimatedFraction += scratch.Xs[a];
            }

            for (var b = 0; b < a; b++)
            {
                var pair = transport.PairIndex[j * speciesCount + scratch.IndexList[b]];
                var value = pair >= 0 ? PairViscosity(in transport, pair, temperature) : 0.0;
                scratch.Eta[a * Stride + b] = value;
                scratch.Eta[b * Stride + a] = value;
            }
        }

        // The component basis reduced over the columns of the set (row operations act on every column alike).
        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            for (var a = 0; a < nm; a++)
            {
                scratch.Basis[i * Stride + a] = species.Stoichiometry[i * speciesCount + scratch.IndexList[a]];
            }
        }

        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0 || scratch.Component[i] < 0)
            {
                continue;
            }

            var column = LocalIndex(in scratch, nm, scratch.Component[i]);
            if (column < 0)
            {
                continue;
            }

            var pivot = scratch.Basis[i * Stride + column];
            if (pivot == 0.0)
            {
                continue;
            }

            if (pivot != 1.0)
            {
                for (var a = 0; a < nm; a++)
                {
                    scratch.Basis[i * Stride + a] /= pivot;
                }
            }

            for (var k = 0; k < elementCount; k++)
            {
                if (k == i || scratch.RowActive[k] == 0)
                {
                    continue;
                }

                var factor = scratch.Basis[k * Stride + column];
                if (factor == 0.0)
                {
                    continue;
                }

                for (var a = 0; a < nm; a++)
                {
                    var value = scratch.Basis[k * Stride + a] - scratch.Basis[i * Stride + a] * factor;
                    scratch.Basis[k * Stride + a] = Math.Abs(value) < BasisCleaningThreshold ? 0.0 : value;
                }
            }
        }

        // Reactions: every non-component of the set formed from the components.
        var ncomp = 0;
        for (var a = 0; a < nm; a++)
        {
            scratch.IsComponent[a] = 0;
        }

        for (var i = 0; i < elementCount; i++)
        {
            if (scratch.RowActive[i] == 0 || scratch.Component[i] < 0)
            {
                continue;
            }

            var a = LocalIndex(in scratch, nm, scratch.Component[i]);
            if (a < 0 || scratch.IsComponent[a] == 1)
            {
                continue;
            }

            scratch.CompLocal[ncomp] = a;
            scratch.CompRow[ncomp] = i;
            scratch.IsComponent[a] = 1;
            ncomp++;
        }

        var nr = 0;
        if (ncomp > 0 && ncomp < nm)
        {
            for (var a = 0; a < nm; a++)
            {
                if (scratch.IsComponent[a] == 1)
                {
                    continue;
                }

                for (var b = 0; b < nm; b++)
                {
                    scratch.Alpha[nr * Stride + b] = 0.0;
                }

                scratch.Alpha[nr * Stride + a] = -1.0;
                for (var k = 0; k < ncomp; k++)
                {
                    scratch.Alpha[nr * Stride + scratch.CompLocal[k]] = scratch.Basis[scratch.CompRow[k] * Stride + a];
                }

                nr++;
            }
        }

        // A trace species leaves the reaction set: eliminated from every reaction through the first one containing it, which is dropped.
        var traceEliminations = 0;
        for (var a = 0; a < nm; a++)
        {
            if (scratch.Xs[a] >= TraceFraction)
            {
                continue;
            }

            var pivotRow = -1;
            for (var r = 0; r < nr; r++)
            {
                var coefficient = scratch.Alpha[r * Stride + a];
                if (Math.Abs(coefficient) <= EliminationThreshold)
                {
                    continue;
                }

                if (pivotRow < 0)
                {
                    pivotRow = r;
                    for (var b = 0; b < nm; b++)
                    {
                        scratch.Stx[b] = scratch.Alpha[r * Stride + b] / coefficient;
                    }
                }
                else
                {
                    for (var b = 0; b < nm; b++)
                    {
                        scratch.Alpha[r * Stride + b] = scratch.Alpha[r * Stride + b] / coefficient - scratch.Stx[b];
                    }
                }
            }

            if (pivotRow < 0)
            {
                continue;
            }

            for (var r = pivotRow; r < nr - 1; r++)
            {
                for (var b = 0; b < nm; b++)
                {
                    scratch.Alpha[r * Stride + b] = scratch.Alpha[(r + 1) * Stride + b];
                }
            }

            nr--;
            traceEliminations++;
        }

        // Estimates for species and pairs without data: hard spheres with the reference's collision integral, modified Eucken.
        for (var a = 0; a < nm; a++)
        {
            var molarMass = species.MolarMass[scratch.IndexList[a]];
            if (scratch.Eta[a * Stride + a] == 0.0)
            {
                var omega = Math.Max(1.0, Math.Log(50.0 * Math.Pow(molarMass, 4.6) / Math.Pow(temperature, 1.4)));
                scratch.Eta[a * Stride + a] = 0.3125 * Math.Sqrt(Boltzmann * molarMass * temperature / (Math.PI * Avogadro))
                                              / (CollisionDiameter * CollisionDiameter * omega);
            }

            if (scratch.Cond[a] == 0.0)
            {
                scratch.Cond[a] = scratch.Eta[a * Stride + a] * (PhysicalConstants.R / molarMass) * (3.75 + 1.32 * (scratch.Cp[a] - 2.5));
            }
        }

        for (var a = 0; a < nm - 1; a++)
        {
            var massA = species.MolarMass[scratch.IndexList[a]];
            var etaA = scratch.Eta[a * Stride + a];
            for (var b = a + 1; b < nm; b++)
            {
                if (scratch.Eta[a * Stride + b] != 0.0)
                {
                    continue;
                }

                var massB = species.MolarMass[scratch.IndexList[b]];
                var ratio = Math.Sqrt(massB / massA);
                var value = 4.0 * Math.Sqrt(2.0) * etaA * Math.Sqrt(massB / (massA + massB));
                var root = 1.0 + Math.Sqrt(ratio * etaA / scratch.Eta[b * Stride + b]);
                value /= root * root;
                scratch.Eta[a * Stride + b] = value;
                scratch.Eta[b * Stride + a] = value;
            }
        }

        // Viscosity and frozen conductivity: equations (5.3) to (5.7).
        var viscosity = 0.0;
        var frozenConductivity = 0.0;
        for (var a = 0; a < nm; a++)
        {
            var massA = species.MolarMass[scratch.IndexList[a]];
            var etaA = scratch.Eta[a * Stride + a];
            var sumViscosity = scratch.Xs[a];
            var sumConductivity = scratch.Xs[a];
            for (var b = 0; b < nm; b++)
            {
                if (b == a)
                {
                    continue;
                }

                var massB = species.MolarMass[scratch.IndexList[b]];
                var phi = 2.0 * massB * etaA / (scratch.Eta[a * Stride + b] * (massA + massB));
                var psi = phi * (1.0 + 2.41 * (massA - massB) * (massA - 0.142 * massB) / ((massA + massB) * (massA + massB)));
                sumViscosity += phi * scratch.Xs[b];
                sumConductivity += psi * scratch.Xs[b];
            }

            viscosity += etaA * scratch.Xs[a] / sumViscosity;
            frozenConductivity += scratch.Cond[a] * scratch.Xs[a] / sumConductivity;
        }

        // Reaction contributions: Butler and Brokaw over the pairs of the set, equations (5.8) to (5.12).
        var status = CaseStatus.Ok;
        var reactionHeatCapacity = 0.0;
        var reactionConductivity = 0.0;
        if (nr > 0)
        {
            for (var r = 0; r < nr; r++)
            {
                var deltaH = 0.0;
                for (var b = 0; b < nm; b++)
                {
                    if (Math.Abs(scratch.Alpha[r * Stride + b]) < ReactionCoefficientThreshold)
                    {
                        scratch.Alpha[r * Stride + b] = 0.0;
                    }

                    deltaH += scratch.Alpha[r * Stride + b] * scratch.H[b];
                }

                scratch.DeltaH[r] = deltaH;
                for (var c = 0; c < nr; c++)
                {
                    scratch.Matrix[r * Stride + c] = 0.0;
                    scratch.MatrixReacting[r * Stride + c] = 0.0;
                }
            }

            for (var k = 0; k < nm - 1; k++)
            {
                if (scratch.Xs[k] < TraceFraction)
                {
                    continue;
                }

                var massK = species.MolarMass[scratch.IndexList[k]];
                for (var m = k + 1; m < nm; m++)
                {
                    if (scratch.Xs[m] < TraceFraction)
                    {
                        continue;
                    }

                    var massM = species.MolarMass[scratch.IndexList[m]];
                    var rtOverPD = 5.0 * massK * massM / (3.0 * AStar * scratch.Eta[k * Stride + m] * (massK + massM));
                    var inverse = 1.0 / (scratch.Xs[k] * scratch.Xs[m]);
                    for (var r = 0; r < nr; r++)
                    {
                        var alphaK = scratch.Alpha[r * Stride + k];
                        var alphaM = scratch.Alpha[r * Stride + m];
                        scratch.Stx[r] = alphaK == 0.0 && alphaM == 0.0 ? 0.0 : scratch.Xs[m] * alphaK - scratch.Xs[k] * alphaM;
                    }

                    for (var r = 0; r < nr; r++)
                    {
                        var stxR = scratch.Stx[r];
                        if (stxR == 0.0)
                        {
                            continue;
                        }

                        for (var c = r; c < nr; c++)
                        {
                            var term = stxR * scratch.Stx[c] * inverse;
                            scratch.Matrix[r * Stride + c] += term;
                            scratch.MatrixReacting[r * Stride + c] += term * rtOverPD;
                        }
                    }
                }
            }

            for (var r = 0; r < nr; r++)
            {
                for (var c = 0; c < r; c++)
                {
                    scratch.Matrix[r * Stride + c] = scratch.Matrix[c * Stride + r];
                    scratch.MatrixReacting[r * Stride + c] = scratch.MatrixReacting[c * Stride + r];
                }

                scratch.Rhs[r] = scratch.DeltaH[r];
            }

            if (DenseSolver.Solve(scratch.Matrix, scratch.Rhs, scratch.RowScale, nr, Stride))
            {
                for (var r = 0; r < nr; r++)
                {
                    reactionHeatCapacity += scratch.DeltaH[r] * scratch.Rhs[r];
                    scratch.Rhs[r] = scratch.DeltaH[r];
                }

                reactionHeatCapacity *= PhysicalConstants.R;
                if (DenseSolver.Solve(scratch.MatrixReacting, scratch.Rhs, scratch.RowScale, nr, Stride))
                {
                    for (var r = 0; r < nr; r++)
                    {
                        reactionConductivity += scratch.DeltaH[r] * scratch.Rhs[r];
                    }

                    reactionConductivity *= PhysicalConstants.R;
                }
                else
                {
                    status = CaseStatus.SingularMatrix;
                    reactionConductivity = 0.0;
                }
            }
            else
            {
                status = CaseStatus.SingularMatrix;
                reactionHeatCapacity = 0.0;
            }
        }

        var massOfSet = 0.0;
        var cpOfSet = 0.0;
        for (var a = 0; a < nm; a++)
        {
            massOfSet += scratch.Xs[a] * species.MolarMass[scratch.IndexList[a]];
            cpOfSet += scratch.Xs[a] * scratch.Cp[a];
        }

        var frozenHeatCapacity = PhysicalConstants.R * cpOfSet / massOfSet;
        var equilibriumHeatCapacity = frozenHeatCapacity + reactionHeatCapacity / massOfSet;
        var reactingConductivity = frozenConductivity + reactionConductivity;
        result.Viscosity = viscosity;
        result.FrozenConductivity = frozenConductivity;
        result.ReactingConductivity = reactingConductivity;
        result.FrozenPrandtl = viscosity * frozenHeatCapacity / frozenConductivity;
        result.ReactingPrandtl = viscosity * equilibriumHeatCapacity / reactingConductivity;
        result.FrozenHeatCapacity = frozenHeatCapacity;
        result.EquilibriumHeatCapacity = equilibriumHeatCapacity;
        result.EstimatedMoleFraction = estimatedFraction;
        result.SpeciesCount = nm;
        result.ReactionCount = nr;
        result.EstimatedSpeciesCount = estimatedCount;
        result.TraceEliminations = traceEliminations;
        result.Capped = capped;
        figures[0] = result;
        return status;
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

    private static double AtomCount(in SpeciesTableView species, int j)
    {
        var sum = 0.0;
        for (var i = 0; i < species.ElementCount; i++)
        {
            sum += Math.Abs(species.Stoichiometry[i * species.SpeciesCount + j]);
        }

        return sum;
    }

    private static bool SameColumn(in SpeciesTableView species, in TransportScratch scratch, int first, int second)
    {
        for (var i = 0; i < species.ElementCount; i++)
        {
            if (scratch.RowActive[i] == 0)
            {
                continue;
            }

            if (species.Stoichiometry[i * species.SpeciesCount + first] != species.Stoichiometry[i * species.SpeciesCount + second])
            {
                return false;
            }
        }

        return true;
    }

    private static int LocalIndex(in TransportScratch scratch, int nm, int speciesIndex)
    {
        for (var a = 0; a < nm; a++)
        {
            if (scratch.IndexList[a] == speciesIndex)
            {
                return a;
            }
        }

        return -1;
    }
}
