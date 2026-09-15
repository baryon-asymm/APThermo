namespace APThermo.Protocol.Tests;

/// <summary>
/// One row of a node's `## Shape exceptions` table (`tests/Protocol.Tests/BOOT.md`, "Shape check"): <see cref="Where"/> is a
/// type's simple name (`Outer.Inner` for a nested type) or `Type.Member` for a member (a constructor named `Type.Type`),
/// <see cref="Rule"/> names a row of the "Shape check" table, <see cref="Measured"/> is the figure the check measures, and
/// <see cref="Reason"/> is the prose decision it cites. <see cref="NodeDocuments"/>'s own vocabulary.
/// </summary>
internal readonly record struct ShapeException(string Where, string Rule, int Measured, string Reason);
