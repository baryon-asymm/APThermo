using System.Reflection;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// Shape level: the root's code-shape constraint (`tests/Protocol.Tests/BOOT.md`, "Shape check") over the C# syntax trees and
/// the assemblies' IL of every node with a project. Five rules carry a numeric limit and a possible declared exception (type
/// lines, method lines, nesting, parameters, efferent coupling): a measurement over its limit needs a row of its own node's
/// `## Shape exceptions` table naming the same <c>Where</c> and <c>Rule</c> with a <c>Measured</c> figure at least the current
/// one, or the fact fails; the reverse fact below fails a row that no longer exceeds its limit or whose figure has fallen
/// behind. Stable type, stable dependencies, mechanics and named construction have their own pass conditions, spelled out at
/// each fact. The coupling and stable-type rules apply to the `src` nodes only, on which their thresholds were calibrated;
/// every other rule applies everywhere a project builds, this node's own code included.
/// </summary>
public sealed class ShapeTests
{
    [Fact]
    public void No_type_spans_more_than_400_lines()
    {
        var problems = ProjectNodes().SelectMany(node => OverLimitProblems(node, "type lines", 400, ShapeMeasures.TypeLines(node))).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_method_spans_more_than_60_lines()
    {
        var problems = ProjectNodes().SelectMany(node => OverLimitProblems(node, "method lines", 60, ShapeMeasures.MethodLines(node))).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_control_flow_nests_deeper_than_3()
    {
        var problems = ProjectNodes().SelectMany(node => OverLimitProblems(node, "nesting", 3, ShapeMeasures.Nesting(node))).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_method_takes_more_than_6_parameters()
    {
        var problems = ProjectNodes().SelectMany(node => OverLimitProblems(node, "parameters", 6, ShapeMeasures.Parameters(node))).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_src_type_names_more_than_14_types_of_the_tree()
    {
        var efferent = CouplingMeasures.EfferentCoupling();
        var problems = SrcNodes().SelectMany(node => CeProblems(node, efferent)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>"Shape check", stable type: a type named by ten or more types of the tree spans at most 100 lines of code,
    /// unless its node's `API.md` names it (a contract, left to review rather than measured here). No `## Shape exceptions`
    /// row applies to this rule; the escape is the document naming the type.</summary>
    [Fact]
    public void Every_stable_type_is_small_or_a_contract()
    {
        var afferent = CouplingMeasures.AfferentCoupling();
        var problems = SrcNodes().SelectMany(node => StableTypeProblems(node, afferent)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>"Shape check", stable dependencies: I = Ce / (Ca + Ce) of every `src` node over the declared dependency graph
    /// never rises along a declared dependency. No exception applies.</summary>
    [Fact]
    public void No_src_dependency_points_to_a_less_stable_node()
    {
        var coupling = CouplingMeasures.NodeCoupling();
        var problems = coupling.SelectMany(pair => DependencyProblems(pair.Key, pair.Value, coupling)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_partial_type_region_or_helpers_class()
    {
        var problems = ProjectNodes()
            .SelectMany(node => ShapeMechanics.Findings(node).Select(f => $"{node.Name}: {f.Where} ({Tree.Relative(f.File)}:{f.Line}) {f.Problem}"))
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>"Shape check", named construction: every creation resolving to a type with a declared parameters exception
    /// passes every argument as <c>name: value</c> (<see cref="ShapeMechanics.Constructions"/> does the resolution).</summary>
    [Fact]
    public void Every_wide_constructor_is_called_with_named_arguments()
    {
        var problems = ShapeMechanics.Constructions(WideConstructorTypes())
            .Where(c => !c.AllArgumentsNamed)
            .Select(c => $"{c.Node.Name}: {c.TypeName} created at {Tree.Relative(c.File)}:{c.Line} without naming every argument")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The reverse of the five over-limit facts above: every declared `## Shape exceptions` row still exceeds its
    /// rule's limit, and its own figure is not below the current measurement, so the tables cannot go stale in either
    /// direction ("Shape check": "the check fails ... when it exceeds its row's figure, and when a row's type or member no
    /// longer exceeds the limit").</summary>
    [Fact]
    public void Every_shape_exception_is_measured_and_still_needed()
    {
        var efferent = CouplingMeasures.EfferentCoupling();
        var problems = ProjectNodes()
            .SelectMany(node => NodeDocuments.ShapeExceptions(node).SelectMany(exception => RowProblems(node, exception, efferent)))
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<Node> ProjectNodes() => Tree.Nodes.Where(node => node.AssemblyName is not null);

    private static IEnumerable<Node> SrcNodes() => ProjectNodes().Where(node => node.RelativePath.StartsWith("src/", StringComparison.Ordinal));

    /// <summary>Every measurement over its limit, matched against the node's own declared rows by <c>Where</c>: unmatched
    /// (no row) or matched with a figure below the current measurement are both problems; a row that still covers the
    /// current measurement is silent here (<see cref="RowProblems"/> checks the row itself, the other way around).</summary>
    private static IEnumerable<string> OverLimitProblems(Node node, string rule, int limit, IEnumerable<(string Where, string File, int Line, int Measured)> measurements)
    {
        var rows = NodeDocuments.ShapeExceptions(node).Where(exception => exception.Rule == rule).ToList();
        foreach (var (where, file, line, measured) in measurements.Where(m => m.Measured > limit))
        {
            var row = rows.FirstOrDefault(candidate => candidate.Where == where);
            if (row.Where is null)
            {
                yield return $"{node.Name}: {where} ({Tree.Relative(file)}:{line}) measures {measured} for {rule}, over {limit}, and no row of {Tree.Relative(node.Boot)} declares it";
            }
            else if (row.Measured < measured)
            {
                yield return $"{node.Name}: {where} measures {measured} for {rule}, over its row's {row.Measured} ({Tree.Relative(node.Boot)})";
            }
        }
    }

    private static IEnumerable<string> CeProblems(Node node, IReadOnlyDictionary<Type, int> efferent)
    {
        var lines = ShapeMeasures.TypeLines(node).ToDictionary(t => t.Where, t => (t.File, t.Line));
        var measurements = efferent.Where(pair => NodeAssemblies.NodeOf(pair.Key) == node).Select(pair => CeMeasurement(pair.Key, pair.Value, lines));
        return OverLimitProblems(node, "efferent coupling", 14, measurements);
    }

    private static (string Where, string File, int Line, int Measured) CeMeasurement(Type type, int value, IReadOnlyDictionary<string, (string File, int Line)> lines)
    {
        var where = QualifiedName(type);
        var (file, line) = lines.TryGetValue(where, out var location) ? location : (Tree.Relative(type.Assembly.Location), 0);
        return (where, file, line, value);
    }

    private static IEnumerable<string> StableTypeProblems(Node node, IReadOnlyDictionary<Type, int> afferent)
    {
        var lines = ShapeMeasures.TypeLines(node).ToDictionary(t => t.Where, t => t.Lines);
        var api = File.ReadAllText(node.Api);
        foreach (var (type, count) in afferent.Where(pair => NodeAssemblies.NodeOf(pair.Key) == node && pair.Value >= 10))
        {
            var where = QualifiedName(type);
            if (ApiDeclarations.NamesType(api, TypeShape.SimpleName(type)) || !lines.TryGetValue(where, out var span) || span <= 100)
            {
                continue;
            }

            yield return $"{node.Name}: {where} is named by {count} types of the tree (a stable type) and spans {span} lines, over 100, without being named in {Tree.Relative(node.Api)}";
        }
    }

    private static IEnumerable<string> DependencyProblems(
        Node node, (int Ce, int Ca, IReadOnlySet<Node> Dependencies) coupling, IReadOnlyDictionary<Node, (int Ce, int Ca, IReadOnlySet<Node> Dependencies)> all)
    {
        var instability = Instability(coupling.Ce, coupling.Ca);
        foreach (var dependency in coupling.Dependencies)
        {
            var dependencyInstability = Instability(all[dependency].Ce, all[dependency].Ca);
            if (dependencyInstability > instability)
            {
                yield return $"{node.Name} (I={instability:F3}) depends on {dependency.Name} (I={dependencyInstability:F3}), which is less stable";
            }
        }
    }

    private static double Instability(int ce, int ca) => (double)ce / Math.Max(1, ce + ca);

    private static IReadOnlyCollection<WideConstructorType> WideConstructorTypes() => ProjectNodes()
        .SelectMany(node => NodeDocuments.ShapeExceptions(node).Where(exception => exception.Rule == "parameters" && IsConstructorPattern(exception.Where))
            .Select(exception => new WideConstructorType(node, exception.Where.Split('.')[^1])))
        .ToList();

    private static bool IsConstructorPattern(string where)
    {
        var segments = where.Split('.');
        return segments.Length >= 2 && segments[^1] == segments[^2];
    }

    private static IEnumerable<string> RowProblems(Node node, ShapeException exception, IReadOnlyDictionary<Type, int> efferent)
    {
        var current = exception.Rule switch
        {
            "parameters" => ShapeMeasures.Parameters(node).Where(p => p.Where == exception.Where).Select(p => (int?)p.Count).FirstOrDefault(),
            "efferent coupling" => CurrentCe(node, exception.Where, efferent),
            _ => null,
        };
        var limit = exception.Rule == "parameters" ? 6 : 14;
        if (current is not { } value)
        {
            yield return $"{Tree.Relative(node.Boot)}: the row for {exception.Where} ({exception.Rule}) names nothing this fact can re-measure";
        }
        else if (value <= limit)
        {
            yield return $"{Tree.Relative(node.Boot)}: {exception.Where} now measures {value} for {exception.Rule}, at or below the limit of {limit}, and no longer needs its row";
        }
        else if (exception.Measured < value)
        {
            yield return $"{Tree.Relative(node.Boot)}: {exception.Where}'s row states {exception.Measured} for {exception.Rule}, below the current measurement of {value}";
        }
    }

    private static int? CurrentCe(Node node, string where, IReadOnlyDictionary<Type, int> efferent)
    {
        if (!NodeAssemblies.Assemblies.TryGetValue(node, out var assembly))
        {
            return null;
        }

        var type = assembly.GetTypes().FirstOrDefault(candidate => QualifiedName(candidate) == where);
        return type is null ? null : efferent.GetValueOrDefault(type);
    }

    private static string QualifiedName(Type type)
    {
        var names = new List<string>();
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            names.Insert(0, TypeShape.SimpleName(current));
        }

        return string.Join(".", names);
    }
}
