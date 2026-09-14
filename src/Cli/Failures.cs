using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>
/// The exception → exit code rule: <see cref="InputException"/> is exit code 2; an accelerator failure and every
/// unexpected exception are exit code 3, so a defect of this node is no longer mistaken for invalid input (F-CL-13).
/// The library's own refusals are translated into <see cref="InputException"/> where the library is called, not here.
/// </summary>
internal static class Failures
{
    public static ExitCode Handle(Exception e, TextWriter error)
    {
        if (e is InputException)
        {
            error.WriteLine(e.Message);
            return ExitCode.InvalidInput;
        }

        if (e is AcceleratorUnavailableException accelerator)
        {
            error.WriteLine(accelerator.Message);
            foreach (var path in accelerator.PathsTried)
            {
                error.WriteLine($"  tried {path}");
            }

            return ExitCode.Infrastructure;
        }

        error.WriteLine($"{e.GetType().Name}: {e.Message}");
        return ExitCode.Infrastructure;
    }
}
