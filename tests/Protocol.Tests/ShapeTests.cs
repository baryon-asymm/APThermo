using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>
/// Shape level: the root's code-shape constraint (`tests/Protocol.Tests/BOOT.md`, "Shape check") over the C# syntax trees and
/// the assemblies' IL of every node whose code lives in one of the tree's assemblies (<see cref="NodeAssemblies.CodeNodes"/>:
/// a node with its own project, or a project-less child node compiled into its nearest ancestor's). Five rules carry a numeric
/// limit and a possible declared exception (type lines, method lines, nesting, parameters, efferent coupling): a measurement
/// over its limit needs a row of its own node's `## Shape exceptions` table naming the same <c>Where</c> and <c>Rule</c> with
/// a <c>Measured</c> figure at least the current one, or the fact fails; the reverse fact below fails a row that no longer
/// exceeds its limit or whose figure has fallen behind. Stable type, stable dependencies, mechanics and named construction
/// have their own pass conditions, spelled out at each fact. The coupling and stable-type rules apply to the `src` nodes
/// only, on which their thresholds were calibrated; every other rule applies everywhere a node's code lives, this node's own
/// code included.
/// </summary>
public sealed class ShapeTests
{
    [Fact]
    public void No_type_spans_more_than_400_lines()
    {
        Assert.True(MeasurementCount("type lines") > 0, "no type of the tree was measured for type lines; this fact has nothing to check");
        var problems = CodeNodes().SelectMany(node => OverLimitProblems(node, "type lines")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_method_spans_more_than_60_lines()
    {
        Assert.True(MeasurementCount("method lines") > 0, "no method of the tree was measured for method lines; this fact has nothing to check");
        var problems = CodeNodes().SelectMany(node => OverLimitProblems(node, "method lines")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_control_flow_nests_deeper_than_3()
    {
        Assert.True(MeasurementCount("nesting") > 0, "no member of the tree was measured for nesting; this fact has nothing to check");
        var problems = CodeNodes().SelectMany(node => OverLimitProblems(node, "nesting")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_method_takes_more_than_6_parameters()
    {
        Assert.True(MeasurementCount("parameters") > 0, "no method of the tree was measured for parameters; this fact has nothing to check");
        var problems = CodeNodes().SelectMany(node => OverLimitProblems(node, "parameters")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Asserts the `src` node set is not empty first (R-Protocol.Tests-14 (a)): with no `src` node, this fact and
    /// <see cref="Every_stable_type_is_small_or_a_contract"/> would both pass over zero nodes rather than over a real,
    /// checked set — silently, since a real violation elsewhere in a `src` node would then never be looked at.</summary>
    [Fact]
    public void No_src_type_names_more_than_14_types_of_the_tree()
    {
        Assert.True(SrcNodes().Any(), "no `src` node exists in the tree; this fact has nothing to check");
        var problems = SrcNodes().SelectMany(node => OverLimitProblems(node, "efferent coupling")).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>"Shape check", stable type: a type named by ten or more types of the tree spans at most 100 lines of code,
    /// unless its node's `API.md` names it (a contract, left to review rather than measured here). No `## Shape exceptions`
    /// row applies to this rule; the escape is the document naming the type. Asserts the `src` node set is not empty first
    /// (R-Protocol.Tests-14 (a)), the same reason <see cref="No_src_type_names_more_than_14_types_of_the_tree"/> does.</summary>
    [Fact]
    public void Every_stable_type_is_small_or_a_contract()
    {
        Assert.True(SrcNodes().Any(), "no `src` node exists in the tree; this fact has nothing to check");
        var afferent = CouplingMeasures.AfferentCoupling();
        var problems = SrcNodes().SelectMany(node => StableTypeProblems(node, afferent)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>"Shape check", stable dependencies: I = Ce / (Ca + Ce) of every `src` node over the declared dependency graph
    /// never rises along a declared dependency. No exception applies. Asserts the graph has at least one declared dependency
    /// edge first (R-Protocol.Tests-14 (b)): with `NodeCoupling()` empty (no `src` node, or none with a declared dependency),
    /// this fact would pass over zero edges rather than over the real graph.</summary>
    [Fact]
    public void No_src_dependency_points_to_a_less_stable_node()
    {
        var coupling = CouplingMeasures.NodeCoupling();
        Assert.True(coupling.Values.Sum(value => value.Dependencies.Count) > 0, "the src node graph has no declared dependency edge; this fact has nothing to check");
        var problems = coupling.SelectMany(pair => DependencyProblems(pair.Key, pair.Value, coupling)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void No_partial_type_region_or_helpers_class()
    {
        Assert.True(CodeNodes().Any(node => SourceSyntax.Trees(node).Any()), "no source file of the tree was read for the mechanics rule; this fact has nothing to check");
        var problems = CodeNodes()
            .SelectMany(node => ShapeMechanics.Findings(node).Select(f => $"{node.Name}: {f.Where} ({Tree.Relative(f.File)}:{f.Line}) {f.Problem}"))
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>"Shape check", named construction: every creation resolving to a type with a declared parameters exception
    /// passes every argument as <c>name: value</c> (<see cref="NamedConstruction.Creations"/> does the resolution). Asserts
    /// the candidate list is not empty first (R-Protocol.Tests-14): with nothing to resolve creations against, the fact
    /// below would pass over zero creations rather than over a real, checked set.</summary>
    [Fact]
    public void Every_wide_constructor_is_called_with_named_arguments()
    {
        var candidates = NamedConstruction.Candidates();
        Assert.True(candidates.Count > 0, "no node declares a parameters row on its own constructor; this fact has nothing to check");

        var problems = NamedConstruction.Creations(candidates)
            .Where(c => !c.AllArgumentsNamed)
            .Select(c => $"{c.Node.Name}: {c.TypeName} created at {Tree.Relative(c.File)}:{c.Line} without naming every argument")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The reverse of the five over-limit facts above: every declared `## Shape exceptions` row still exceeds its
    /// rule's limit, and its own figure is not below the current measurement, so the tables cannot go stale in either
    /// direction ("Shape check": "the check fails ... when it exceeds its row's figure, and when a row's type or member no
    /// longer exceeds the limit"). Asserts the declared-row list is not empty first (R-Protocol.Tests-14): with no row
    /// anywhere in the tree, the fact below would pass over zero rows rather than over a real, checked set.</summary>
    [Fact]
    public void Every_shape_exception_is_measured_and_still_needed()
    {
        var exceptions = CodeNodes().SelectMany(node => NodeDocuments.ShapeExceptions(node).Select(exception => (Node: node, Exception: exception))).ToList();
        Assert.True(exceptions.Count > 0, "no node declares a Shape exceptions row anywhere in the tree; this fact has nothing to re-measure");

        var problems = exceptions.SelectMany(pair => RowProblems(pair.Node, pair.Exception)).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Every node whose code lives in one of the tree's assemblies (<see cref="NodeAssemblies.CodeNodes"/>): a node
    /// with its own project, or a project-less child node compiled into its nearest ancestor's (root BOOT.md, Constraints,
    /// 2026-09-15). The Shape level measures every one of them, this node's own code included.</summary>
    private static IEnumerable<Node> CodeNodes() => NodeAssemblies.CodeNodes;

    private static IEnumerable<Node> SrcNodes() => CodeNodes().Where(node => node.IsSrc);

    /// <summary>The total measurements <see cref="RuleMeasurements"/> reports for one over-limit rule, across every project
    /// node (R-Protocol.Tests-14 (e)): the population <see cref="OverLimitProblems"/> compares against its limit, asserted
    /// non-empty by the four size/nesting/parameters facts before they assert zero problems over it.</summary>
    private static int MeasurementCount(string rule) => CodeNodes().Sum(node => RuleMeasurements(node, rule).Measurements.Count());

    /// <summary>The limit and the current measurements of one over-limit "Shape check" rule, over one node: the same lookup
    /// <see cref="OverLimitProblems"/> compares a measurement against and <see cref="RowProblems"/> re-measures a declared
    /// row against, so the two facts can never read a rule's limit or its measurements differently. An unrecognised rule
    /// name (a stray row of a rule this lookup does not cover) yields no measurements, which <see cref="RowProblems"/> reads
    /// as "nothing to re-measure".</summary>
    private static (int Limit, IEnumerable<(string Where, string File, int Line, int Measured)> Measurements) RuleMeasurements(Node node, string rule) => rule switch
    {
        "type lines" => (400, ShapeMeasures.TypeLines(node).Select(m => (m.Where, m.File, m.Line, Measured: m.Lines))),
        "method lines" => (60, ShapeMeasures.MethodLines(node).Select(m => (m.Where, m.File, m.Line, Measured: m.Lines))),
        "nesting" => (3, ShapeMeasures.Nesting(node).Select(m => (m.Where, m.File, m.Line, Measured: m.Depth))),
        "parameters" => (6, ShapeMeasures.Parameters(node).Select(m => (m.Where, m.File, m.Line, Measured: m.Count))),
        "efferent coupling" => (14, EfferentMeasurements(node)),
        _ => (int.MaxValue, Enumerable.Empty<(string Where, string File, int Line, int Measured)>()),
    };

    private static IEnumerable<(string Where, string File, int Line, int Measured)> EfferentMeasurements(Node node)
    {
        var lines = ShapeMeasures.TypeLines(node).ToDictionary(t => t.Where, t => (t.File, t.Line));
        return CouplingMeasures.EfferentCoupling().Where(pair => NodeAssemblies.NodeOf(pair.Key) == node).Select(pair => CeMeasurement(pair.Key, pair.Value, lines));
    }

    /// <summary>Every measurement over its limit, matched against the node's own declared rows by <c>Where</c>: unmatched
    /// (no row) or matched with a figure below the current measurement are both problems; a row that still covers the
    /// current measurement is silent here (<see cref="RowProblems"/> checks the row itself, the other way around). A
    /// <c>Where</c> with more than one measurement (overloads, or a record's primary and an explicit constructor sharing
    /// <c>Type.Type</c>) is matched against each of its measurements in turn, so the worst one is never shadowed by a
    /// smaller one found first.</summary>
    private static IEnumerable<string> OverLimitProblems(Node node, string rule)
    {
        var (limit, measurements) = RuleMeasurements(node, rule);
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

    private static (string Where, string File, int Line, int Measured) CeMeasurement(Type type, int value, IReadOnlyDictionary<string, (string File, int Line)> lines)
    {
        var where = QualifiedName(type);
        var (file, line) = lines.TryGetValue(where, out var location) ? location : (Tree.Relative(type.Assembly.Location), 0);
        return (where, file, line, value);
    }

    /// <summary>Every Ca ≥ 10 type of a `src` node that is neither named in its `API.md` nor within the 100-line stable-type
    /// limit; also every such type this fact cannot even measure against the limit, because no syntax entry of its own node
    /// matches its reflection-read qualified name (R-Protocol.Tests-14: a lookup miss used to be silently treated as
    /// compliant, which would have let a real violation the syntax walk cannot see escape unreported).</summary>
    private static IEnumerable<string> StableTypeProblems(Node node, IReadOnlyDictionary<Type, int> afferent)
    {
        var lines = ShapeMeasures.TypeLines(node).ToDictionary(t => t.Where, t => t.Lines);
        var api = File.ReadAllText(node.Api);
        foreach (var (type, count) in afferent.Where(pair => NodeAssemblies.NodeOf(pair.Key) == node && pair.Value >= 10))
        {
            var where = QualifiedName(type);
            if (ApiDeclarations.NamesType(api, TypeShape.SimpleName(type)))
            {
                continue;
            }

            if (!lines.TryGetValue(where, out var span))
            {
                yield return $"{node.Name}: {where} is named by {count} types of the `src` nodes (a stable type) and matches no syntax entry of {Tree.Relative(node.Boot)}'s own node to measure its lines against";
            }
            else if (span > 100)
            {
                yield return $"{node.Name}: {where} is named by {count} types of the `src` nodes (a stable type) and spans {span} lines, over 100, without being named in {Tree.Relative(node.Api)}";
            }
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

    /// <summary>The reverse check for one declared row: re-measures every current measurement at the row's own
    /// <c>Where</c> (<see cref="RuleMeasurements"/>, the same lookup <see cref="OverLimitProblems"/> uses) and compares
    /// the row against the largest of them, so a row naming a record's primary constructor cannot be pronounced
    /// unnecessary by an explicit constructor of the same name that happens to be smaller.</summary>
    private static IEnumerable<string> RowProblems(Node node, ShapeException exception)
    {
        var (limit, measurements) = RuleMeasurements(node, exception.Rule);
        var matches = measurements.Where(m => m.Where == exception.Where).Select(m => m.Measured).ToList();
        if (matches.Count == 0)
        {
            yield return $"{Tree.Relative(node.Boot)}: the row for {exception.Where} ({exception.Rule}) names nothing this fact can re-measure";
            yield break;
        }

        var current = matches.Max();
        if (current <= limit)
        {
            yield return $"{Tree.Relative(node.Boot)}: {exception.Where} now measures {current} for {exception.Rule}, at or below the limit of {limit}, and no longer needs its row";
        }
        else if (exception.Measured < current)
        {
            yield return $"{Tree.Relative(node.Boot)}: {exception.Where}'s row states {exception.Measured} for {exception.Rule}, below the current measurement of {current}";
        }
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
