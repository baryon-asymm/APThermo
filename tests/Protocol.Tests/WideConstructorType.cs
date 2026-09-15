namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// One type named by a `parameters` row of a node's `## Shape exceptions` table whose member is that type's own constructor
/// (`Type.Type` in the row's `Where`, or `Outer.Inner.Inner` for a nested type: the last two dotted segments equal, since a
/// constructor's member name is the type's own simple name). <see cref="Node"/> is the node whose `BOOT.md` declares the row,
/// so that the type's namespace is <c>Node.AssemblyName</c>; <see cref="SimpleName"/> is the type's own simple name, unqualified
/// and undotted. Two different nodes may declare a row for the same simple name (`RocketBatchViews` in `Execution` and in
/// `Performance.Tests`): each is its own, distinct <see cref="WideConstructorType"/>. `NamedConstruction`'s own vocabulary for
/// the "named construction" rule (`tests/Protocol.Tests/BOOT.md`, "Shape check").
/// </summary>
internal readonly record struct WideConstructorType(Node Node, string SimpleName);
