using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

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
    /// <summary>The converged equilibrium state, with the derivatives of section 2.5 or the plateau convention of a pinned pair.</summary>
    public static void WriteEquilibrium(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums,
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

        Close(result, sums, state);
    }

    /// <summary>The frozen state: an ideal gas of fixed composition, whose equilibrium response is its frozen one.</summary>
    public static void WriteFrozen(in EquilibriumProblem problem, in EquilibriumResult result, in MixtureSums sums)
    {
        var state = Common(problem, sums);
        state.CpEquilibrium = state.CpFrozen;
        state.CvEquilibrium = state.CvFrozen;
        state.DlnVdlnT = 1.0;
        state.DlnVdlnP = -1.0;
        state.GammaS = state.CpFrozen / state.CvFrozen;
        Close(result, sums, state);
    }

    /// <summary>Everything that does not depend on which response the state carries.</summary>
    private static MixtureState Common(in EquilibriumProblem problem, in MixtureSums sums)
    {
        var r = PhysicalConstants.R;
        var n = sums.SumGas;
        var temperature = sums.Temperature;
        var state = new MixtureState();
        state.Temperature = temperature;
        state.Pressure = problem.Pressure;
        state.MolarMass = 1.0 / n;
        state.MixtureMolarMass = 1.0 / (n + sums.CondensedMoles);
        state.Density = problem.Pressure / (n * r * temperature);
        state.Enthalpy = r * temperature * sums.HOverRT;
        state.InternalEnergy = state.Enthalpy - n * r * temperature;
        state.Entropy = r * sums.SOverR;
        state.GibbsEnergy = state.Enthalpy - temperature * state.Entropy;
        state.CpFrozen = r * sums.CpOverR;
        state.CvFrozen = state.CpFrozen - n * r;
        state.Velocity = 0.0;
        state.Mach = 0.0;
        return state;
    }

    /// <summary>The sound speed needs the isentropic exponent, so it is written after the closure; the flow speed is the performance node's.</summary>
    private static void Close(in EquilibriumResult result, in MixtureSums sums, MixtureState state)
    {
        state.SoundSpeed = Math.Sqrt(sums.SumGas * PhysicalConstants.R * sums.Temperature * state.GammaS);
        result.State[0] = state;
    }
}
