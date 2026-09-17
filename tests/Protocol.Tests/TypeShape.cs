using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>
/// What a type names: in its declarations (base type, interfaces, members) and in the bodies of its methods (via
/// <see cref="IlBody"/>), its outermost declaring type, and what the compiler generated rather than the author.
/// </summary>
internal static class TypeShape
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The name without the generic arity suffix.</summary>
    public static string SimpleName(Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`');
        return arity < 0 ? name : name[..arity];
    }

    /// <summary>The type that holds a nested or compiler-generated type, up to the top level.</summary>
    public static Type Outermost(Type type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    /// <summary>Written by the compiler or a source generator (the regex generator marks its classes GeneratedCode), not by the author.</summary>
    public static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name is "CompilerGeneratedAttribute" or "EmbeddedAttribute" or "GeneratedCodeAttribute");

    /// <summary>Every method body a type owns: methods, constructors and the type initializer, declared on the type itself.</summary>
    public static IEnumerable<MethodBase> MethodsOf(Type type)
    {
        foreach (var method in type.GetMethods(Declared))
        {
            yield return method;
        }

        foreach (var constructor in type.GetConstructors(Declared))
        {
            yield return constructor;
        }

        if (type.TypeInitializer is { } initializer)
        {
            yield return initializer;
        }
    }

    /// <summary>
    /// The types in the shape of a type, each with the place it appears: the base type, the interfaces, the types of its fields,
    /// properties, events, method returns and parameters, and the locals of its method bodies. Not unwrapped.
    /// </summary>
    public static IEnumerable<(string Where, Type Type)> Shape(Type type)
    {
        if (type.BaseType is { } baseType)
        {
            yield return ("base type", baseType);
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return ("interface", contract);
        }

        foreach (var field in type.GetFields(Declared))
        {
            yield return ($"field {field.Name}", field.FieldType);
        }

        foreach (var property in type.GetProperties(Declared))
        {
            yield return ($"property {property.Name}", property.PropertyType);
        }

        foreach (var @event in type.GetEvents(Declared))
        {
            if (@event.EventHandlerType is { } handler)
            {
                yield return ($"event {@event.Name}", handler);
            }
        }

        foreach (var method in MethodsOf(type))
        {
            if (method is MethodInfo info)
            {
                yield return ($"{method.Name} returns", info.ReturnType);
            }

            foreach (var parameter in method.GetParameters())
            {
                yield return ($"{method.Name}({parameter.Name})", parameter.ParameterType);
            }

            foreach (var local in IlBody.Locals(method))
            {
                yield return ($"{method.Name} local {local.LocalIndex}", local.LocalType);
            }
        }
    }

    /// <summary>
    /// Every type the given type mentions, in its shape and in the bodies of its methods (the members its instructions name and
    /// the generic arguments of the methods it calls, read by <see cref="IlBody"/>), unwrapped from arrays, references, pointers
    /// and generic arguments; generic parameters dropped. Types from any assembly: the callers keep the ones they care about.
    /// </summary>
    public static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var seen = new HashSet<Type>();
        var candidates = Shape(type).Select(pair => pair.Type).Concat(MethodsOf(type).SelectMany(IlBody.BoundTypes));
        foreach (var candidate in candidates)
        {
            foreach (var unwrapped in Unwrap(candidate))
            {
                if (!unwrapped.IsGenericParameter && seen.Add(unwrapped))
                {
                    yield return unwrapped;
                }
            }
        }
    }

    /// <summary>A type stripped of arrays, references and pointers (nested ones included, an array of pointers to an array
    /// unwrapped down to its element), followed by the same unwrapping of each of its generic arguments, recursively. The
    /// tree's one walk from a type as written to the leaf types it is built from: <see cref="ReferencedTypes"/> uses it to
    /// find what a signature names, and <c>InvariantTests</c> uses it to find whether a signature or a body ever names
    /// <c>float</c> or <c>Half</c>, wherever in the shape they are buried.</summary>
    public static IEnumerable<Type> Unwrap(Type type)
    {
        var bare = type;
        while ((bare.IsByRef || bare.IsArray || bare.IsPointer) && bare.GetElementType() is { } element)
        {
            bare = element;
        }

        yield return bare;
        if (!bare.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in bare.GetGenericArguments())
        {
            foreach (var nested in Unwrap(argument))
            {
                yield return nested;
            }
        }
    }
}
