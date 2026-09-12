using AerospacePropellantThermodynamics.Thermo;
using ILGPU;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// The equilibrium composition of one case by minimization of the Gibbs energy: the reduced Newton–Raphson iteration of
/// NASA RP-1311 Part I (Gordon and McBride, 1994), chapters 2 and 3, with the derivatives of section 2.5. Kernel-compatible:
/// static, no allocation, no exceptions; every input, output and scratch area is a view given by the caller.
/// </summary>
/// <remarks>
/// Unknowns of the reduced system: the Lagrange multipliers π_i of the active elements, the mole-number corrections Δn_j of the
/// condensed species in the solution, Δln n (n = gaseous kmol per kg), and Δln T for hp and sp. Gaseous corrections follow
/// from equation (2.18). The control factor λ of equations (3.1)–(3.3) limits every step; the tests of (3.5) and (3.6) decide
/// convergence, after which a few further Newton steps polish the solution to machine precision. Condensed species are
/// added one at a time by the test of equation (3.7) and removed when their mole number turns negative or the temperature
/// leaves their range (section 3.4 and 3.5).
/// </remarks>
public static class EquilibriumSolver
{
    /// <summary>−ln(1e-8): gaseous species below this mole fraction are held at zero in the sums but keep their logarithms.</summary>
    public const double TraceThreshold = 18.420681;

    /// <summary>−ln(1e-4), the bound of equation (3.2) on the growth of a small species in one step.</summary>
    private const double SmallSpeciesBound = 9.2103404;

    /// <summary>Newton steps allowed after the last change of the condensed set.</summary>
    public const int MaxNewtonSteps = 50;

    /// <summary>Changes of the condensed set allowed per case.</summary>
    public const int MaxCondensedSetChanges = 10;

    private const double CorrectionTest = 0.5e-5;      // equation (3.5)
    private const double BalanceTest = 1.0e-6;         // equation (3.6a), relative to the largest abundance
    private const double TemperatureTest = 1.0e-4;     // equation (3.6b)
    private const double PolishTest = 1.0e-11;         // the steps after the report's tests, until the corrections are this small
    private const int MaxPolishSteps = 6;
    private const double DefaultTemperatureEstimate = 3800.0;
    private const double MinTemperature = 100.0;
    private const double MaxTemperature = 20000.0;
    private const double StandardPressure = 1.0e5;    // Pa; the thermodynamic data are for 1 bar
    private const double ResetMoles = 1.0e-6;          // section 3.6: the reset of vanished species on a singular matrix
    private const int MaxSingularResets = 2;
    private const double BalanceInvariant = 1.0e-12;   // the node's element-conservation invariant, on max(1, b_i)
    private const double PhaseTransitionWindow = 50.0; // K, section 3.5: closer than this to a transition, both phases are kept
    private const double FrozenTemperatureTest = 1.0e-10; // relative, on the Newton step of the frozen temperature

    /// <summary>Solves the tp, hp or sp problem. With <paramref name="useMolesAsEstimate"/> the result's moles (and the problem's temperature) are the initial estimate.</summary>
    public static void Solve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, bool useMolesAsEstimate)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        var elementCount = table.ElementCount;
        result.Iterations[0] = 0;
        result.Status[0] = (int)CaseStatus.InvalidInput;

        if (speciesCount <= 0 || elementCount <= 0 || !(problem.Pressure > 0.0) || elementCount > TableLimits.MaxElements)
        {
            return;
        }

        if (problem.Kind == ProblemKind.AssignedTemperaturePressure && !(problem.Temperature > 0.0))
        {
            return;
        }

        // Element and species masks: an absent element removes every species that contains it.
        var anyElement = false;
        for (var i = 0; i < elementCount; i++)
        {
            var b = problem.ElementMoles[i];
            if (!(b >= 0.0))
            {
                return;
            }

            scratch.ElementActive[i] = b > 0.0 ? 1 : 0;
            anyElement |= b > 0.0;
        }

        if (!anyElement)
        {
            return;
        }

        var activeGases = 0;
        for (var j = 0; j < speciesCount; j++)
        {
            var active = 1;
            for (var i = 0; i < elementCount; i++)
            {
                if (table.Stoichiometry[i * speciesCount + j] != 0.0 && scratch.ElementActive[i] == 0)
                {
                    active = 0;
                }
            }

            scratch.SpeciesActive[j] = active;
            if (active == 1 && j < gasCount)
            {
                activeGases++;
            }
        }

        if (activeGases == 0)
        {
            return;
        }

        var isTp = problem.Kind == ProblemKind.AssignedTemperaturePressure;
        var isHp = problem.Kind == ProblemKind.AssignedEnthalpyPressure;
        var logPressure = Math.Log(problem.Pressure / StandardPressure);

        // Initial estimates (section 3.1) or the caller's.
        var temperature = isTp ? problem.Temperature : (problem.Temperature > 0.0 ? problem.Temperature : DefaultTemperatureEstimate);
        var condensedCount = 0;
        for (var c = 0; c < ScratchLayout.MaxCondensedInSolution; c++)
        {
            scratch.CondensedInSolution[c] = -1;
        }

        double logN;
        if (useMolesAsEstimate)
        {
            var estimate = 0.0;
            for (var j = 0; j < gasCount; j++)
            {
                if (scratch.SpeciesActive[j] == 1 && result.Moles[j] > 0.0)
                {
                    estimate += result.Moles[j];
                }
            }

            if (!(estimate > 0.0))
            {
                estimate = 0.1;
            }

            logN = Math.Log(estimate);
            for (var j = 0; j < gasCount; j++)
            {
                scratch.LogMoles[j] = scratch.SpeciesActive[j] == 1 && result.Moles[j] > 0.0
                    ? Math.Log(result.Moles[j])
                    : logN - TraceThreshold - 1.0;
            }

            for (var j = gasCount; j < speciesCount; j++)
            {
                if (scratch.SpeciesActive[j] == 1 && result.Moles[j] > 0.0 && condensedCount < ScratchLayout.MaxCondensedInSolution)
                {
                    scratch.CondensedInSolution[condensedCount++] = j;
                }
                else
                {
                    result.Moles[j] = 0.0;
                }
            }
        }
        else
        {
            logN = Math.Log(0.1);
            var each = Math.Log(0.1 / activeGases);
            for (var j = 0; j < gasCount; j++)
            {
                scratch.LogMoles[j] = each;
            }

            for (var j = gasCount; j < speciesCount; j++)
            {
                result.Moles[j] = 0.0;
            }
        }

        for (var i = 0; i < elementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        var unknownStride = ScratchLayout.MaxUnknowns(elementCount);
        var iterations = 0;
        var setChanges = 0;
        var status = CaseStatus.NotConverged;
        var functionsAt = -1.0;

        while (true)
        {
            var converged = false;
            var polishSteps = 0;
            var singularResets = 0;
            var steps = 0;
            while (steps < MaxNewtonSteps + MaxPolishSteps)
            {
                if (functionsAt != temperature)
                {
                    EvaluateFunctions(table, scratch, temperature);
                    functionsAt = temperature;
                }

                // Gaseous moles retained in the sums (section 3.2).
                var sumGas = 0.0;
                for (var j = 0; j < gasCount; j++)
                {
                    var retained = scratch.SpeciesActive[j] == 1 && scratch.LogMoles[j] - logN > -TraceThreshold;
                    result.Moles[j] = retained ? Math.Exp(scratch.LogMoles[j]) : 0.0;
                    sumGas += result.Moles[j];
                }

                var n = Math.Exp(logN);
                var unknowns = elementCount + condensedCount + 1 + (isTp ? 0 : 1);
                var nRow = elementCount + condensedCount;
                var tRow = nRow + 1;
                var hOverRT = 0.0;
                var sOverR = 0.0;
                for (var j = 0; j < speciesCount; j++)
                {
                    var nj = result.Moles[j];
                    if (nj == 0.0)
                    {
                        continue;
                    }

                    hOverRT += nj * scratch.HOverRT[j];
                    sOverR += j < gasCount
                        ? nj * (scratch.SOverR[j] - scratch.LogMoles[j] + logN - logPressure)
                        : nj * scratch.SOverR[j];
                }

                Assemble(table, problem, scratch, result, unknowns, unknownStride, condensedCount, isTp, isHp,
                         logN, logPressure, sumGas, n, hOverRT, sOverR, temperature);

                var solved = DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, unknowns, unknownStride);
                if (!solved)
                {
                    // Section 3.6: reset the vanished gaseous species, then drop the last condensed species, then give up.
                    if (singularResets < MaxSingularResets)
                    {
                        singularResets++;
                        for (var j = 0; j < gasCount; j++)
                        {
                            if (scratch.SpeciesActive[j] == 1 && result.Moles[j] == 0.0)
                            {
                                scratch.LogMoles[j] = Math.Log(ResetMoles);
                            }
                        }

                        continue;
                    }

                    if (condensedCount > 0)
                    {
                        condensedCount--;
                        result.Moles[scratch.CondensedInSolution[condensedCount]] = 0.0;
                        scratch.CondensedInSolution[condensedCount] = -1;
                        setChanges++;
                        singularResets = 0;
                        continue;
                    }

                    status = CaseStatus.SingularMatrix;
                    goto Finish;
                }

                steps++;
                iterations++;
                for (var i = 0; i < elementCount; i++)
                {
                    result.Multipliers[i] = scratch.RightHandSide[i];
                }

                var deltaLogN = scratch.RightHandSide[nRow];
                var deltaLogT = isTp ? 0.0 : scratch.RightHandSide[tRow];

                // Corrections of the gaseous species, equation (2.18), and the control factor λ, equations (3.1)–(3.3).
                var largest = Math.Max(5.0 * Math.Abs(deltaLogT), 5.0 * Math.Abs(deltaLogN));
                var lambda2 = double.MaxValue;
                for (var j = 0; j < gasCount; j++)
                {
                    if (scratch.SpeciesActive[j] == 0)
                    {
                        scratch.Corrections[j] = 0.0;
                        continue;
                    }

                    var sum = deltaLogN + scratch.HOverRT[j] * deltaLogT;
                    for (var i = 0; i < elementCount; i++)
                    {
                        sum += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
                    }

                    var mu = scratch.GOverRT[j] + scratch.LogMoles[j] - logN + logPressure;
                    var delta = sum - mu;
                    scratch.Corrections[j] = delta;
                    // Only growth is limited, as in CEA: a species on its way out may shrink by any factor in one step.
                    var logFraction = scratch.LogMoles[j] - logN;
                    if (delta <= 0.0)
                    {
                        continue;
                    }

                    if (logFraction > -TraceThreshold)
                    {
                        largest = Math.Max(largest, delta);
                    }
                    else if (delta - deltaLogN > 0.0)
                    {
                        lambda2 = Math.Min(lambda2, (-logFraction - SmallSpeciesBound) / (delta - deltaLogN));
                    }
                }

                var lambda = 1.0;
                if (largest > 0.0)
                {
                    lambda = Math.Min(lambda, 2.0 / largest);
                }

                lambda = Math.Min(lambda, lambda2);

                // Apply the corrections, equation (3.4).
                for (var j = 0; j < gasCount; j++)
                {
                    if (scratch.SpeciesActive[j] == 1)
                    {
                        scratch.LogMoles[j] += lambda * scratch.Corrections[j];
                    }
                }

                for (var c = 0; c < condensedCount; c++)
                {
                    result.Moles[scratch.CondensedInSolution[c]] += lambda * scratch.RightHandSide[elementCount + c];
                }

                logN += lambda * deltaLogN;
                if (!isTp)
                {
                    temperature = Math.Exp(Math.Log(temperature) + lambda * deltaLogT);
                    if (!(temperature >= MinTemperature) || !(temperature <= MaxTemperature))
                    {
                        status = CaseStatus.TemperatureOutOfRange;
                        goto Finish;
                    }
                }

                // Convergence tests, equations (3.5) and (3.6), on the undamped corrections.
                var total = sumGas;
                for (var c = 0; c < condensedCount; c++)
                {
                    total += result.Moles[scratch.CondensedInSolution[c]];
                }

                var worst = n * Math.Abs(deltaLogN) / total;
                for (var j = 0; j < gasCount; j++)
                {
                    if (result.Moles[j] > 0.0)
                    {
                        worst = Math.Max(worst, result.Moles[j] * Math.Abs(scratch.Corrections[j]) / total);
                    }
                }

                for (var c = 0; c < condensedCount; c++)
                {
                    worst = Math.Max(worst, Math.Abs(scratch.RightHandSide[elementCount + c]) / total);
                }

                var balanced = ElementBalanceWithin(table, problem, scratch, result, BalanceTest, true);
                var reportConverged = worst <= CorrectionTest && balanced && (isTp || Math.Abs(deltaLogT) <= TemperatureTest);
                if (reportConverged)
                {
                    // The report's tests passed; a few more steps bring the corrections to rounding level.
                    converged = true;
                    var polished = worst <= PolishTest && (isTp || Math.Abs(deltaLogT) <= PolishTest);
                    if (polished || polishSteps >= MaxPolishSteps)
                    {
                        break;
                    }

                    polishSteps++;
                }
                else if (steps >= MaxNewtonSteps)
                {
                    break;
                }
            }

            if (!converged)
            {
                status = CaseStatus.NotConverged;
                goto Finish;
            }

            // The final iterate: the functions at the final temperature and the retained mole numbers.
            if (functionsAt != temperature)
            {
                EvaluateFunctions(table, scratch, temperature);
                functionsAt = temperature;
            }

            for (var j = 0; j < gasCount; j++)
            {
                var retained = scratch.SpeciesActive[j] == 1 && scratch.LogMoles[j] - logN > -TraceThreshold;
                result.Moles[j] = retained ? Math.Exp(scratch.LogMoles[j]) : 0.0;
            }

            // Condensed species, one change per convergence (sections 3.4 and 3.5): a negative mole number removes the
            // species; a phase outside its temperature range is switched for the other phase of the same substance, or
            // joined by it within 50 K of the transition when the temperature is a variable; else the inclusion test.
            var changed = false;
            for (var c = 0; c < condensedCount && !changed; c++)
            {
                if (result.Moles[scratch.CondensedInSolution[c]] < 0.0)
                {
                    condensedCount = RemoveCondensed(scratch, result, condensedCount, c);
                    changed = true;
                }
            }

            for (var c = 0; c < condensedCount && !changed; c++)
            {
                var j = scratch.CondensedInSolution[c];
                if (SpeciesFunctions.IsInRange(table, j, temperature))
                {
                    continue;
                }

                var other = OtherPhaseInRange(table, scratch, condensedCount, j, temperature);
                var bothPhases = other >= 0 && !isTp && DistanceOutOfRange(table, j, temperature) < PhaseTransitionWindow
                                 && condensedCount < ScratchLayout.MaxCondensedInSolution;
                if (!bothPhases)
                {
                    condensedCount = RemoveCondensed(scratch, result, condensedCount, c);
                }

                if (other >= 0 && condensedCount < ScratchLayout.MaxCondensedInSolution)
                {
                    scratch.CondensedInSolution[condensedCount++] = other;
                    result.Moles[other] = 0.0;
                }

                changed = true;
            }

            if (!changed && condensedCount < ScratchLayout.MaxCondensedInSolution)
            {
                var best = -1;
                var bestGain = 0.0;
                for (var j = gasCount; j < speciesCount; j++)
                {
                    if (scratch.SpeciesActive[j] == 0 || InSolution(scratch, condensedCount, j) || !SpeciesFunctions.IsInRange(table, j, temperature))
                    {
                        continue;
                    }

                    var gain = -scratch.GOverRT[j];
                    for (var i = 0; i < elementCount; i++)
                    {
                        gain += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
                    }

                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        best = j;
                    }
                }

                if (best >= 0)
                {
                    scratch.CondensedInSolution[condensedCount++] = best;
                    result.Moles[best] = 0.0;
                    changed = true;
                }
            }

            if (!changed)
            {
                status = CaseStatus.Ok;
                break;
            }

            setChanges++;
            if (setChanges > MaxCondensedSetChanges)
            {
                status = CaseStatus.NotConverged;
                break;
            }
        }

        if (status == CaseStatus.Ok && !ElementBalanceWithin(table, problem, scratch, result, BalanceInvariant, false))
        {
            status = CaseStatus.NotConverged;
        }

        if (status == CaseStatus.Ok)
        {
            status = FinishState(table, problem, scratch, result, condensedCount, logN, logPressure, temperature, unknownStride);
        }

    Finish:
        result.Iterations[0] = iterations;
        result.Status[0] = (int)status;
    }

    /// <summary>
    /// With the composition fixed to the result's moles, solves for the temperature (hp, sp) or evaluates at the assigned one
    /// (tp), and writes the frozen properties: the derivatives of an ideal gas of fixed composition.
    /// </summary>
    public static void SolveFrozen(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        result.Iterations[0] = 0;
        result.Status[0] = (int)CaseStatus.InvalidInput;
        if (speciesCount <= 0 || !(problem.Pressure > 0.0))
        {
            return;
        }

        var sumGas = 0.0;
        for (var j = 0; j < gasCount; j++)
        {
            if (!(result.Moles[j] >= 0.0))
            {
                return;
            }

            sumGas += result.Moles[j];
        }

        if (!(sumGas > 0.0))
        {
            return;
        }

        var isTp = problem.Kind == ProblemKind.AssignedTemperaturePressure;
        var temperature = isTp ? problem.Temperature : (problem.Temperature > 0.0 ? problem.Temperature : DefaultTemperatureEstimate);
        if (!(temperature > 0.0))
        {
            return;
        }

        var logN = Math.Log(sumGas);
        var logPressure = Math.Log(problem.Pressure / StandardPressure);
        var iterations = 0;
        if (!isTp)
        {
            var target = problem.Kind == ProblemKind.AssignedEnthalpyPressure
                ? problem.Target / PhysicalConstants.R       // h0/R, K·kmol/kg
                : problem.Target / PhysicalConstants.R;      // s0/R, kmol/kg
            var converged = false;
            for (var step = 0; step < MaxNewtonSteps; step++)
            {
                EvaluateFunctions(table, scratch, temperature);
                var value = 0.0;
                var slope = 0.0;    // d(value)/dT
                for (var j = 0; j < speciesCount; j++)
                {
                    var nj = result.Moles[j];
                    if (nj == 0.0)
                    {
                        continue;
                    }

                    if (problem.Kind == ProblemKind.AssignedEnthalpyPressure)
                    {
                        value += nj * scratch.HOverRT[j] * temperature;
                        slope += nj * scratch.CpOverR[j];
                    }
                    else
                    {
                        value += j < gasCount
                            ? nj * (scratch.SOverR[j] - Math.Log(nj) + logN - logPressure)
                            : nj * scratch.SOverR[j];
                        slope += nj * scratch.CpOverR[j] / temperature;
                    }
                }

                iterations++;
                var deltaT = -(value - target) / slope;
                if (Math.Abs(deltaT) > 0.4 * temperature)
                {
                    deltaT = 0.4 * temperature * (deltaT > 0.0 ? 1.0 : -1.0);
                }

                temperature += deltaT;
                if (!(temperature >= MinTemperature) || !(temperature <= MaxTemperature))
                {
                    result.Iterations[0] = iterations;
                    result.Status[0] = (int)CaseStatus.TemperatureOutOfRange;
                    return;
                }

                if (Math.Abs(deltaT) <= FrozenTemperatureTest * temperature)
                {
                    converged = true;
                    break;
                }
            }

            if (!converged)
            {
                result.Iterations[0] = iterations;
                result.Status[0] = (int)CaseStatus.NotConverged;
                return;
            }
        }

        EvaluateFunctions(table, scratch, temperature);
        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        var state = new MixtureState();
        var n = sumGas;
        var hOverRT = 0.0;
        var sOverR = 0.0;
        var cpOverR = 0.0;
        var condensedMoles = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            hOverRT += nj * scratch.HOverRT[j];
            cpOverR += nj * scratch.CpOverR[j];
            if (j < gasCount)
            {
                sOverR += nj * (scratch.SOverR[j] - Math.Log(nj) + logN - logPressure);
            }
            else
            {
                sOverR += nj * scratch.SOverR[j];
                condensedMoles += nj;
            }
        }

        var r = PhysicalConstants.R;
        state.Temperature = temperature;
        state.Pressure = problem.Pressure;
        state.MolarMass = 1.0 / n;
        state.MixtureMolarMass = 1.0 / (n + condensedMoles);
        state.Density = problem.Pressure / (n * r * temperature);
        state.Enthalpy = r * temperature * hOverRT;
        state.InternalEnergy = state.Enthalpy - n * r * temperature;
        state.Entropy = r * sOverR;
        state.GibbsEnergy = state.Enthalpy - temperature * state.Entropy;
        state.CpFrozen = r * cpOverR;
        state.CvFrozen = state.CpFrozen - n * r;
        state.CpEquilibrium = state.CpFrozen;
        state.CvEquilibrium = state.CvFrozen;
        state.DlnVdlnT = 1.0;
        state.DlnVdlnP = -1.0;
        state.GammaS = state.CpFrozen / state.CvFrozen;
        state.SoundSpeed = Math.Sqrt(n * r * temperature * state.GammaS);
        state.Velocity = 0.0;
        state.Mach = 0.0;
        result.State[0] = state;
        result.Iterations[0] = iterations;
        result.Status[0] = (int)CaseStatus.Ok;
    }

    private static void EvaluateFunctions(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var h = SpeciesFunctions.HOverRT(table, j, temperature);
            var s = SpeciesFunctions.SOverR(table, j, temperature);
            scratch.HOverRT[j] = h;
            scratch.SOverR[j] = s;
            scratch.CpOverR[j] = SpeciesFunctions.CpOverR(table, j, temperature);
            scratch.GOverRT[j] = h - s;
        }
    }

    /// <summary>Takes the condensed species at position <paramref name="position"/> out of the solution; returns the new count.</summary>
    private static int RemoveCondensed(in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, int position)
    {
        result.Moles[scratch.CondensedInSolution[position]] = 0.0;
        for (var d = position; d + 1 < condensedCount; d++)
        {
            scratch.CondensedInSolution[d] = scratch.CondensedInSolution[d + 1];
        }

        condensedCount--;
        scratch.CondensedInSolution[condensedCount] = -1;
        return condensedCount;
    }

    /// <summary>How far the temperature lies outside the species' range, in K (zero inside).</summary>
    private static double DistanceOutOfRange(in SpeciesTableView table, int species, double temperature)
    {
        var first = table.IntervalStart[species];
        var last = first + table.IntervalCount[species] - 1;
        var low = table.IntervalBounds[first * 2];
        var high = table.IntervalBounds[last * 2 + 1];
        return Math.Max(Math.Max(low - temperature, temperature - high), 0.0);
    }

    /// <summary>Another condensed record with the same formula, active, not in the solution and in range at the temperature; −1 if none.</summary>
    private static int OtherPhaseInRange(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, int species, double temperature)
    {
        var speciesCount = table.SpeciesCount;
        for (var k = table.GasCount; k < speciesCount; k++)
        {
            if (k == species || scratch.SpeciesActive[k] == 0 || InSolution(scratch, condensedCount, k)
                || !SpeciesFunctions.IsInRange(table, k, temperature))
            {
                continue;
            }

            var same = true;
            for (var i = 0; i < table.ElementCount; i++)
            {
                if (table.Stoichiometry[i * speciesCount + k] != table.Stoichiometry[i * speciesCount + species])
                {
                    same = false;
                }
            }

            if (same)
            {
                return k;
            }
        }

        return -1;
    }

    private static bool InSolution(in EquilibriumScratch scratch, int condensedCount, int species)
    {
        for (var c = 0; c < condensedCount; c++)
        {
            if (scratch.CondensedInSolution[c] == species)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Element balance over the retained species: |b_i° − Σ a_ij n_j| against the tolerance times (relative: b_max; absolute: max(1, b_i)).</summary>
    private static bool ElementBalanceWithin(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                             in EquilibriumResult result, double tolerance, bool relativeToLargest)
    {
        var speciesCount = table.SpeciesCount;
        var largest = 0.0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            largest = Math.Max(largest, problem.ElementMoles[i]);
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            if (scratch.ElementActive[i] == 0)
            {
                continue;
            }

            var b = 0.0;
            for (var j = 0; j < speciesCount; j++)
            {
                b += table.Stoichiometry[i * speciesCount + j] * result.Moles[j];
            }

            var bound = relativeToLargest ? tolerance * largest : tolerance * Math.Max(1.0, problem.ElementMoles[i]);
            if (Math.Abs(problem.ElementMoles[i] - b) > bound)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Fills the reduced iteration matrix (RP-1311 table 2.1) and its right-hand side for the current estimate.</summary>
    private static void Assemble(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, int unknowns, int stride, int condensedCount, bool isTp, bool isHp,
                                 double logN, double logPressure, double sumGas, double n, double hOverRT, double sOverR, double temperature)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        var elementCount = table.ElementCount;
        var nRow = elementCount + condensedCount;
        var tRow = nRow + 1;
        for (var k = 0; k < unknowns * stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var k = 0; k < unknowns; k++)
        {
            scratch.RightHandSide[k] = 0.0;
        }

        // Contributions of the gaseous species, accumulated species by species.
        for (var j = 0; j < gasCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            var h = scratch.HOverRT[j];
            var s = scratch.SOverR[j];
            var mu = scratch.GOverRT[j] + scratch.LogMoles[j] - logN + logPressure;
            // The entropy row weighs a gaseous species by its entropy in the mixture, mixing terms included.
            var tWeight = isTp ? 0.0 : (isHp ? h : s - (scratch.LogMoles[j] - logN) - logPressure);
            for (var k = 0; k < elementCount; k++)
            {
                var akj = table.Stoichiometry[k * speciesCount + j];
                if (akj == 0.0)
                {
                    continue;
                }

                var akjn = akj * nj;
                for (var i = 0; i < elementCount; i++)
                {
                    scratch.Matrix[k * stride + i] += akjn * table.Stoichiometry[i * speciesCount + j];
                }

                scratch.Matrix[k * stride + nRow] += akjn;
                scratch.RightHandSide[k] += akjn * mu;
                if (!isTp)
                {
                    scratch.Matrix[k * stride + tRow] += akjn * h;
                    scratch.Matrix[tRow * stride + k] += akjn * tWeight;
                }
            }

            scratch.RightHandSide[nRow] += nj * mu;
            if (!isTp)
            {
                scratch.Matrix[nRow * stride + tRow] += nj * h;
                scratch.Matrix[tRow * stride + nRow] += nj * tWeight;
                scratch.Matrix[tRow * stride + tRow] += nj * scratch.CpOverR[j] + nj * tWeight * h;
                scratch.RightHandSide[tRow] += nj * tWeight * mu;
            }
        }

        // The n row shares its π coefficients with the Δln n column of the element rows.
        for (var i = 0; i < elementCount; i++)
        {
            scratch.Matrix[nRow * stride + i] = scratch.Matrix[i * stride + nRow];
        }

        scratch.Matrix[nRow * stride + nRow] = sumGas - n;
        scratch.RightHandSide[nRow] += n - sumGas;

        // Element rows: b° − b, and the condensed columns; condensed rows.
        for (var k = 0; k < elementCount; k++)
        {
            if (scratch.ElementActive[k] == 0)
            {
                scratch.Matrix[k * stride + k] = 1.0;
                scratch.RightHandSide[k] = 0.0;
                if (!isTp)
                {
                    scratch.Matrix[tRow * stride + k] = 0.0;
                }

                continue;
            }

            var b = 0.0;
            for (var j = 0; j < speciesCount; j++)
            {
                b += table.Stoichiometry[k * speciesCount + j] * result.Moles[j];
            }

            scratch.RightHandSide[k] += problem.ElementMoles[k] - b;
        }

        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var row = elementCount + c;
            var h = scratch.HOverRT[j];
            var tWeight = isTp ? 0.0 : (isHp ? h : scratch.SOverR[j]);
            for (var i = 0; i < elementCount; i++)
            {
                var aij = table.Stoichiometry[i * speciesCount + j];
                scratch.Matrix[row * stride + i] = aij;
                scratch.Matrix[i * stride + row] = aij;
            }

            scratch.RightHandSide[row] = scratch.GOverRT[j];
            if (!isTp)
            {
                scratch.Matrix[row * stride + tRow] = h;
                scratch.Matrix[tRow * stride + row] = tWeight;
                scratch.Matrix[tRow * stride + tRow] += result.Moles[j] * scratch.CpOverR[j];
            }
        }

        if (isHp)
        {
            scratch.RightHandSide[tRow] += problem.Target / (PhysicalConstants.R * temperature) - hOverRT;
        }
        else if (!isTp)
        {
            scratch.RightHandSide[tRow] += problem.Target / PhysicalConstants.R - sOverR + n - sumGas;
        }
    }

    /// <summary>The mixture properties and the equilibrium derivatives (RP-1311 sections 2.5 and 2.6) at the converged composition.</summary>
    private static CaseStatus FinishState(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                          in EquilibriumResult result, int condensedCount, double logN, double logPressure,
                                          double temperature, int stride)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        var elementCount = table.ElementCount;
        var r = PhysicalConstants.R;
        var n = 0.0;
        var hOverRT = 0.0;
        var sOverR = 0.0;
        var cpOverR = 0.0;
        var hSquared = 0.0;
        var condensedMoles = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            hOverRT += nj * scratch.HOverRT[j];
            cpOverR += nj * scratch.CpOverR[j];
            if (j < gasCount)
            {
                n += nj;
                sOverR += nj * (scratch.SOverR[j] - scratch.LogMoles[j] + logN - logPressure);
                hSquared += nj * scratch.HOverRT[j] * scratch.HOverRT[j];
            }
            else
            {
                sOverR += nj * scratch.SOverR[j];
                condensedMoles += nj;
            }
        }

        // Derivatives with respect to ln T (table 2.3) and ln p (table 2.4): the tp matrix with two right-hand sides.
        var unknowns = elementCount + condensedCount + 1;
        var nRow = elementCount + condensedCount;
        var dlnNdlnT = 0.0;
        var dlnNdlnP = 0.0;
        var reaction = 0.0;
        for (var pass = 0; pass < 2; pass++)
        {
            AssembleDerivative(table, scratch, result, condensedCount, unknowns, stride, pass == 0);
            if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, unknowns, stride))
            {
                return CaseStatus.SingularMatrix;
            }

            if (pass == 0)
            {
                dlnNdlnT = scratch.RightHandSide[nRow];
                // Equation (2.59): the reaction part of cp/R from the temperature derivatives.
                for (var i = 0; i < elementCount; i++)
                {
                    var sum = 0.0;
                    for (var j = 0; j < gasCount; j++)
                    {
                        sum += table.Stoichiometry[i * speciesCount + j] * result.Moles[j] * scratch.HOverRT[j];
                    }

                    reaction += sum * scratch.RightHandSide[i];
                }

                for (var c = 0; c < condensedCount; c++)
                {
                    reaction += scratch.HOverRT[scratch.CondensedInSolution[c]] * scratch.RightHandSide[elementCount + c];
                }

                var gasEnthalpy = 0.0;
                for (var j = 0; j < gasCount; j++)
                {
                    gasEnthalpy += result.Moles[j] * scratch.HOverRT[j];
                }

                reaction += gasEnthalpy * dlnNdlnT + hSquared;
            }
            else
            {
                dlnNdlnP = scratch.RightHandSide[nRow];
            }
        }

        var state = new MixtureState();
        state.Temperature = temperature;
        state.Pressure = problem.Pressure;
        state.MolarMass = 1.0 / n;
        state.MixtureMolarMass = 1.0 / (n + condensedMoles);
        state.Density = problem.Pressure / (n * r * temperature);
        state.Enthalpy = r * temperature * hOverRT;
        state.InternalEnergy = state.Enthalpy - n * r * temperature;
        state.Entropy = r * sOverR;
        state.GibbsEnergy = state.Enthalpy - temperature * state.Entropy;
        state.CpFrozen = r * cpOverR;
        state.CvFrozen = state.CpFrozen - n * r;
        state.CpEquilibrium = r * (cpOverR + reaction);
        state.DlnVdlnT = 1.0 + dlnNdlnT;
        state.DlnVdlnP = -1.0 + dlnNdlnP;
        state.CvEquilibrium = state.CpEquilibrium + n * r * state.DlnVdlnT * state.DlnVdlnT / state.DlnVdlnP;
        state.GammaS = -(state.CpEquilibrium / state.CvEquilibrium) / state.DlnVdlnP;
        state.SoundSpeed = Math.Sqrt(n * r * temperature * state.GammaS);
        state.Velocity = 0.0;
        state.Mach = 0.0;
        result.State[0] = state;
        return CaseStatus.Ok;
    }

    /// <summary>The tp-type matrix at the converged composition with the right-hand side of table 2.3 (temperature) or 2.4 (pressure).</summary>
    private static void AssembleDerivative(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                           int condensedCount, int unknowns, int stride, bool temperatureDerivative)
    {
        var speciesCount = table.SpeciesCount;
        var gasCount = table.GasCount;
        var elementCount = table.ElementCount;
        var nRow = elementCount + condensedCount;
        for (var k = 0; k < unknowns * stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var k = 0; k < unknowns; k++)
        {
            scratch.RightHandSide[k] = 0.0;
        }

        for (var j = 0; j < gasCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            var weight = temperatureDerivative ? -scratch.HOverRT[j] : 1.0;
            for (var k = 0; k < elementCount; k++)
            {
                var akj = table.Stoichiometry[k * speciesCount + j];
                if (akj == 0.0)
                {
                    continue;
                }

                var akjn = akj * nj;
                for (var i = 0; i < elementCount; i++)
                {
                    scratch.Matrix[k * stride + i] += akjn * table.Stoichiometry[i * speciesCount + j];
                }

                scratch.Matrix[k * stride + nRow] += akjn;
                scratch.RightHandSide[k] += akjn * weight;
            }

            scratch.RightHandSide[nRow] += nj * weight;
        }

        for (var i = 0; i < elementCount; i++)
        {
            scratch.Matrix[nRow * stride + i] = scratch.Matrix[i * stride + nRow];
            if (scratch.ElementActive[i] == 0)
            {
                scratch.Matrix[i * stride + i] = 1.0;
                scratch.RightHandSide[i] = 0.0;
            }
        }

        // At convergence Σ n_j − n vanishes; the n-row diagonal is zero.
        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var row = elementCount + c;
            for (var i = 0; i < elementCount; i++)
            {
                var aij = table.Stoichiometry[i * speciesCount + j];
                scratch.Matrix[row * stride + i] = aij;
                scratch.Matrix[i * stride + row] = aij;
            }

            scratch.RightHandSide[row] = temperatureDerivative ? -scratch.HOverRT[j] : 0.0;
        }
    }
}
