using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Root invariants that the root BOOT.md says are checked by reflection: double precision only and no mutable static field in the
/// numerical nodes, and no CUDA type outside the execution node. They read the same shapes and bodies as the dependency check.
/// </summary>
public sealed class InvariantTests
{
    /// <summary>The four numerical nodes the root <c>BOOT.md</c> names directly (Decomposition): the kernel-capable
    /// levels. <see cref="Numerical"/> generates the full list every fact reads from these four and the tree's own node
    /// list, so a future child node of one of them is found, not typed by hand (the guards audit's O4).</summary>
    public static readonly IReadOnlyList<string> NumericalNodes = ["src/Thermo", "src/Equilibrium", "src/Performance", "src/Transport"];

    /// <summary>The nodes allowed to name CUDA types: the execution node and its tests (root BOOT.md, Invariants and Taboos).</summary>
    public static readonly IReadOnlyList<string> CudaNodes = ["src/Execution", "tests/Execution.Tests"];

    /// <summary>The namespaces of ILGPU that exist only for NVIDIA hardware.</summary>
    public static readonly IReadOnlyList<string> CudaNamespaces = ["ILGPU.Runtime.Cuda", "ILGPU.Backends.PTX"];

    /// <summary>Numerical nodes hold no single-precision (`float` or `Half`) value or operation (root BOOT.md, Invariants).</summary>
    [Fact]
    public void NumericalNodesHoldNoSinglePrecisionValueOrOperation()
    {
        var problems = Numerical().SelectMany(pair => SinglePrecisionProblems(pair.Node, pair.Assembly)).ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: double precision only.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Numerical nodes' own source files name no <c>float</c>, <c>Half</c>, <c>System.Single</c> or <c>MathF</c>, and carry
    /// no numeric literal with an <c>f</c> or <c>F</c> suffix (root BOOT.md, Invariants: double precision only; the guards
    /// audit's F4). The IL-level fact above stays: it sees a conversion, while the compiler folds a <c>float</c> constant
    /// into a plain <c>double</c> load (<c>ldc.r8</c>) no IL walk can tell apart from a literal written as a double.
    /// </summary>
    [Fact]
    public void NumericalNodeSourcesNameNoSinglePrecisionSyntax()
    {
        var scanned = Numerical().SelectMany(pair => SourceSyntax.Trees(pair.Node).Select(entry => (pair.Node, entry.Tree))).ToList();
        Assert.NotEmpty(scanned);
        var problems = scanned.SelectMany(pair => SinglePrecisionSyntaxProblems(pair.Node, pair.Tree)).ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: double precision only, at the source level.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// No numerical node's own sources put a literal or a <c>const</c> on the left of an ordered floating-point comparison
    /// (root BOOT.md, Constraints, the third ILGPU defect of 2026-09-28): ILGPU 1.5.3 moves a constant-left operand of such
    /// a comparison to the right and inverts its NaN ordering while doing so, so the comparison's truth on a NaN input
    /// depends on which side of the source the constant was written. Read from the semantic model of a compilation over
    /// each numerical node's own files (<see cref="ConstantLeftComparisons"/>), since a syntax walk alone cannot tell a
    /// floating-point operand from an integer one.
    /// </summary>
    [Fact]
    public void NumericalNodeSourcesPutNoConstantLeftOfAnOrderedFloatingComparison()
    {
        var scanned = Numerical().ToList();
        Assert.NotEmpty(scanned);
        var problems = scanned.SelectMany(pair => ConstantLeftComparisons.Problems(pair.Node, [.. SourceSyntax.Files(pair.Node)])).ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Constraints: no constant left of an ordered floating-point comparison.\n" + string.Join("\n", problems));
    }

    /// <summary>Only the execution node and its own tests name a CUDA type (root BOOT.md, Invariants: the CPU path needs no
    /// NVIDIA software).</summary>
    [Fact]
    public void OnlyTheExecutionNodeAndItsTestsNameCudaTypes()
    {
        var problems = NodeAssemblies.Assemblies.OrderBy(pair => pair.Key.RelativePath, StringComparer.Ordinal)
            .Where(pair => !CudaNodes.Contains(pair.Key.RelativePath))
            .SelectMany(pair => CudaProblems(pair.Key, pair.Value))
            .ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Taboos: no CUDA type outside the execution node.\n" + string.Join("\n", problems.Distinct()));
    }

    /// <summary>Numerical nodes have no mutable static field (root BOOT.md, Invariants: no hidden state).</summary>
    [Fact]
    public void NumericalNodesHaveNoMutableStaticField()
    {
        var problems = Numerical().SelectMany(pair => MutableStaticFieldProblems(pair.Node, pair.Assembly)).ToList();
        Assert.True(problems.Count == 0, "root BOOT.md, Invariants: no hidden state.\n" + string.Join("\n", problems));
    }

    /// <summary>The double overloads of `System.Math` a numerical node may call anywhere (root BOOT.md, "Math in numerical
    /// nodes"): the IEEE operations both accelerators compute the same bits for, each a single instruction on CUDA. `Exp`,
    /// `Log`, `Log10` and `Pow` are not on the list (2026-10-05, item 13 of 0.2.2): the tree's own `KernelMath.Exp`, `Log` and
    /// `Pow` replaced them. Every one of them is accepted only when every one of its parameters is `double`, so an overload for
    /// another type of the same name (`Math.Abs(int)`) is refused like any unlisted member.</summary>
    private static readonly IReadOnlyList<string> AllowedMathMethods = ["Sqrt", "Abs", "Floor", "Ceiling"];

    /// <summary>The `System.Math` members that only `KernelMath` and the `Elementary` node (the tree's own elementary
    /// functions) may call: the fused multiply-add is the one place a product is fused with a sum.</summary>
    private static readonly IReadOnlyList<string> ConfinedMathMethods = ["FusedMultiplyAdd"];

    /// <summary>The static `System.Double` members a numerical node may call, and only inside `KernelMath` and `Elementary`
    /// (`double.Min`/`Max` are never on this list, root BOOT.md, the guards audit's F1). Only static members are read at
    /// all: `double.Equals`, `double.ToString` and `double.CompareTo` are instance methods the hand-written
    /// `IEquatable&lt;T&gt;` structs and an exception message call on the host side, never kernel code, and the root's math
    /// constraint is about the functions a kernel might run, not the formatting or equality of a result once it is off the GPU.</summary>
    private static readonly IReadOnlyList<string> ConfinedDoubleMethods = ["IsNaN", "IsNegative"];

    /// <summary>The `System.BitConverter` members a numerical node may call, and only inside `KernelMath` and `Elementary`:
    /// the bit conversions of a double (`mov.b64` on CUDA).</summary>
    private static readonly IReadOnlyList<string> ConfinedBitConverterMethods = ["DoubleToInt64Bits", "Int64BitsToDouble"];

    /// <summary>
    /// Numerical nodes call no member of `System.Math` or `System.Double` outside the root's allow-list (root BOOT.md,
    /// "Math in numerical nodes", 2026-09-28, the guards audit's F1): both accelerators must run the same comparisons,
    /// selections and functions, which only the tree's own functions and single IEEE instructions provide, and an unlisted member
    /// either compiles to a PTX instruction with different NaN behaviour (`Math.Min`/`Max`, `double.Min`/`Max`, all four
    /// compiling to the same `min.f64`/`max.f64` regardless of the declaring type the source names) or is not proven to
    /// compile for CUDA at all. Fails on an empty scan, so a broken walk over the numerical assemblies cannot pass
    /// silently.
    /// </summary>
    [Fact]
    public void NumericalNodesCallOnlyTheAllowedMathAndDoubleMembers()
    {
        var scanned = Numerical().ToList();
        Assert.NotEmpty(scanned);
        var methods = scanned.SelectMany(pair => pair.Assembly.GetTypes().SelectMany(TypeShape.MethodsOf)).ToList();
        Assert.NotEmpty(methods);
        var problems = scanned.SelectMany(pair => KernelMathProblems(pair.Node, pair.Assembly)).ToList();
        Assert.True(problems.Count == 0,
            "root BOOT.md, Constraints: only the double overloads of System.Math named in the allow-list, and " +
            "Math.FusedMultiplyAdd, double.IsNaN/IsNegative and the BitConverter bit conversions nowhere outside KernelMath and Elementary.\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// No method of any <c>src</c> assembly calls an ILGPU transfer (a method named <c>CopyToCPU…</c> or <c>CopyFromCPU…</c>,
    /// the <c>…UnsafeAsync</c> forms included) with a raw by-reference parameter, a <c>ref T</c> into host memory (root
    /// BOOT.md, the fourth hazard of the ILGPU constraint, 2026-10-01; <c>src/Execution/Chunks/BOOT.md</c>, "Host memory
    /// crosses into ILGPU pinned"): ILGPU 1.5.3 turns such a reference into a raw pointer without pinning it, and a
    /// compacting collection moves the array under the copy. The span overloads take their span <c>in</c>, which is a
    /// by-reference parameter too, and pin it; they are the sanctioned form, so a by-reference span is not refused. The
    /// walk covers every <c>src</c> assembly, not only the numerical ones, since the transfers live in the execution node,
    /// and it fails on an empty walk: with no call to any ILGPU transfer found it would prove nothing.
    /// </summary>
    [Fact]
    public void NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference()
    {
        var assemblies = NodeAssemblies.Assemblies
            .Where(pair => pair.Key.RelativePath.StartsWith("src/", StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .ToList();
        Assert.NotEmpty(assemblies);
        var calls = assemblies.SelectMany(IlgpuTransferCalls).ToList();
        Assert.True(calls.Count > 0, "no call to an ILGPU transfer (CopyToCPU*, CopyFromCPU*) was found in any src assembly: the walk proves nothing.");
        var problems = calls
            .Where(call => call.Callee.GetParameters().Any(IsRawReference))
            .Select(call => $"{call.Caller.FullName}.{call.Method.Name} calls {call.Callee.DeclaringType!.Name}.{call.Callee.Name}({string.Join(", ", call.Callee.GetParameters().Select(parameter => parameter.ParameterType.Name))})")
            .ToList();
        Assert.True(problems.Count == 0,
            "root BOOT.md, the fourth hazard of the ILGPU constraint: host memory crosses into ILGPU only through an overload that pins it " +
            "(the Span or array overloads), never as a ref into a managed array.\n" + string.Join("\n", problems));
    }

    /// <summary>Every call a method of the assembly makes to a method of an ILGPU assembly named <c>CopyToCPU…</c> or <c>CopyFromCPU…</c>.</summary>
    private static IEnumerable<(Type Caller, MethodBase Method, MethodBase Callee)> IlgpuTransferCalls(Assembly assembly) =>
        assembly.GetTypes().SelectMany(type => TypeShape.MethodsOf(type).SelectMany(method => IlBody.Instructions(method)
            .Select(instruction => instruction.Operand)
            .OfType<MethodBase>()
            .Where(callee => callee.DeclaringType is { } declaring && IsIlgpuTransfer(declaring, callee))
            .Select(callee => (type, method, callee))));

    private static bool IsIlgpuTransfer(Type declaring, MethodBase callee) =>
        declaring.Assembly.GetName().Name!.StartsWith("ILGPU", StringComparison.Ordinal)
        && (callee.Name.StartsWith("CopyToCPU", StringComparison.Ordinal) || callee.Name.StartsWith("CopyFromCPU", StringComparison.Ordinal));

    /// <summary>A by-reference parameter that is not a span: the <c>in Span&lt;T&gt;</c> and <c>in ReadOnlySpan&lt;T&gt;</c> of the pinning overloads are by-reference too.</summary>
    private static bool IsRawReference(ParameterInfo parameter) =>
        parameter.ParameterType.IsByRef && !IsSpan(parameter.ParameterType.GetElementType()!);

    private static bool IsSpan(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Span<>) || type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));

    /// <summary>The numerical nodes and every descendant of the four the root <c>BOOT.md</c> names (the guards audit's
    /// O4): generated from the tree's own node list rather than typed by hand, so a future child node of one of the
    /// four is found automatically instead of silently escaping every fact that reads this method.</summary>
    private static IEnumerable<(Node Node, Assembly Assembly)> Numerical()
    {
        foreach (var path in NumericalNodes)
        {
            var root = Tree.Nodes.SingleOrDefault(candidate => candidate.RelativePath == path)
                       ?? throw new InvalidOperationException($"{path} is not a node of the tree; the list of numerical nodes is stale");
            foreach (var node in Tree.Nodes.Where(candidate => candidate == root || candidate.IsDescendantOf(root)))
            {
                if (NodeAssemblies.AssemblyOf(node) is { } assembly)
                {
                    yield return (node, assembly);
                }
            }
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

                var boundSingle = IlBody.BoundTypes(method).Where(IsSinglePrecision).Select(bound => bound.Name).Distinct().ToList();
                if (boundSingle.Count > 0)
                {
                    yield return $"{node.Name}: {type.FullName}.{method.Name} binds to a single-precision type ({string.Join(", ", boundSingle)})";
                }
            }
        }
    }

    /// <summary>The source-level single-precision markers of one syntax tree: the <c>float</c> keyword, the identifiers
    /// <c>Half</c>, <c>Single</c> and <c>MathF</c> (bare or qualified, since a qualified name's rightmost identifier is one
    /// of these too), and a numeric literal token with an <c>f</c>/<c>F</c> suffix that is not a hexadecimal or binary
    /// literal's own trailing digit.</summary>
    private static IEnumerable<string> SinglePrecisionSyntaxProblems(Node node, SyntaxTree tree)
    {
        var root = tree.GetRoot();
        foreach (var predefined in root.DescendantNodes().OfType<PredefinedTypeSyntax>())
        {
            if (predefined.Keyword.IsKind(SyntaxKind.FloatKeyword))
            {
                yield return SyntaxProblem(node, tree, predefined.Span, "float");
            }
        }

        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (identifier.Identifier.Text is "Half" or "Single" or "MathF")
            {
                yield return SyntaxProblem(node, tree, identifier.Span, identifier.Identifier.Text);
            }
        }

        foreach (var token in root.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.NumericLiteralToken) && IsFloatLiteral(token.Text))
            {
                yield return SyntaxProblem(node, tree, token.Span, token.Text);
            }
        }
    }

    private static bool IsFloatLiteral(string text) =>
        text.Length > 0 && text[^1] is 'f' or 'F'
        && !text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        && !text.StartsWith("0b", StringComparison.OrdinalIgnoreCase);

    private static string SyntaxProblem(Node node, SyntaxTree tree, Microsoft.CodeAnalysis.Text.TextSpan span, string what)
    {
        var line = tree.GetLineSpan(span).StartLinePosition.Line + 1;
        return $"{node.Name}: {Tree.Relative(tree.FilePath!)}:{line} names {what}";
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

            foreach (var problem in StaticFieldProblems(node, type))
            {
                yield return problem;
            }

            foreach (var problem in StaticPropertyProblems(node, type))
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> StaticFieldProblems(Node node, Type type)
    {
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
            else if (!IsAllowedStaticReadonlyType(field.FieldType))
            {
                yield return $"{node.Name}: {type.FullName}.{field.Name} is a static readonly field of type {field.FieldType.Name}, not a primitive, string or enum";
            }
        }
    }

    /// <summary>Every static property with a setter, auto or not (root BOOT.md, Invariants: no hidden state; the guards
    /// audit's F3): a getter alone reads state owned elsewhere, but a setter is a place to hide it.</summary>
    private static IEnumerable<string> StaticPropertyProblems(Node node, Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (TypeShape.IsCompilerGenerated(property) || property.GetSetMethod(nonPublic: true) is null)
            {
                continue;
            }

            yield return $"{node.Name}: {type.FullName}.{property.Name} is a static property with a setter";
        }
    }

    /// <summary>The allow-list of a static readonly field's type (root BOOT.md, Invariants: no hidden state; the guards
    /// audit's F3): primitives, `string` and enums are the tree's own immutable value kinds; anything else (a mutable
    /// collection included) is state that outlives a call.</summary>
    private static bool IsAllowedStaticReadonlyType(Type type) => type.IsPrimitive || type == typeof(string) || type.IsEnum;

    /// <summary>
    /// The calls of one type's method bodies that the Constraint above forbids: a <c>System.Math</c> member outside the
    /// allow-list (any overload of <c>Math.Min</c> or <c>Math.Max</c> included) and, outside <c>KernelMath</c> and the
    /// <c>Elementary</c> node, <c>Math.FusedMultiplyAdd</c>, <c>double.IsNaN</c>, <c>double.IsNegative</c> and the two
    /// bit conversions of <c>BitConverter</c>. Split into small methods, one per level of the walk (type, method,
    /// instruction), so that no method nests deeper than the root's limit of 3.
    /// </summary>
    private static IEnumerable<string> KernelMathProblems(Node node, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var problem in KernelMathProblemsInType(node, type))
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> KernelMathProblemsInType(Node node, Type type)
    {
        var confined = type.FullName == "APThermo.Thermo.KernelMath" || type.Namespace == "APThermo.Thermo.Elementary";
        foreach (var method in TypeShape.MethodsOf(type))
        {
            foreach (var problem in KernelMathProblemsInMethod(node, type, method, confined))
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> KernelMathProblemsInMethod(Node node, Type type, MethodBase method, bool confined)
    {
        foreach (var instruction in IlBody.Instructions(method))
        {
            if (instruction.Operand is MethodBase { DeclaringType: { } declaring } called && CallProblem(declaring, called, confined) is { } problem)
            {
                yield return $"{node.Name}: {type.FullName}.{method.Name} {problem}";
            }
        }
    }

    /// <summary>What is wrong with one call into <c>System.Math</c>, <c>System.Double</c> or <c>System.BitConverter</c>, or null:
    /// a call outside the allow-list, or a confined member outside <c>KernelMath</c> and <c>Elementary</c>
    /// (<paramref name="confined"/> is true inside them).</summary>
    private static string? CallProblem(Type declaring, MethodBase called, bool confined) =>
        declaring == typeof(Math)
            ? MathProblem(called, confined)
            : declaring == typeof(double) && called.IsStatic
                ? MemberProblem("double", called.Name, ConfinedDoubleMethods, confined)
                : declaring == typeof(BitConverter) ? MemberProblem("BitConverter", called.Name, ConfinedBitConverterMethods, confined) : null;

    private static string? MathProblem(MethodBase called, bool confined)
    {
        var allowed = AllowedMathMethods.Contains(called.Name, StringComparer.Ordinal);
        var isConfined = ConfinedMathMethods.Contains(called.Name, StringComparer.Ordinal);
        return !IsDoubleOverload(called) || !(allowed || isConfined)
            ? $"calls Math.{called.Name}, outside the allow-list"
            : isConfined && !confined ? $"calls Math.{called.Name} outside KernelMath and Elementary" : null;
    }

    private static string? MemberProblem(string type, string name, IReadOnlyList<string> confinedMembers, bool confined) =>
        !confinedMembers.Contains(name, StringComparer.Ordinal)
            ? $"calls {type}.{name}, outside the allow-list"
            : confined ? null : $"calls {type}.{name} outside KernelMath and Elementary";

    /// <summary>Whether every parameter of a `System.Math` overload is `double`: the allow-list names the double
    /// overload only, so a same-named overload of another type (`Math.Abs(Int32)`) is refused like any unlisted
    /// member.</summary>
    private static bool IsDoubleOverload(MethodBase method) => method.GetParameters().All(parameter => parameter.ParameterType == typeof(double));

    /// <summary>Whether a type is, or is built from, <c>float</c> or <c>Half</c> anywhere in its shape (root BOOT.md,
    /// Invariants: double precision only): the same unwrap <see cref="TypeShape.ReferencedTypes"/> uses
    /// (<see cref="TypeShape.Unwrap"/>), so a nested array, a by-reference-to-array parameter (<c>out float[]</c>) or a
    /// generic argument buried behind either cannot slip past a single, one-layer copy of the walk.</summary>
    private static bool IsSinglePrecision(Type type) => TypeShape.Unwrap(type).Any(bare => bare == typeof(float) || bare == typeof(Half));
}
