using System.Reflection;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// Root invariants that the root BOOT.md says are checked by reflection: double precision only and no mutable static field in the
/// numerical nodes, and no CUDA type outside the execution node. They read the same shapes and bodies as the dependency check.
/// </summary>
public sealed class InvariantTests
{
    /// <summary>The numerical nodes, the kernel-capable levels the root BOOT.md names (Decomposition).</summary>
    public static readonly IReadOnlyList<string> NumericalNodes = ["src/Thermo", "src/Equilibrium", "src/Performance", "src/Transport"];

    /// <summary>The nodes allowed to name CUDA types: the execution node and its tests (root BOOT.md, Invariants and Taboos).</summary>
    public static readonly IReadOnlyList<string> CudaNodes = ["src/Execution", "tests/Execution.Tests"];

    /// <summary>The namespaces of ILGPU that exist only for NVIDIA hardware.</summary>
    public static readonly IReadOnlyList<string> CudaNamespaces = ["ILGPU.Runtime.Cuda", "ILGPU.Backends.PTX"];

    [Fact]
    public void Numerical_nodes_hold_no_single_precision_value_or_operation()
    {
        var problems = new List<string>();
        foreach (var (node, assembly) in Numerical())
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var (where, referenced) in Tree.Shape(type))
                {
                    if (IsSinglePrecision(referenced))
                    {
                        problems.Add($"{node.Name}: {type.FullName}, {where} is {referenced.Name}");
                    }
                }

                foreach (var method in Tree.MethodsOf(type))
                {
                    var opcodes = Tree.Instructions(method).Select(instruction => instruction.Code.Name!).Where(name => name.Contains(".r4", StringComparison.Ordinal)).Distinct().ToList();
                    if (opcodes.Count > 0)
                    {
                        problems.Add($"{node.Name}: {type.FullName}.{method.Name} performs single-precision operations ({string.Join(", ", opcodes)})");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: double precision only.\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Only_the_execution_node_and_its_tests_name_cuda_types()
    {
        var problems = new List<string>();
        foreach (var (node, assembly) in Tree.Assemblies.OrderBy(pair => pair.Key.RelativePath, StringComparer.Ordinal))
        {
            if (CudaNodes.Contains(node.RelativePath))
            {
                continue;
            }

            foreach (var type in assembly.GetTypes())
            {
                foreach (var referenced in Tree.ReferencedTypes(type))
                {
                    if (referenced.Namespace is { } ns && CudaNamespaces.Any(cuda => ns == cuda || ns.StartsWith(cuda + ".", StringComparison.Ordinal)))
                    {
                        problems.Add($"{node.Name}: {type.FullName} names {referenced.FullName}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, "root BOOT.md, Taboos: no CUDA type outside the execution node.\n" + string.Join("\n", problems.Distinct()));
    }

    [Fact]
    public void Numerical_nodes_have_no_mutable_static_field()
    {
        var problems = new List<string>();
        foreach (var (node, assembly) in Numerical())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (Tree.IsCompilerGenerated(type))
                {
                    continue;
                }

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (field.IsLiteral || Tree.IsCompilerGenerated(field))
                    {
                        continue;
                    }

                    if (!field.IsInitOnly)
                    {
                        problems.Add($"{node.Name}: {type.FullName}.{field.Name} is a static field that is neither const nor readonly");
                    }
                    else if (field.FieldType.IsArray)
                    {
                        problems.Add($"{node.Name}: {type.FullName}.{field.Name} is a static readonly array, whose elements are mutable state");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: no hidden state.\n" + string.Join("\n", problems));
    }

    private static IEnumerable<(Node Node, Assembly Assembly)> Numerical()
    {
        foreach (var path in NumericalNodes)
        {
            var node = Tree.Nodes.SingleOrDefault(candidate => candidate.RelativePath == path)
                       ?? throw new InvalidOperationException($"{path} is not a node of the tree; the list of numerical nodes is stale");
            yield return (node, Tree.Assemblies[node]);
        }
    }

    private static bool IsSinglePrecision(Type type)
    {
        var bare = type.IsByRef || type.IsArray || type.IsPointer ? type.GetElementType() ?? type : type;
        if (bare == typeof(float) || bare == typeof(Half))
        {
            return true;
        }

        return bare.IsGenericType && bare.GetGenericArguments().Any(IsSinglePrecision);
    }
}
