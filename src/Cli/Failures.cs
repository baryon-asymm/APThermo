using APThermo.Execution;

namespace APThermo.Cli;

/// <summary>
/// The exception → exit code rule: <see cref="InputException"/> is exit code 2; an accelerator failure, an I/O
/// failure this node did not translate to <see cref="InputException"/>, and, through <see cref="Program.Main"/>'s
/// unhandled-exception handler, every other exception are exit code 3, so a defect of this node is no longer
/// mistaken for invalid input (F-CL-13). The library's own refusals are translated into <see cref="InputException"/>
/// where the library is called, not here.
/// </summary>
/// <remarks>
/// Root BOOT.md, Constraints, Diagnostics (2026-09-24): CA1031 forbids <see cref="Program.Run"/> from catching
/// <see cref="Exception"/> itself, so it catches only the types this class has a named overload for
/// (<see cref="InputException"/>, <see cref="AcceleratorUnavailableException"/>) plus <see cref="IOException"/> and
/// <see cref="UnauthorizedAccessException"/>, both reported through <see cref="Unhandled"/>. Every exception outside
/// that list leaves <see cref="Program.Run"/>; a real process still reports it through <see cref="Unhandled"/>, from
/// the process-wide handler <see cref="Program.Main"/> installs, and ends with exit code 3.
/// </remarks>
internal static class Failures
{
    /// <summary>An input refusal: the message alone, exit code 2.</summary>
    public static ExitCode Handle(InputException e, TextWriter error)
    {
        error.WriteLine(e.Message);
        return ExitCode.InvalidInput;
    }

    /// <summary>An accelerator that could not be created: the message and every path tried, exit code 3.</summary>
    public static ExitCode Handle(AcceleratorUnavailableException e, TextWriter error)
    {
        error.WriteLine(e.Message);
        foreach (var path in e.PathsTried)
        {
            error.WriteLine($"  tried {path}");
        }

        return ExitCode.Infrastructure;
    }

    /// <summary>
    /// An exception this node names no specific refusal for: its type and message on <paramref name="error"/>, exit
    /// code 3. Used directly by <see cref="Program.Run"/>'s <see cref="IOException"/> and
    /// <see cref="UnauthorizedAccessException"/> catches, and by <see cref="Program.Main"/>'s process-wide
    /// unhandled-exception handler for everything that leaves <see cref="Program.Run"/> uncaught.
    /// </summary>
    public static ExitCode Unhandled(Exception e, TextWriter error)
    {
        error.WriteLine($"{e.GetType().Name}: {e.Message}");
        return ExitCode.Infrastructure;
    }
}
