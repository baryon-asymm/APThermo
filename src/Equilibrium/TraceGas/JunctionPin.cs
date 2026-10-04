namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// What one trace-gas convergence of an hp or sp problem remembers of a data junction (BOOT.md, "The data junction"): the last
/// internal interval bound a temperature step crossed, and, once the convergence is pinned at a junction, the two temperatures the
/// tp states are converged at, the miss of the first, and which of the three tp convergences the loop is in. One value per convergence,
/// changed field by field through <c>ref</c> by <see cref="DataJunction"/>, never copied whole (BOOT.md, "No whole IterationState through
/// ref"): no two <c>bool</c> fields, and the one <c>int</c> sits after the doubles.
/// </summary>
internal struct JunctionPin
{
    /// <summary>Not pinned: the temperature is an unknown of the iteration.</summary>
    public const int Free = 0;

    /// <summary>Pinned at <see cref="Lower"/>, the tp state of the lower interval's functions, whose miss is not yet measured.</summary>
    public const int MeasuringLower = 1;

    /// <summary>Pinned at <see cref="Upper"/>, the tp state of the upper interval's functions.</summary>
    public const int AtUpper = 2;

    /// <summary>Pinned at <see cref="Lower"/> again, because the lower state is the nearer of the two to the target.</summary>
    public const int AtLower = 3;

    /// <summary>K: the last internal interval bound a temperature step crossed; 0 before the first crossing.</summary>
    public double LastBound;

    /// <summary>K: the junction T_J, the largest temperature on the lower interval.</summary>
    public double Lower;

    /// <summary>K: the smallest temperature on the upper interval.</summary>
    public double Upper;

    /// <summary>The miss of the tp state at <see cref="Lower"/> from the target, in the units of the target.</summary>
    public double LowerMiss;

    /// <summary><see cref="Free"/>, <see cref="MeasuringLower"/>, <see cref="AtUpper"/> or <see cref="AtLower"/>.</summary>
    public int Phase;
}
