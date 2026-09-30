using System.Reflection;

namespace APThermo.Performance.Tests;

/// <summary>
/// L0: the compile size of the rocket kernel is held by the node that owns the method (BOOT.md, "Compile size"; the root's
/// Compile size constraint). The allocation bound of the compile itself is the execution tests node's guard.
/// </summary>
public sealed class CompileSizeTests
{
    /// <summary>
    /// <c>StationSolve.At</c> is the one method through which the rocket program reaches the equilibrium solves, from seven
    /// call sites, and carries <see cref="MethodImplAttributes.NoInlining"/> so ILGPU compiles one copy of the solve instead of
    /// seven. Read from the compiled method, named directly: <c>StationSolve</c> is internal to the performance node and reached
    /// through its <c>InternalsVisibleTo</c> grant. Red with the attribute removed.
    /// </summary>
    [Fact]
    public void TheStationSolveIsNotInlined()
    {
        var method = typeof(StationSolve).GetMethod(nameof(StationSolve.At));

        Assert.NotNull(method);
        Assert.True(method.GetMethodImplementationFlags().HasFlag(MethodImplAttributes.NoInlining),
                    "StationSolve.At is inlined by ILGPU at each of its call sites: a full copy of the solve at each");
    }
}
