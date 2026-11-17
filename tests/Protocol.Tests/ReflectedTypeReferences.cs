using System.Reflection;
using System.Reflection.Emit;

namespace APThermo.Protocol.Tests;

/// <summary>Direct literal reflection lookups that resolve to actual tree types.</summary>
internal static class ReflectedTypeReferences
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> Inventory = new(() =>
        NodeAssemblies.Assemblies.Values.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.FullName is not null)
            .GroupBy(type => type.FullName!, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal));

    internal static IEnumerable<string> Names(MethodBase method)
    {
        var instructions = IlBody.Instructions(method).ToArray();
        for (var i = 0; i < instructions.Length; i++)
        {
            if (instructions[i].Operand is not MethodInfo call || !IsLookup(call))
            {
                continue;
            }

            var index = i - 1;
            while (index >= 0 && instructions[index].Code is var opcode &&
                   (opcode == OpCodes.Ldc_I4_0 || opcode == OpCodes.Ldc_I4_1))
            {
                index--;
            }

            if (index >= 0 && instructions[index].Code == OpCodes.Ldstr &&
                instructions[index].Literal is { } name && Inventory.Value.ContainsKey(name))
            {
                yield return name;
            }
        }
    }

    internal static IEnumerable<Type> Types(MethodBase method) => Names(method).Select(name => Inventory.Value[name]);

    private static bool IsLookup(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return method.Name == "GetType" && method.ReturnType == typeof(Type) &&
            (method.DeclaringType == typeof(Assembly) || method.DeclaringType == typeof(Type)) &&
            parameters.Length > 0 && parameters[0].ParameterType == typeof(string) &&
            parameters.Skip(1).All(parameter => parameter.ParameterType == typeof(bool));
    }
}
