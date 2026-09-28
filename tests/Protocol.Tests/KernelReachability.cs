using System.Reflection;
using System.Reflection.Emit;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The call graph reachable from the execution node's kernel entry points (its internal <c>Kernels</c> type, the registry
/// `src/Execution/BOOT.md` names), walked through the tree's own assemblies only: an external call (the BCL, ILGPU) carries
/// no method body this walk can read, and stops there on its own. The guards audit's O6: no hosted fact inspected a
/// kernel-reached method for a <c>throw</c>, an allocation (<c>newarr</c>, or a <c>newobj</c> of a reference type) or a
/// <c>box</c> before this, so a kernel-incompatible statement only failed at the reference machine's own PTX compile.
/// </summary>
internal static class KernelReachability
{
    private const string KernelsTypeName = "APThermo.Execution.Kernels";

    /// <summary>Every problem found in a method the walk reaches from a kernel entry point, one per offending instruction.</summary>
    public static IEnumerable<string> Problems()
    {
        var visited = new HashSet<MethodBase>();
        foreach (var entry in EntryPoints())
        {
            foreach (var problem in Walk(entry, entry, visited))
            {
                yield return problem;
            }
        }
    }

    /// <summary>The methods of the execution node's <c>Kernels</c> type: the tree's only kernel entry points.</summary>
    public static IEnumerable<MethodBase> EntryPoints()
    {
        foreach (var assembly in NodeAssemblies.Assemblies.Values.Distinct())
        {
            if (assembly.GetType(KernelsTypeName, throwOnError: false) is { } kernels)
            {
                foreach (var method in TypeShape.MethodsOf(kernels))
                {
                    yield return method;
                }
            }
        }
    }

    private static IEnumerable<string> Walk(MethodBase entry, MethodBase method, HashSet<MethodBase> visited)
    {
        if (!visited.Add(method))
        {
            yield break;
        }

        foreach (var instruction in IlBody.Instructions(method))
        {
            foreach (var problem in InstructionProblems(entry, method, instruction))
            {
                yield return problem;
            }

            if (Reachable(instruction) is { } called)
            {
                foreach (var problem in Walk(entry, called, visited))
                {
                    yield return problem;
                }
            }
        }
    }

    /// <summary>The called method of an instruction, when it names one whose declaring type belongs to a node of the tree
    /// (a call into the BCL or ILGPU carries no body this walk can read, and is left alone).</summary>
    private static MethodBase? Reachable(Instruction instruction) =>
        instruction.Operand is MethodBase called && called.DeclaringType is { } declaring && NodeAssemblies.NodeOf(declaring) is not null
            ? called
            : null;

    private static IEnumerable<string> InstructionProblems(MethodBase entry, MethodBase method, Instruction instruction)
    {
        if (instruction.Code == OpCodes.Throw)
        {
            yield return $"{Where(entry, method)}: throws";
        }
        else if (instruction.Code == OpCodes.Newarr)
        {
            yield return $"{Where(entry, method)}: allocates an array (newarr)";
        }
        else if (instruction.Code == OpCodes.Box)
        {
            yield return $"{Where(entry, method)}: boxes a value (box)";
        }
        else if (instruction.Code == OpCodes.Newobj && instruction.Operand is MethodBase constructor
                 && constructor.DeclaringType is { IsValueType: false } declaring)
        {
            yield return $"{Where(entry, method)}: allocates a reference type ({declaring.FullName}, newobj)";
        }
    }

    private static string Where(MethodBase entry, MethodBase method)
    {
        var type = method.DeclaringType!;
        var node = NodeAssemblies.NodeOf(type)?.Name ?? type.Assembly.GetName().Name;
        return $"{node}: {type.FullName}.{method.Name}, reached from Kernels.{entry.Name}";
    }
}
