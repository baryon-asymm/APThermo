using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Performance;

/// <summary>
/// The four views one rocket case is solved over, built once in <see cref="RocketSolver.Solve"/> and passed to every stage:
/// the species table, the case, its scratch and the result it writes into. Blittable, as the kernel requires.
/// </summary>
internal readonly struct RocketContext
{
    public readonly SpeciesTableView Table;
    public readonly RocketProblem Problem;
    public readonly EquilibriumScratch Scratch;
    public readonly RocketResult Result;

    public RocketContext(in SpeciesTableView table, in RocketProblem problem, in EquilibriumScratch scratch, in RocketResult result)
    {
        Table = table;
        Problem = problem;
        Scratch = scratch;
        Result = result;
    }
}
