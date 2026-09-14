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
/// added one at a time by the test of equation (3.7) and removed when their mole number turns negative; a record beyond
/// its effective range pairs with, or is switched for, the record of its formula on the other side of their crossing
/// (sections 3.4 and 3.5, completed by the condensed-species rule of BOOT.md: pinned pairs, the crossing T*, the switch
/// and range memories, and the anti-cycling skip of the inclusion test).
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
    public const int MaxCondensedSetChanges = 3 * ScratchLayout.MaxCondensedInSolution;

    private const double CorrectionTest = 0.5e-5;      // equation (3.5)
    private const double TemperatureTest = 1.0e-4;     // equation (3.6b)
    private const double PolishTest = 1.0e-11;         // the steps after the report's tests, until the corrections are this small
    private const int MaxPolishSteps = 6;
    private const double DefaultTemperatureEstimate = 3800.0;
    private const double MinTemperature = 100.0;
    private const double MaxTemperature = 20000.0;
    private const double StandardPressure = 1.0e5;    // Pa; the thermodynamic data are for 1 bar
    private const double ResetMoles = 1.0e-6;          // section 3.6: the reset of vanished species on a singular matrix
    private const int MaxSingularResets = 2;
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
        var lastSwitchedOut = -1;
        var lastRemovedForRange = -1;

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

                var sums = new MixtureSums { LogN = logN, LogPressure = logPressure, Temperature = temperature, N = Math.Exp(logN), SumGas = sumGas };
                for (var j = 0; j < speciesCount; j++)
                {
                    var nj = result.Moles[j];
                    if (nj == 0.0)
                    {
                        continue;
                    }

                    sums.HOverRT += nj * scratch.HOverRT[j];
                    sums.SOverR += j < gasCount
                        ? nj * (scratch.SOverR[j] - scratch.LogMoles[j] + logN - logPressure)
                        : nj * scratch.SOverR[j];
                    sums.CpOverR += nj * scratch.CpOverR[j];
                    if (j >= gasCount)
                    {
                        sums.CondensedMoles += nj;
                    }
                }

                var layout = new SystemLayout(problem.Kind, elementCount, condensedCount, unknownStride);
                IterationMatrix.Assemble(table, problem, scratch, result, layout, sums);

                var solved = DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.Unknowns, layout.Stride);
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

                var deltaLogN = scratch.RightHandSide[layout.NRow];
                var deltaLogT = isTp ? 0.0 : scratch.RightHandSide[layout.TRow];

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

                var worst = sums.N * Math.Abs(deltaLogN) / total;
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

                var balanced = ElementBalance.WithinReportTest(table, problem, scratch, result);
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
            var skipInclusion = lastRemovedForRange;
            lastRemovedForRange = -1;
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
                if (PhaseGeometry.InEffectiveRange(table, scratch, j, temperature))
                {
                    continue;
                }

                if (PhaseGeometry.PartnerInSolution(table, scratch, condensedCount, j) >= 0)
                {
                    continue;
                }

                var above = temperature > PhaseGeometry.EffectiveHigh(table, scratch, j);
                var adjacent = PhaseGeometry.Adjacent(table, scratch, j, above);
                var k = PhaseGeometry.PhaseAt(table, scratch, condensedCount, j, temperature);
                if (k < 0 && adjacent >= 0 && !PhaseGeometry.InSolution(scratch, condensedCount, adjacent))
                {
                    k = adjacent;
                }

                var bound = above ? PhaseGeometry.RecordHigh(table, j) : PhaseGeometry.RecordLow(table, j);
                var neighbour = k >= 0 && k == adjacent;
                var crossing = neighbour ? PhaseGeometry.Crossing(table, j, k, bound) : bound;
                var latent = neighbour ? Math.Abs(SpeciesFunctions.HOverRT(table, j, bound) - SpeciesFunctions.HOverRT(table, k, bound)) : 0.0;
                var pair = neighbour && !isTp && latent >= SpeciesFunctions.LatentHeatThreshold && condensedCount < ScratchLayout.MaxCondensedInSolution
                           && (Math.Abs(temperature - crossing) <= PhaseTransitionWindow || k == lastSwitchedOut);
                if (pair)
                {
                    scratch.CondensedInSolution[condensedCount++] = k;
                    result.Moles[k] = 0.0;
                }
                else
                {
                    condensedCount = RemoveCondensed(scratch, result, condensedCount, c);
                    if (k >= 0 && condensedCount < ScratchLayout.MaxCondensedInSolution)
                    {
                        scratch.CondensedInSolution[condensedCount++] = k;
                        result.Moles[k] = 0.0;
                        lastSwitchedOut = j;
                    }
                    else if (k < 0)
                    {
                        // Anti-cycling (BOOT.md): the first escape through its own bound is forgiven and only
                        // skipped for one inclusion pass; a species that escapes twice in one solve chases a
                        // temperature the solution keeps leaving and stands down for the rest of the solve.
                        if (scratch.SpeciesActive[j] == 2)
                        {
                            scratch.SpeciesActive[j] = 0;
                        }
                        else
                        {
                            scratch.SpeciesActive[j] = 2;
                            lastRemovedForRange = j;
                        }
                    }
                }

                changed = true;
            }

            if (!changed && condensedCount < ScratchLayout.MaxCondensedInSolution)
            {
                var best = -1;
                var bestGain = 0.0;
                var skippedGain = 0.0;
                for (var j = gasCount; j < speciesCount; j++)
                {
                    if (scratch.SpeciesActive[j] == 0 || PhaseGeometry.InSolution(scratch, condensedCount, j)
                        || !PhaseGeometry.InEffectiveRange(table, scratch, j, temperature)
                        || PhaseGeometry.PartnerInSolution(table, scratch, condensedCount, j) >= 0)
                    {
                        continue;
                    }

                    var gain = -scratch.GOverRT[j];
                    for (var i = 0; i < elementCount; i++)
                    {
                        gain += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
                    }

                    if (j == skipInclusion)
                    {
                        skippedGain = gain;
                        continue;
                    }

                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        best = j;
                    }
                }

                if (best < 0 && skippedGain > 0.0)
                {
                    best = skipInclusion;
                    bestGain = skippedGain;
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

        if (status == CaseStatus.Ok && !ElementBalance.WithinInvariant(table, problem, scratch, result))
        {
            status = CaseStatus.NotConverged;
        }

        if (status == CaseStatus.Ok && StoodDownCandidateRemains(table, scratch, result, condensedCount, temperature))
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

    /// <summary>
    /// A positive per-mole inclusion gain this far above zero is real at a converged state; below it is the rounding
    /// of the polished multipliers. Used only for the stand-down honesty check at an Ok exit.
    /// </summary>
    private const double ResidualGainLimit = 1.0e-9;

    /// <summary>
    /// True when a species stood down by the anti-cycling rule would qualify for inclusion at the final state: its
    /// elements present, inside its effective range at the final temperature, no phase partner in the solution and a
    /// per-mole gain above <see cref="ResidualGainLimit"/>. An Ok status must not hide such a candidate (BOOT.md):
    /// the caller turns it into <see cref="CaseStatus.NotConverged"/>.
    /// </summary>
    private static bool StoodDownCandidateRemains(in SpeciesTableView table, in EquilibriumScratch scratch,
                                                  in EquilibriumResult result, int condensedCount, double temperature)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        for (var j = table.GasCount; j < speciesCount; j++)
        {
            if (scratch.SpeciesActive[j] != 0 || PhaseGeometry.InSolution(scratch, condensedCount, j))
            {
                continue;
            }

            var present = true;
            for (var i = 0; i < elementCount && present; i++)
            {
                present = table.Stoichiometry[i * speciesCount + j] == 0.0 || scratch.ElementActive[i] == 1;
            }

            if (!present || !PhaseGeometry.InEffectiveRange(table, scratch, j, temperature)
                || PhaseGeometry.PartnerInSolution(table, scratch, condensedCount, j) >= 0)
            {
                continue;
            }

            var gain = -scratch.GOverRT[j];
            for (var i = 0; i < elementCount; i++)
            {
                gain += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
            }

            if (gain > ResidualGainLimit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The mixture properties and the equilibrium derivatives (RP-1311 sections 2.5 and 2.6) at the converged composition.</summary>
    private static CaseStatus FinishState(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                          in EquilibriumResult result, int condensedCount, double logN, double logPressure,
                                          double temperature, int stride)
    {
        var gasCount = table.GasCount;
        var sums = new MixtureSums { LogN = logN, LogPressure = logPressure, Temperature = temperature };
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            sums.HOverRT += nj * scratch.HOverRT[j];
            sums.CpOverR += nj * scratch.CpOverR[j];
            if (j < gasCount)
            {
                sums.SumGas += nj;
                sums.SOverR += nj * (scratch.SOverR[j] - scratch.LogMoles[j] + logN - logPressure);
            }
            else
            {
                sums.SOverR += nj * scratch.SOverR[j];
                sums.CondensedMoles += nj;
            }
        }

        var derivatives = DerivativeSystem.Solve(table, scratch, result, condensedCount, stride);
        if (!derivatives.Solved)
        {
            return CaseStatus.SingularMatrix;
        }

        MixtureProperties.WriteEquilibrium(problem, result, sums, derivatives);
        return CaseStatus.Ok;
    }
}
