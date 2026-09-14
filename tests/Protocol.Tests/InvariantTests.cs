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
        var problems = Numerical().SelectMany(pair => SinglePrecisionProblems(pair.Node, pair.Assembly)).ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: double precision only.\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Only_the_execution_node_and_its_tests_name_cuda_types()
    {
        var problems = NodeAssemblies.Assemblies.OrderBy(pair => pair.Key.RelativePath, StringComparer.Ordinal)
            .Where(pair => !CudaNodes.Contains(pair.Key.RelativePath))
            .SelectMany(pair => CudaProblems(pair.Key, pair.Value))
            .ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Taboos: no CUDA type outside the execution node.\n" + string.Join("\n", problems.Distinct()));
    }

    [Fact]
    public void Numerical_nodes_have_no_mutable_static_field()
    {
        var problems = Numerical().SelectMany(pair => MutableStaticFieldProblems(pair.Node, pair.Assembly)).ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: no hidden state.\n" + string.Join("\n", problems));
    }

    private static IEnumerable<(Node Node, Assembly Assembly)> Numerical()
    {
        foreach (var path in NumericalNodes)
        {
            var node = Tree.Nodes.SingleOrDefault(candidate => candidate.RelativePath == path)
                       ?? throw new InvalidOperationException($"{path} is not a node of the tree; the list of numerical nodes is stale");
            yield return (node, NodeAssemblies.Assemblies[node]);
        }
    }

    private static IEnumerable<string> SinglePrecisionProblems(Node node, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var (where, referenced) in TypeShape.Shape(type))
            {
                if (IsSinglePrecision(referenced))
                {
                    yield return $"{node.Name}: {type.FullName}, {where} is {referenced.Name}";
                }
            }

            foreach (var method in TypeShape.MethodsOf(type))
            {
                var opcodes = IlBody.Instructions(method).Select(instruction => instruction.Code.Name!).Where(name => name.Contains(".r4", StringComparison.Ordinal)).Distinct().ToList();
                if (opcodes.Count > 0)
                {
                    yield return $"{node.Name}: {type.FullName}.{method.Name} performs single-precision operations ({string.Join(", ", opcodes)})";
                }
            }
        }
    }

    private static IEnumerable<string> CudaProblems(Node node, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var referenced in TypeShape.ReferencedTypes(type))
            {
                if (referenced.Namespace is { } ns && CudaNamespaces.Any(cuda => ns == cuda || ns.StartsWith(cuda + ".", StringComparison.Ordinal)))
                {
                    yield return $"{node.Name}: {type.FullName} names {referenced.FullName}";
                }
            }
        }
    }

    private static IEnumerable<string> MutableStaticFieldProblems(Node node, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (TypeShape.IsCompilerGenerated(type))
            {
                continue;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.IsLiteral || TypeShape.IsCompilerGenerated(field))
                {
                    continue;
                }

                if (!field.IsInitOnly)
                {
                    yield return $"{node.Name}: {type.FullName}.{field.Name} is a static field that is neither const nor readonly";
                }
                else if (field.FieldType.IsArray)
                {
                    yield return $"{node.Name}: {type.FullName}.{field.Name} is a static readonly array, whose elements are mutable state";
                }
            }
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
