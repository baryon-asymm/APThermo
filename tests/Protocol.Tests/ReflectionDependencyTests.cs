using System.Reflection;
using Xunit.Abstractions;

namespace APThermo.Protocol.Tests;

/// <summary>Exact reflection lookups contribute real, not diagnostic-only, dependencies.</summary>
public sealed class ReflectionDependencyTests(ITestOutputHelper output)
{
    /// <summary>Machine-inventoried direct reflection edges include both process-owner callers.</summary>
    [Fact]
    public void TheActualTreeRecordsDirectReflectionDependencies()
    {
        var edges = (from node in NodeAssemblies.CodeNodes
                     from type in NodeAssemblies.TypesOf(node)
                     from method in TypeShape.MethodsOf(type)
                     from target in ReflectedTypeReferences.Types(method)
                     let destination = NodeAssemblies.NodeOf(target)
                     where destination is not null && destination != node && !destination.IsDescendantOf(node)
                     select $"{node.RelativePath} -> {destination.RelativePath}: {type.FullName}.{method.Name} -> {target.FullName}")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(edges);
        Assert.Contains(edges, edge => edge.StartsWith("tests/Fixtures.Tests -> tests/Harness/ProcessOwner:", StringComparison.Ordinal));
        Assert.Contains(edges, edge => edge.StartsWith("tests/Fixtures.Tests/ProcessProbe -> tests/Harness/ProcessOwner:", StringComparison.Ordinal));
        foreach (var edge in edges)
        {
            output.WriteLine(edge);
        }
    }

    /// <summary>A literal passed to GetType binds to the actual tree type.</summary>
    [Fact]
    public void DirectReflectionLookupNamesAnInventoriedType()
    {
        var owner = NodeAssemblies.CodeNodes.Single(node => node.RelativePath == "tests/Fixtures.Tests");
        var type = NodeAssemblies.TypesOf(owner).Single(item => item.Name == "OwnerIdentityTests");
        Assert.NotNull(type.TypeInitializer);
        Assert.Contains("APThermo.Harness.ProcessOwner.ParentLifetime", NamesOf(type.TypeInitializer));
    }

    /// <summary>A real type name used only in a message does not create a dependency.</summary>
    [Fact]
    public void AMessageIsNotAReflectionLookupEvenInAMethodWithAnotherLookup()
    {
        var target = typeof(ReflectionDependencyTests).GetMethod(nameof(DiagnosticOnly), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(target);
        var names = NamesOf(target);
        Assert.DoesNotContain("APThermo.Harness.ProcessOwner.ParentLifetime", names);
    }

    private static string[] NamesOf(MethodBase target)
    {
        var helper = typeof(ReflectionDependencyTests).Assembly.GetType("APThermo.Protocol.Tests.ReflectedTypeReferences");
        Assert.NotNull(helper);
        var reader = helper.GetMethod("Names", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(reader);
        return [.. (IEnumerable<string>)reader.Invoke(null, [target])!];
    }

    private static Type? DiagnosticOnly()
    {
        GC.KeepAlive("APThermo.Harness.ProcessOwner.ParentLifetime");
        return typeof(string).Assembly.GetType("System.String", false);
    }
}
