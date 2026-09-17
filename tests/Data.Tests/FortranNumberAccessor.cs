using System.Reflection;

namespace APThermo.Data.Tests;

/// <summary>Reaches the internal number reader of the node without widening its public surface.</summary>
internal static class FortranNumberAccessor
{
    private static readonly MethodInfo ParseMethod =
        typeof(SpeciesDatabase).Assembly.GetType("APThermo.Data.FortranNumber")!
            .GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)!;

    public static double Parse(string field)
    {
        try
        {
            return (double)ParseMethod.Invoke(null, [field])!;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }
}
