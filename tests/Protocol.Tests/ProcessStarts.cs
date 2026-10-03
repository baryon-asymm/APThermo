using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Which test methods start a process, read from the IL (root <c>BOOT.md</c>, Test time budgets: a fact that starts a process
/// carries <c>Category=EndToEnd</c>). A test method starts a process when the static call graph from it, through the methods of
/// the <c>tests</c> nodes' assemblies, reaches a call of <c>Process.Start</c> or a construction of a <c>ProcessStartInfo</c>:
/// the harness's <c>DotnetProcess.Run</c> and this node's own Python runner are found that way, by their bodies, not by name.
/// A lambda is reached through the method its delegate names, an async or iterator method through its state machine; a
/// virtual call is not resolved, and a call into a <c>src</c> assembly is not followed (what a node under test starts is
/// that node's own matter, and the walk over the tests nodes alone stays small).
/// </summary>
internal static class ProcessStarts
{
    private const string FactAttribute = "Xunit.FactAttribute";

    private const string TraitAttribute = "Xunit.TraitAttribute";

    private static readonly Lazy<HashSet<MethodKey>> ReachingLazy = new(MethodsReachingAProcess);

    /// <summary>Every test method (<c>[Fact]</c>, <c>[Theory]</c> and the attributes derived from them) of every test assembly of the tree.</summary>
    public static IEnumerable<MethodBase> TestMethods() =>
        TestsNodeAssemblies().Where(NodeAssemblies.IsTestAssembly)
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(TypeShape.MethodsOf)
            .Where(IsTestMethod);

    /// <summary>Whether the test method reaches a call of <c>Process.Start</c> or a <c>ProcessStartInfo</c> construction.</summary>
    public static bool StartsAProcess(MethodBase test) => ReachingLazy.Value.Contains(Key(test));

    /// <summary>Whether the method, its type, an enclosing type or a base type carries <c>[Trait("Category", category)]</c>.</summary>
    public static bool CarriesCategory(MethodBase test, string category)
    {
        var attributes = test.GetCustomAttributesData().Concat(TypesAround(test.DeclaringType).SelectMany(type => type.GetCustomAttributesData()));
        return attributes.Any(attribute => attribute.AttributeType.FullName == TraitAttribute
            && attribute.ConstructorArguments is [{ Value: "Category" }, { Value: string value }] && value == category);
    }

    private static IEnumerable<Type> TypesAround(Type? type)
    {
        for (var outer = type; outer is not null; outer = outer.DeclaringType)
        {
            for (var current = outer; current is not null; current = current.BaseType)
            {
                yield return current;
            }
        }
    }

    private static IEnumerable<Assembly> TestsNodeAssemblies() =>
        NodeAssemblies.Assemblies.Where(pair => pair.Key.RelativePath.StartsWith("tests/", StringComparison.Ordinal)).Select(pair => pair.Value);

    private static bool IsTestMethod(MethodBase method)
    {
        foreach (var attribute in method.GetCustomAttributesData())
        {
            for (var type = attribute.AttributeType; type is not null; type = type.BaseType)
            {
                if (type.FullName == FactAttribute)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The methods of the tests nodes' assemblies from which a process start is reachable: the callers of the sinks, closed backwards.</summary>
    private static HashSet<MethodKey> MethodsReachingAProcess()
    {
        var callersOf = new Dictionary<MethodKey, List<MethodKey>>();
        var reaching = new HashSet<MethodKey>();
        foreach (var method in TestsNodeAssemblies().SelectMany(assembly => assembly.GetTypes()).SelectMany(TypeShape.MethodsOf))
        {
            foreach (var callee in Callees(method))
            {
                Record(Key(method), callee, callersOf, reaching);
            }
        }

        var pending = new Queue<MethodKey>(reaching);
        while (pending.TryDequeue(out var reached))
        {
            foreach (var caller in callersOf.GetValueOrDefault(reached) ?? [])
            {
                if (reaching.Add(caller))
                {
                    pending.Enqueue(caller);
                }
            }
        }

        return reaching;
    }

    /// <summary>One edge of the call graph: a sink marks its caller as reaching a process start, any other callee gets the caller recorded.</summary>
    private static void Record(MethodKey caller, MethodBase callee, Dictionary<MethodKey, List<MethodKey>> callersOf, HashSet<MethodKey> reaching)
    {
        if (IsProcessStart(callee))
        {
            _ = reaching.Add(caller);
            return;
        }

        var key = Key(callee);
        if (!callersOf.TryGetValue(key, out var callers))
        {
            callers = [];
            callersOf[key] = callers;
        }

        callers.Add(caller);
    }

    /// <summary>The methods a body names (calls, delegate targets, constructions), and the state machine of an async or iterator method.</summary>
    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        foreach (var instruction in IlBody.Instructions(method))
        {
            if (instruction.Operand is MethodBase callee)
            {
                yield return callee;
            }
        }

        foreach (var attribute in method.GetCustomAttributesData())
        {
            if (attribute.AttributeType.Name is "AsyncStateMachineAttribute" or "IteratorStateMachineAttribute"
                && attribute.ConstructorArguments is [{ Value: Type machine }]
                && machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } moveNext)
            {
                yield return moveNext;
            }
        }
    }

    private static bool IsProcessStart(MethodBase callee) =>
        callee.DeclaringType?.FullName switch
        {
            "System.Diagnostics.Process" => callee.Name == "Start",
            "System.Diagnostics.ProcessStartInfo" => callee.IsConstructor,
            _ => false,
        };

    private static MethodKey Key(MethodBase method) => new(method.Module, method.MetadataToken);

    /// <summary>A method as its module and metadata token name it: the same method however reflection reached it, a constructed generic instance included.</summary>
    private readonly record struct MethodKey(Module Module, int Token);
}
