namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The tolerances this node owns: those of comparisons that are not with the reference. A comparison with the reference
/// takes the fixtures node's table and nothing else (BOOT.md, Invariants); a comparison of two paths of this tree against
/// each other has no reference to ask, so the number lives here, once, with where it comes from.
/// </summary>
internal static class Tolerances
{
    /// <summary>
    /// Two paths through the same arithmetic, relative: the same state reached by an equilibrium solve and by a frozen one
    /// at its composition, or the same plateau state reached twice. The solver polishes until its corrections fall below
    /// 1e-11, so this leaves two decades of head-room over its own convergence.
    /// </summary>
    public const double SelfConsistency = 1.0e-9;

    /// <summary>An algebraic identity between two numbers of one state, where only the rounding of the arithmetic separates them.</summary>
    public const double Exact = 1.0e-12;

    /// <summary>
    /// The largest positive per-mole inclusion gain an Ok state may leave on a condensed candidate outside the solution: the
    /// rounding of the polished multipliers, not a missed equilibrium.
    /// </summary>
    public const double ResidualInclusionGain = 1.0e-9;

    /// <summary>
    /// The largest departure of a retained gas's chemical potential from the sum of its element multipliers that an Ok state may
    /// show, in units of RT, as <see cref="EquilibriumConditions.Violations"/> checks it: two decades above the polish
    /// threshold the solver's corrections fall below, 1e-11, with room for the gas of a mole fraction near 1e-6.
    /// </summary>
    public const double GasChemicalPotential = 1.0e-6;

    /// <summary>
    /// Relative: a derivative of the state against a central difference of two solved states at p(1 ± 1e-4). The difference
    /// is second order, about 1e-8 at that step, and the polished states carry about 1e-11 of noise divided by the step, so
    /// the 1e-6 of the StateRecord criterion has two decades of room (measured 2e-8 to 9e-8 on the plateau, 2026-10-03).
    /// </summary>
    public const double FiniteDifference = 1.0e-6;

    /// <summary>
    /// J/kg: an absolute floor added to the enthalpy self-consistency bound. <see cref="SelfConsistency"/> alone is relative
    /// to the enthalpy and gives no bound where the two paths' enthalpies are compared near zero.
    /// </summary>
    public const double EnthalpyFloor = 1.0e-3;

    /// <summary>
    /// K: how far a pinned pair's temperature may sit from the record's shared bound at the cut — generous next to the
    /// crossing T* the fits' own disagreement moves it by (Equilibrium BOOT.md, effective range).
    /// </summary>
    public const double PlateauCutTolerance = 0.01;
}
