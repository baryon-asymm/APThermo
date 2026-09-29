using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The root's third ILGPU defect (root <c>BOOT.md</c>, Constraints, 2026-09-28): ILGPU 1.5.3 moves a constant left operand
/// of a floating-point ordered comparison (<c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>) to the right and inverts
/// its NaN ordering while doing so, so <c>1.0 &lt; v</c> is true for a NaN <c>v</c> on CUDA and false on the CPU. The rule
/// for a numerical node's own sources: no literal and no <c>const</c> stands on the left of an ordered comparison whose
/// operands are both floating-point. A syntax walk alone cannot tell a floating-point operand from an integer one, so this
/// fact builds a compilation over each numerical node's own files and reads the semantic model's own type for each operand.
/// </summary>
internal static class ConstantLeftComparisons
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp14);

    /// <summary>Every ordered floating-point comparison of a numerical node's own sources whose left operand is a literal
    /// or a <c>const</c>, one problem per occurrence, naming the file and line.</summary>
    public static IEnumerable<string> Problems(Node node, IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            yield break;
        }

        var compilation = Compile(node, files);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var comparison in tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>())
            {
                if (IsOrderedComparison(comparison) && IsConstantLeftOfFloatingComparison(comparison, model))
                {
                    var line = tree.GetLineSpan(comparison.Span).StartLinePosition.Line + 1;
                    yield return $"{node.Name}: {Tree.Relative(tree.FilePath!)}:{line}: '{comparison}' has a constant left of an " +
                                 "ordered floating-point comparison (the root's third ILGPU defect)";
                }
            }
        }
    }

    private static bool IsOrderedComparison(BinaryExpressionSyntax comparison) => comparison.Kind() is
        SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
        or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;

    private static bool IsConstantLeftOfFloatingComparison(BinaryExpressionSyntax comparison, SemanticModel model) =>
        IsConstant(comparison.Left, model) && IsFloatingPoint(comparison.Left, model) && IsFloatingPoint(comparison.Right, model);

    private static bool IsConstant(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is LiteralExpressionSyntax { Token.Value: not null })
        {
            return true;
        }

        var constant = model.GetConstantValue(expression);
        return constant.HasValue;
    }

    /// <summary>Whether the operand's type once the comparison's own implicit conversion is applied is <c>double</c> or
    /// <c>float</c>: <see cref="TypeInfo.ConvertedType"/>, not <see cref="TypeInfo.Type"/>. An integer constant compared
    /// against a floating-point operand (<c>0 &lt; v</c>, or a <c>const int</c> in the same place) keeps its own
    /// <c>int</c> type in <c>Type</c>, while the compiler converts it to the operand's floating-point type before the
    /// comparison runs (<c>ldc.r8 0; ldarg.0; clt</c>, the third audit pass's finding 4a) exactly as a literal already
    /// written in that type would; <c>ConvertedType</c> is what the emitted comparison actually operates on.</summary>
    private static bool IsFloatingPoint(ExpressionSyntax expression, SemanticModel model) =>
        model.GetTypeInfo(expression).ConvertedType?.SpecialType is SpecialType.System_Double or SpecialType.System_Single;

    private static CSharpCompilation Compile(Node node, IReadOnlyList<string> files)
    {
        var trees = files.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), ParseOptions, path));
        return CSharpCompilation.Create(node.AssemblyName ?? node.Name, trees, References(), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>Every distinct, non-dynamic assembly already loaded in this process (the tree's own projects, ILGPU, the
    /// .NET runtime): a best-effort umbrella wide enough for the semantic model to type every operand a numerical node's
    /// own sources can write, without reconstructing the exact project reference graph.</summary>
    private static IEnumerable<MetadataReference> References() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && assembly.Location.Length > 0)
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location));
}
