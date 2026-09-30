using System.Text.Json;

namespace APThermo.Harness;

/// <summary>
/// The field-by-field comparison of two JSON documents that every runner holds a command-line example's delivered
/// document to, the hosted ones included, where an exact byte comparison belongs to the reference machine: the same
/// members in the same order, every string and boolean equal, every number within
/// <see cref="RelativeNumberTolerance"/> relative. The docs tests node and the workflow step that runs the packed
/// tool's example both call it, so the rule exists once (the harness node's `BOOT.md`).
/// </summary>
public static class JsonFieldComparison
{
    /// <summary>Four orders of magnitude above the measured platform difference (about 1e-13, root `BOOT.md`'s
    /// Documentation ⚠ of 2026-09-29) and five below the fixtures' own tolerance rows.</summary>
    public const double RelativeNumberTolerance = 1e-9;

    /// <summary>
    /// Null when <paramref name="approved"/> and <paramref name="actual"/> agree field by field; otherwise the first
    /// difference found, walking the document depth-first, named by its JSON path from the root (<paramref name="path"/>,
    /// <c>"$"</c> for the whole document).
    /// </summary>
    public static string? Mismatch(JsonElement approved, JsonElement actual, string path) =>
        approved.ValueKind != actual.ValueKind
            ? $"{path}: kind {approved.ValueKind} approved, {actual.ValueKind} delivered"
            : approved.ValueKind switch
            {
                JsonValueKind.Object => ObjectMismatch(approved, actual, path),
                JsonValueKind.Array => ArrayMismatch(approved, actual, path),
                JsonValueKind.Number => NumberMismatch(approved, actual, path),
                JsonValueKind.String => approved.GetString() == actual.GetString()
                    ? null
                    : $"{path}: '{approved.GetString()}' approved, '{actual.GetString()}' delivered",
                JsonValueKind.True or JsonValueKind.False => approved.GetBoolean() == actual.GetBoolean()
                    ? null
                    : $"{path}: {approved.GetBoolean()} approved, {actual.GetBoolean()} delivered",
                JsonValueKind.Null or JsonValueKind.Undefined => null,   // the ValueKind check above already agreed
                _ => throw new ArgumentOutOfRangeException(nameof(approved), approved.ValueKind, "no such JSON value kind"),
            };

    /// <summary>The same member names, in the same order, each recursively equal.</summary>
    private static string? ObjectMismatch(JsonElement approved, JsonElement actual, string path)
    {
        var approvedMembers = approved.EnumerateObject().ToArray();
        var actualMembers = actual.EnumerateObject().ToArray();
        if (approvedMembers.Length != actualMembers.Length)
        {
            return $"{path}: {approvedMembers.Length} member(s) approved, {actualMembers.Length} delivered";
        }

        for (var i = 0; i < approvedMembers.Length; i++)
        {
            if (approvedMembers[i].Name != actualMembers[i].Name)
            {
                return $"{path}: member {i} is '{approvedMembers[i].Name}' approved, '{actualMembers[i].Name}' delivered";
            }

            var mismatch = Mismatch(approvedMembers[i].Value, actualMembers[i].Value, $"{path}.{approvedMembers[i].Name}");
            if (mismatch is not null)
            {
                return mismatch;
            }
        }

        return null;
    }

    /// <summary>The same length, each element recursively equal in order.</summary>
    private static string? ArrayMismatch(JsonElement approved, JsonElement actual, string path)
    {
        var approvedItems = approved.EnumerateArray().ToArray();
        var actualItems = actual.EnumerateArray().ToArray();
        if (approvedItems.Length != actualItems.Length)
        {
            return $"{path}: {approvedItems.Length} element(s) approved, {actualItems.Length} delivered";
        }

        for (var i = 0; i < approvedItems.Length; i++)
        {
            var mismatch = Mismatch(approvedItems[i], actualItems[i], $"{path}[{i}]");
            if (mismatch is not null)
            {
                return mismatch;
            }
        }

        return null;
    }

    /// <summary>Equal within <see cref="RelativeNumberTolerance"/> relative to the larger magnitude, or exactly equal
    /// at zero.</summary>
    private static string? NumberMismatch(JsonElement approved, JsonElement actual, string path)
    {
        var expected = approved.GetDouble();
        var delivered = actual.GetDouble();
        var scale = Math.Max(Math.Abs(expected), Math.Abs(delivered));
        var difference = Math.Abs(expected - delivered);
        return difference <= scale * RelativeNumberTolerance
            ? null
            : $"{path}: {expected} approved, {delivered} delivered, {difference / scale:e3} relative";
    }
}
