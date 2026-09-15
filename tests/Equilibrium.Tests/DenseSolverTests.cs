using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>L0: the internal dense solver against systems with known solutions.</summary>
[Collection(CpuCollection.Name)]
public sealed class DenseSolverTests(CpuFixture fixture)
{
    [Fact]
    public void Solves_a_three_by_three_system()
    {
        // 2x + y − z = 8; −3x − y + 2z = −11; −2x + y + 2z = −3  →  x = 2, y = 3, z = −1;
        // the bound is the rounding of Gaussian elimination with scaled partial pivoting on this system, not a
        // tolerance of the node (this fact is a unit test of the routine, outside the Tolerances invariant below).
        var (solved, x) = Solve([2, 1, -1, -3, -1, 2, -2, 1, 2], [8, -11, -3], 3, 3);
        Assert.True(solved);
        Assert.Equal(2.0, x[0], 1e-14);
        Assert.Equal(3.0, x[1], 1e-14);
        Assert.Equal(-1.0, x[2], 1e-14);
    }

    [Fact]
    public void Pivots_around_a_zero_diagonal()
    {
        // The bound is the rounding of the elimination once it has pivoted around the zero diagonal entry.
        var (solved, x) = Solve([0, 1, 1, 0], [1, 2], 2, 2);
        Assert.True(solved);
        Assert.Equal(2.0, x[0], 1e-15);
        Assert.Equal(1.0, x[1], 1e-15);
    }

    [Fact]
    public void Reports_a_singular_matrix()
    {
        var (solved, _) = Solve([1, 2, 2, 4], [1, 2], 2, 2);
        Assert.False(solved);
    }

    [Fact]
    public void Honours_a_stride_wider_than_the_system()
    {
        // The same 3×3 system stored in a 3×5 area, solved to the same rounding as the system above.
        var matrix = new double[] { 2, 1, -1, 9, 9, -3, -1, 2, 9, 9, -2, 1, 2, 9, 9 };
        var (solved, x) = Solve(matrix, [8, -11, -3, 9, 9], 3, 5);
        Assert.True(solved);
        Assert.Equal(2.0, x[0], 1e-14);
        Assert.Equal(3.0, x[1], 1e-14);
        Assert.Equal(-1.0, x[2], 1e-14);
    }

    [Fact]
    public void Scales_rows_before_choosing_the_pivot()
    {
        // Row scaling makes the small-coefficient row the pivot when its scaled entry is the larger one; the
        // coarser 1e-9 bound is the rounding the scaling itself carries, wider than the unscaled systems above.
        var (solved, x) = Solve([1e-10, 1e-10, 1, 2], [2e-10, 3], 2, 2);
        Assert.True(solved);
        Assert.Equal(1.0, x[0], 1e-9);
        Assert.Equal(1.0, x[1], 1e-9);
    }

    private (bool Solved, double[] Solution) Solve(double[] matrix, double[] rhs, int n, int stride)
    {
        var accelerator = fixture.Accelerator;
        using var a = accelerator.Allocate1D(matrix);
        using var b = accelerator.Allocate1D(rhs);
        using var scale = accelerator.Allocate1D<double>(n);
        var solved = DenseSolver.Solve(a.View, b.View, scale.View, n, stride);
        return (solved, b.GetAsArray1D());
    }
}
