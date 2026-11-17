using System.Reflection;

namespace APThermo.Protocol.Tests;

/// <summary>xUnit's automatically invoked instance and fixture setup for a test class.</summary>
internal sealed class ProcessSetup(IEnumerable<Assembly> assemblies)
{
    private readonly Dictionary<string, Type[]> _collections =
        assemblies.SelectMany(assembly => assembly.GetTypes())
            .Select(type => (Type: type, Name: AttributeName(type, "Xunit.CollectionDefinitionAttribute")))
            .Where(item => item.Name is not null)
            .GroupBy(item => item.Name!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.SelectMany(item => Fixtures(item.Type, typeof(ICollectionFixture<>))).ToArray(),
                          StringComparer.Ordinal);

    public IEnumerable<MethodBase> Methods(Type? testType)
    {
        if (testType is null)
        {
            yield break;
        }

        foreach (var method in Lifecycle(testType))
        {
            yield return method;
        }

        var fixtures = Fixtures(testType, typeof(IClassFixture<>));
        var name = AttributeName(testType, "Xunit.CollectionAttribute");
        if (name is not null && _collections.TryGetValue(name, out var shared))
        {
            fixtures = fixtures.Concat(shared);
        }

        foreach (var fixture in fixtures.Distinct())
        {
            foreach (var method in Lifecycle(fixture))
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<Type> Fixtures(Type type, Type contract) =>
        type.GetInterfaces().Where(item => item.IsGenericType && item.GetGenericTypeDefinition() == contract)
            .Select(item => item.GetGenericArguments()[0]);

    private static string? AttributeName(Type type, string name) => type.GetCustomAttributesData()
        .Where(attribute => attribute.AttributeType.FullName == name)
        .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
        .FirstOrDefault();

    private static IEnumerable<MethodBase> Lifecycle(Type type)
    {
        foreach (var current in TypeAndBases(type))
        {
            foreach (var constructor in current.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            {
                yield return constructor;
            }

            if (current.TypeInitializer is { } initializer)
            {
                yield return initializer;
            }
        }

        foreach (var contract in new[] { typeof(IAsyncLifetime), typeof(IDisposable) })
        {
            if (contract.IsAssignableFrom(type))
            {
                foreach (var method in type.GetInterfaceMap(contract).TargetMethods)
                {
                    yield return method;
                }
            }
        }
    }

    private static IEnumerable<Type> TypeAndBases(Type type)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            yield return current;
        }
    }
}
