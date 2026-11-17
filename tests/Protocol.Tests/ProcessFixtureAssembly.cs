using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace APThermo.Protocol.Tests;

/// <summary>Builds a real in-memory xUnit collection definition for setup graph controls.</summary>
internal static class ProcessFixtureAssembly
{
    public static MethodInfo Create(bool starts)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FixtureProbe" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Probe");
        var fixture = module.DefineType("Fixture", TypeAttributes.Public | TypeAttributes.Class);
        var constructor = fixture.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes).GetILGenerator();
        constructor.Emit(OpCodes.Ldarg_0);
        constructor.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        if (starts)
        {
            constructor.Emit(OpCodes.Ldstr, "controlled");
            constructor.Emit(OpCodes.Newobj, typeof(ProcessStartInfo).GetConstructor([typeof(string)])!);
            constructor.Emit(OpCodes.Pop);
        }

        constructor.Emit(OpCodes.Ret);
        var fixtureType = fixture.CreateType()!;
        var collection = module.DefineType("CollectionDefinition", TypeAttributes.Public | TypeAttributes.Class);
        collection.AddInterfaceImplementation(typeof(ICollectionFixture<>).MakeGenericType(fixtureType));
        SetName(collection, typeof(CollectionDefinitionAttribute), "fixture-probe");
        _ = collection.CreateType();
        var consumer = module.DefineType("Consumer", TypeAttributes.Public | TypeAttributes.Class);
        SetName(consumer, typeof(CollectionAttribute), "fixture-probe");
        var method = consumer.DefineMethod("Body", MethodAttributes.Public, typeof(void), Type.EmptyTypes).GetILGenerator();
        method.Emit(OpCodes.Ret);
        return consumer.CreateType()!.GetMethod("Body")!;
    }

    private static void SetName(TypeBuilder type, Type attribute, string name) =>
        type.SetCustomAttribute(new CustomAttributeBuilder(attribute.GetConstructor([typeof(string)])!, [name]));
}
