using APThermo.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Equilibrium.Tests;

/// <summary>Guards the recovery call boundary where a loop-live plan previously escaped by reference.</summary>
public sealed class LoopLiveBoundaryTests
{
    /// <summary>The actual solve passes a fresh per-iteration local by reference to the non-inlined recovery stage.</summary>
    [Fact]
    public void RecoveryCallDoesNotTakeTheAddressOfALoopLiveLocal()
    {
        var path = RepositoryPaths.Resolve("src", "Equilibrium", "EquilibriumSolver.cs");
        Assert.True(UsesPerIterationReference(File.ReadAllText(path)),
            "EquilibriumSolver.Solve must pass a per-iteration local, not a loop-live local, by ref to AttemptPlan.Next");
    }

    /// <summary>The guard accepts a fresh copy and rejects direct passage of the loop-live plan.</summary>
    [Theory]
    [InlineData("var copy = plan; AttemptPlan.Next(ref copy);", true)]
    [InlineData("AttemptPlan.Next(ref plan);", false)]
    [InlineData("ref var copy = ref plan; AttemptPlan.Next(ref copy);", false)]
    [InlineData("Other.Next(ref plan);", false)]
    public void ControlsDistinguishLivePlanFromAnIterationCopy(string body, bool expected)
    {
        var source = "class EquilibriumSolver { static void Solve() { int plan = 0; while (true) { " + body + " } } }";
        Assert.Equal(expected, UsesPerIterationReference(source));
    }

    private static bool UsesPerIterationReference(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var methods = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == "EquilibriumSolver")
            .SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>())
            .Where(method => method.Identifier.ValueText == "Solve").ToArray();
        if (methods.Length != 1)
        {
            return false;
        }

        var calls = methods[0].DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is MemberAccessExpressionSyntax member &&
                member.Expression.ToString() == "AttemptPlan" && member.Name.Identifier.ValueText == "Next").ToArray();
        if (calls.Length != 1 || calls[0].Ancestors().OfType<WhileStatementSyntax>().SingleOrDefault() is not { } loop)
        {
            return false;
        }

        var arguments = calls[0].ArgumentList.Arguments.Where(argument => argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)).ToArray();
        return arguments.Length == 1 && arguments[0].Expression is IdentifierNameSyntax name &&
            loop.Statement.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Any(variable => variable.Identifier.ValueText == name.Identifier.ValueText && variable.SpanStart < calls[0].SpanStart &&
                    variable.Initializer?.Value is not RefExpressionSyntax);
    }
}
