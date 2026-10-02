using APThermo.Thermo;

namespace APThermo.Equilibrium.StateRecord;

/// <summary>
/// The state record of one converged case: the assignments both paths share written once here, then the equilibrium (or
/// pinned-pair) closure or the frozen one. Kernel-compatible; it writes <c>result.State[0]</c> and nothing else.
/// </summary>
/// <remarks>
/// Property definitions of RP-1311: M = 1/n over the gaseous moles (2.3a), MW = 1 kg over the moles of all species (2.4a;
/// the reference's MW), γ_s = −(∂ln p/∂ln V)_s and a² = n R T γ_s per unit mass (2.71, 2.74, section 2.6). At a pinned pair the state carries the
/// reference's convention cp_eq = cv_eq = (∂ln V/∂ln T)_p = 0 with the real (∂ln V/∂ln p)_T (BOOT.md, API.md).
/// </remarks>
internal static class MixtureProperties
{
    /// <summary>
    /// The converged equilibrium state, with the derivatives of section 2.5 or the plateau convention of a pinned pair.
    /// False, with nothing written, when the state guard (<see cref="IsPhysical"/>) fails (the third pass of
    /// 2026-09-28, finding F3): the guard is decided before <c>result.State[0]</c> is touched, not after.
    /// </summary>
    public static bool WriteEquilibrium(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums,
                                        in Derivatives derivatives)
    {
        var state = Common(problem, sums);
        if (derivatives.Pinned)
        {
            state.CpEquilibrium = 0.0;
            state.CvEquilibrium = 0.0;
            state.DlnVdlnT = 0.0;
            state.DlnVdlnP = -1.0 + derivatives.DlnNdlnP;
            state.GammaS = -1.0 / state.DlnVdlnP;
        }
        else
        {
            state.CpEquilibrium = PhysicalConstants.R * (sums.CpOverR + derivatives.Reaction);
            state.DlnVdlnT = 1.0 + derivatives.DlnNdlnT;
            state.DlnVdlnP = -1.0 + derivatives.DlnNdlnP;
            state.CvEquilibrium = state.CpEquilibrium + sums.SumGas * PhysicalConstants.R * state.DlnVdlnT * state.DlnVdlnT / state.DlnVdlnP;
            state.GammaS = -(state.CpEquilibrium / state.CvEquilibrium) / state.DlnVdlnP;
        }

        return Close(result, sums, state, derivatives.Pinned);
    }

    /// <summary>
    /// The frozen state: an ideal gas of fixed composition, whose equilibrium response is its frozen one. False, with
    /// nothing written, when the state guard fails (the third pass of 2026-09-28, finding F3).
    /// </summary>
    public static bool WriteFrozen(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums)
    {
        var state = Common(problem, sums);
        state.CpEquilibrium = state.CpFrozen;
        state.CvEquilibrium = state.CvFrozen;
        state.DlnVdlnT = 1.0;
        state.DlnVdlnP = -1.0;
        state.GammaS = state.CpFrozen / state.CvFrozen;
        return Close(result, sums, state, pinned: false);
    }

    /// <summary>Everything that does not depend on which response the state carries.</summary>
    private static MixtureState Common(in EquilibriumProblem problem, in MixtureSums sums)
    {
        var r = PhysicalConstants.R;
        var n = sums.SumGas;
        var temperature = sums.Temperature;
        var enthalpy = r * temperature * sums.HOverRT;
        var entropy = r * sums.SOverR;
        var cpFrozen = r * sums.CpOverR;
        var state = new MixtureState
        {
            Temperature = temperature,
            Pressure = problem.Pressure,
            MolarMass = 1.0 / n,
            MixtureMolarMass = 1.0 / (n + sums.CondensedMoles),
            Density = problem.Pressure / (n * r * temperature),
            Enthalpy = enthalpy,
            InternalEnergy = enthalpy - n * r * temperature,
            Entropy = entropy,
            GibbsEnergy = enthalpy - temperature * entropy,
            CpFrozen = cpFrozen,
            CvFrozen = cpFrozen - n * r,
            Velocity = 0.0,
            Mach = 0.0,
        };
        return state;
    }

    /// <summary>
    /// The sound speed needs the isentropic exponent, so it is computed after the closure; the flow speed is the
    /// performance node's. The state guard is decided on this local state before anything is written (the third
    /// pass of 2026-09-28, finding F3): a caller reading a failing status never finds a stale or partial
    /// <c>result.State[0]</c>.
    /// </summary>
    private static bool Close(in EquilibriumResult result, in MixtureSums sums, MixtureState state, bool pinned)
    {
        state.SoundSpeed = Math.Sqrt(sums.SumGas * PhysicalConstants.R * sums.Temperature * state.GammaS);
        if (!IsPhysical(state, pinned))
        {
            return false;
        }

        result.State[0] = state;
        return true;
    }

    /// <summary>
    /// The state guard (BOOT.md, 2026-09-28): true when every heat capacity, γs and the sound speed of the state are
    /// finite and positive — a species evaluated outside its fitted range can still return a finite but unphysical
    /// polynomial value inside the mixture's temperature window. The pinned pair's zero <c>CpEquilibrium</c>/
    /// <c>CvEquilibrium</c> convention (Property definitions, BOOT.md) is exempt for <paramref name="pinned"/>: a
    /// frozen state's heat capacities are never legitimately zero.
    /// </summary>
    private static bool IsPhysical(in MixtureState state, bool pinned)
    {
        var equilibriumOk = pinned || (IsFinitePositive(state.CpEquilibrium) && IsFinitePositive(state.CvEquilibrium));
        return IsFinitePositive(state.CpFrozen) && IsFinitePositive(state.CvFrozen) && equilibriumOk
               && IsFinitePositive(state.GammaS) && IsFinitePositive(state.SoundSpeed);
    }

    /// <summary>Finite and strictly positive: excludes NaN (every comparison with it is false), zero, negative values and +∞.</summary>
    private static bool IsFinitePositive(double value) => value is > 0.0 and < double.PositiveInfinity;
}
