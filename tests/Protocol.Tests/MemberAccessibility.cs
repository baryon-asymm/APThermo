using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>
/// The declared accessibility of a member or a nested type, read the same way for every kind reflection reports one for: a
/// field or a method/constructor directly, a nested type through its own <c>IsNested*</c> flags, and a property or event
/// through the most visible of its own accessors. The public-surface snapshot (<see cref="SurfaceTests"/>) and the
/// tree-contract snapshot (<see cref="TreeContractSnapshotTests"/>) share this one reading and differ only in which levels
/// they keep (the guards audit's O7: the tree contract also lists <c>internal</c> and <c>protected internal</c> members).
/// </summary>
internal static class MemberAccessibility
{
    /// <summary>Whether a member's own declared accessibility is <c>public</c>: the public-surface snapshot's filter.</summary>
    public static bool IsPublic(MemberInfo member) => Level(member) == AccessibilityLevel.Public;

    /// <summary>Whether a member's own declared accessibility is <c>public</c>, <c>internal</c> or <c>protected internal</c>
    /// (a <c>private</c>, <c>protected</c> or <c>private protected</c> member crosses no assembly boundary and is not a
    /// friend's contract): the tree-contract snapshot's filter.</summary>
    public static bool IsPublicOrInternal(MemberInfo member) =>
        Level(member) is AccessibilityLevel.Public or AccessibilityLevel.Internal or AccessibilityLevel.ProtectedInternal;

    /// <summary>A leading keyword for a member's snapshot line, empty for <c>public</c> so that every line the
    /// public-surface snapshot already approved (public members only) reads exactly as it did before the tree-contract
    /// snapshot widened the floor to <see cref="IsPublicOrInternal"/> (the guards audit's O7).</summary>
    public static string Prefix(MemberInfo member) => Level(member) switch
    {
        AccessibilityLevel.Public => string.Empty,
        AccessibilityLevel.ProtectedInternal => "protected internal ",
        AccessibilityLevel.Internal => "internal ",
        AccessibilityLevel.Protected or AccessibilityLevel.PrivateProtected or AccessibilityLevel.Private =>
            throw new InvalidOperationException($"{member.DeclaringType}.{member.Name} is below the internal floor every caller filters to first"),
        _ => throw new InvalidOperationException($"{member.DeclaringType}.{member.Name} has no recognised accessibility level"),
    };

    private static AccessibilityLevel Level(MemberInfo member) => member switch
    {
        Type type => NestedLevel(type),
        FieldInfo field => FieldLevel(field),
        MethodBase method => MethodLevel(method),
        PropertyInfo property => AccessorLevel(property.GetAccessors(nonPublic: true)),
        EventInfo @event => AccessorLevel([@event.AddMethod, @event.RemoveMethod]),
        _ => AccessibilityLevel.Private,
    };

    private static AccessibilityLevel NestedLevel(Type type) =>
        type.IsNestedPublic ? AccessibilityLevel.Public
        : type.IsNestedFamORAssem ? AccessibilityLevel.ProtectedInternal
        : type.IsNestedAssembly ? AccessibilityLevel.Internal
        : type.IsNestedFamily ? AccessibilityLevel.Protected
        : type.IsNestedFamANDAssem ? AccessibilityLevel.PrivateProtected
        : AccessibilityLevel.Private;

    private static AccessibilityLevel FieldLevel(FieldInfo field) =>
        field.IsPublic ? AccessibilityLevel.Public
        : field.IsFamilyOrAssembly ? AccessibilityLevel.ProtectedInternal
        : field.IsAssembly ? AccessibilityLevel.Internal
        : field.IsFamily ? AccessibilityLevel.Protected
        : field.IsFamilyAndAssembly ? AccessibilityLevel.PrivateProtected
        : AccessibilityLevel.Private;

    private static AccessibilityLevel MethodLevel(MethodBase method) =>
        method.IsPublic ? AccessibilityLevel.Public
        : method.IsFamilyOrAssembly ? AccessibilityLevel.ProtectedInternal
        : method.IsAssembly ? AccessibilityLevel.Internal
        : method.IsFamily ? AccessibilityLevel.Protected
        : method.IsFamilyAndAssembly ? AccessibilityLevel.PrivateProtected
        : AccessibilityLevel.Private;

    /// <summary>The most visible of a property or event's own accessors: a public getter with an internal setter is a
    /// public member, the same reading <c>BindingFlags.Public</c> alone already gave the public-surface snapshot.</summary>
    private static AccessibilityLevel AccessorLevel(IEnumerable<MethodInfo?> accessors) =>
        accessors.Where(accessor => accessor is not null).Select(accessor => MethodLevel(accessor!))
            .DefaultIfEmpty(AccessibilityLevel.Private).Max();
}

/// <summary>The seven levels a member's declared accessibility resolves to, ordered least to most visible so that
/// <see cref="MemberAccessibility"/> can take the most visible of a property or event's own accessors with <c>Max()</c>.</summary>
internal enum AccessibilityLevel
{
    /// <summary>Visible to the declaring type only.</summary>
    Private,

    /// <summary>Visible to the declaring assembly and to a derived type, but only where both hold at once.</summary>
    PrivateProtected,

    /// <summary>Visible to the declaring type and its derived types.</summary>
    Protected,

    /// <summary>Visible to the declaring assembly and its recognised friends.</summary>
    Internal,

    /// <summary>Visible to the declaring assembly, its friends, or any derived type.</summary>
    ProtectedInternal,

    /// <summary>Visible everywhere the declaring type itself is visible.</summary>
    Public,
}
