using System.Reflection;

namespace APThermo.Performance.Tests;

/// <summary>Generated numeric inventory for the finite-figure invariant.</summary>
internal static class FigureChecks
{
    internal static IEnumerable<PropertyInfo> NumericProperties() =>
        typeof(PerformanceFigures).GetProperties().Where(p => p.PropertyType == typeof(double));

    internal static IReadOnlyList<string> NonFinite(in PerformanceFigures figures, string label)
    {
        var violations = new List<string>();
        foreach (var property in NumericProperties())
        {
            var value = (double)property.GetValue(figures)!;
            if (!double.IsFinite(value))
            {
                violations.Add($"{label}: {property.Name} {value:R} is not finite");
            }
        }

        return violations;
    }
}
