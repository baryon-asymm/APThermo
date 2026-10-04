using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;

namespace APThermo.Equilibrium.Tests;

/// <summary>What the buffers a solve owns or writes hold before it runs (<see cref="HostSolver.SolveFilled"/>).</summary>
internal enum BufferFill
{
    /// <summary>Zeros: how the batch pipeline starts a run, and the harness's default.</summary>
    Zero,

    /// <summary>NaN in every double, −77777 in every integer: a value no arithmetic of the solver can mistake for a number it computed.</summary>
    NotANumber,

    /// <summary>1e300 in every double, the largest integer in every integer: a number that any arithmetic overflows or dwarfs.</summary>
    Large,
}

/// <summary>The application of a <see cref="BufferFill"/> to the buffers of one solve.</summary>
internal static class BufferFills
{
    /// <summary>Fills the scratch of the solve: <see cref="BufferFill.Zero"/> clears it, the others write their pattern into every element.</summary>
    public static void Scratch(this BufferFill fill, MemoryBuffer1D<double, Stride1D.Dense> doubles, MemoryBuffer1D<int, Stride1D.Dense> ints)
    {
        ArgumentNullException.ThrowIfNull(doubles);
        ArgumentNullException.ThrowIfNull(ints);
        if (fill == BufferFill.Zero)
        {
            doubles.MemSetToZero();
            ints.MemSetToZero();
            return;
        }

        doubles.CopyFromCPU([.. Enumerable.Repeat(Number(fill), (int)doubles.Length)]);
        ints.CopyFromCPU([.. Enumerable.Repeat(Integer(fill), (int)ints.Length)]);
    }

    /// <summary>Fills the outputs of the solve the solver is to write, every field of the state included.</summary>
    public static void Outputs(this BufferFill fill, MemoryBuffer1D<double, Stride1D.Dense> multipliers, MemoryBuffer1D<MixtureState, Stride1D.Dense> state,
                               MemoryBuffer1D<int, Stride1D.Dense> status, MemoryBuffer1D<int, Stride1D.Dense> iterations)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(iterations);
        if (fill == BufferFill.Zero)
        {
            multipliers.MemSetToZero();
            state.MemSetToZero();
            status.MemSetToZero();
            iterations.MemSetToZero();
            return;
        }

        multipliers.CopyFromCPU([.. Enumerable.Repeat(Number(fill), (int)multipliers.Length)]);
        status.CopyFromCPU([Integer(fill)]);
        iterations.CopyFromCPU([Integer(fill)]);
        state.CopyFromCPU([PoisonedState(Number(fill))]);
    }

    private static double Number(BufferFill fill) => fill == BufferFill.NotANumber ? double.NaN : 1.0e300;

    private static int Integer(BufferFill fill) => fill == BufferFill.NotANumber ? -77777 : int.MaxValue;

    private static MixtureState PoisonedState(double number)
    {
        object boxed = default(MixtureState);
        foreach (var property in typeof(MixtureState).GetProperties().Where(p => p.PropertyType == typeof(double)))
        {
            property.SetValue(boxed, number);
        }

        return (MixtureState)boxed;
    }
}
