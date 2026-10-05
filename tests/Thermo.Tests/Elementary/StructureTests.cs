using System.Reflection;
using APThermo.Thermo.Elementary;

namespace APThermo.Thermo.Tests.Elementary;

/// <summary>
/// The two structural promises of the node (BOOT.md, "No state", "One copy each"): the tables are switches over constants,
/// so no type of the node keeps a static field that is not a constant; and every elementary function and every one of its
/// accurate paths is reached through one method marked <c>NoInlining</c>, since ILGPU 1.5.3 inlines every function and the
/// fast paths inlined at the tree's call sites did not compile within twelve minutes (root <c>BOOT.md</c>, Compile size).
/// </summary>
public sealed class StructureTests
{
    /// <summary>The entry points that must not be inlined: each function and each of its accurate paths.</summary>
    private static readonly (Type Type, string Method)[] Entries =
    [
        (typeof(ExpFunction), nameof(ExpFunction.Exp)),
        (typeof(ExpFunction), nameof(ExpFunction.Accurate)),
        (typeof(LogFunction), nameof(LogFunction.Log)),
        (typeof(LogFunction), nameof(LogFunction.AccurateTd)),
        (typeof(PowFunction), nameof(PowFunction.Pow)),
        (typeof(PowFunction), nameof(PowFunction.Accurate)),
    ];

    /// <summary>Every static field of every type of the node is a constant.</summary>
    [Fact]
    public void TheNodeHasNoStaticStateOtherThanConstants()
    {
        var types = typeof(KernelMath).Assembly.GetTypes().Where(type => type.Namespace == typeof(ExpFunction).Namespace).ToList();
        Assert.True(types.Count >= 10, $"{types.Count} types found in the node's namespace");
        var offenders = types
            .SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => !field.IsLiteral)
            .Select(field => $"{field.DeclaringType!.Name}.{field.Name}")
            .ToList();
        Assert.True(offenders.Count == 0, "static fields that are not constants: " + string.Join(", ", offenders));
    }

    /// <summary>Each function and each accurate path carries NoInlining.</summary>
    [Fact]
    public void EveryElementaryEntryIsNotInlined()
    {
        foreach (var (type, name) in Entries)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.True(method.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining), $"{type.Name}.{name} is not marked NoInlining");
        }
    }
}
