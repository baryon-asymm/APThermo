using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AerospacePropellantThermodynamics.Protocol.Tests;

/// <summary>
/// A node of the tree: a directory holding both documents. <see cref="RelativePath"/> is the directory path from the tree root
/// with '/' separators, empty for the root; <see cref="AssemblyName"/> is the name of the project in the directory, if any.
/// </summary>
internal sealed record Node(string RelativePath, string Directory, string? AssemblyName)
{
    public string Name => RelativePath.Length == 0 ? "the root" : RelativePath;

    public string Boot => Path.Combine(Directory, "BOOT.md");

    public string Api => Path.Combine(Directory, "API.md");

    public bool IsDescendantOf(Node other) =>
        other.RelativePath.Length == 0 ? RelativePath.Length > 0 : RelativePath.StartsWith(other.RelativePath + "/", StringComparison.Ordinal);
}

/// <summary>One instruction of a method body: the opcode and the member its token names, when it names one.</summary>
internal readonly record struct Instruction(OpCode Code, MemberInfo? Operand);

/// <summary>
/// The tree as the checks see it: the root found from this source file, the nodes found by directory path, the assemblies of the
/// nodes that have a project, and the reflection walks the checks share (the shape of a type, the instructions of a method body).
/// </summary>
internal static class Tree
{
    /// <summary>Directories never read as nodes: build output, tool caches, the agent's session directory (.claude holds
    /// worktrees of other sessions, each a full copy of the tree), and the protocol kit's templates (the linter's --exclude templates).</summary>
    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal)
    {
        ".git", ".vs", ".claude", "bin", "obj", ".venv", "__pycache__", "TestResults", "artifacts", "node_modules", "templates",
    };

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Lazy<string> RootLazy = new(FindRoot);

    private static readonly Lazy<IReadOnlyList<Node>> NodesLazy = new(FindNodes);

    private static readonly Lazy<IReadOnlyDictionary<Node, Assembly>> AssembliesLazy = new(LoadAssemblies);

    private static readonly Dictionary<byte, OpCode> OneByte = OpCodeTable(single: true);

    private static readonly Dictionary<byte, OpCode> TwoByte = OpCodeTable(single: false);

    /// <summary>The directory holding AGENTS.md, found upward from this file (AGENTS.md §13: from the source, never from the binary).</summary>
    public static string Root => RootLazy.Value;

    /// <summary>Every node of the tree, ordered by path; the root first.</summary>
    public static IReadOnlyList<Node> Nodes => NodesLazy.Value;

    /// <summary>The assemblies of the nodes that have a project, loaded by the name the project gives them.</summary>
    public static IReadOnlyDictionary<Node, Assembly> Assemblies => AssembliesLazy.Value;

    /// <summary>The node whose project built the assembly, or null for an assembly from outside the tree.</summary>
    public static Node? NodeOf(Assembly assembly) => Assemblies.FirstOrDefault(pair => pair.Value == assembly).Key;

    public static Node? NodeOf(Type type) => NodeOf(type.Assembly);

    /// <summary>A test assembly references xunit; its public types are its tests, listed by its BOOT.md, not by API.md.</summary>
    public static bool IsTestAssembly(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Any(reference => reference.Name is { } name && name.StartsWith("xunit", StringComparison.Ordinal));

    public static string Relative(string path)
    {
        var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
        return relative == "." ? string.Empty : relative;
    }

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

            foreach (var local in Body(method)?.LocalVariables ?? [])
            {
                yield return ($"{method.Name} local {local.LocalIndex}", local.LocalType);
            }
        }
    }

    /// <summary>
    /// Every type the given type mentions, in its shape and in the bodies of its methods (the members its instructions name and
    /// the generic arguments of the methods it calls), unwrapped from arrays, references, pointers and generic arguments; generic
    /// parameters dropped. Types from any assembly: the callers keep the ones they care about.
    /// </summary>
    public static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var seen = new HashSet<Type>();
        foreach (var candidate in Shape(type).Select(pair => pair.Type).Concat(BodyTypes(type)))
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

    /// <summary>
    /// The instructions of a method body. The operand width of every opcode is read from the runtime's own table
    /// (<see cref="OpCodes"/>), not transcribed: a wrong width would desynchronise the walk and yield garbage instead of failing.
    /// </summary>
    public static IEnumerable<Instruction> Instructions(MethodBase method)
    {
        var il = Body(method)?.GetILAsByteArray() ?? [];
        var typeContext = method.DeclaringType is { IsGenericTypeDefinition: true } declaring ? declaring.GetGenericArguments() : null;
        var methodContext = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
        var module = method.Module;
        var offset = 0;
        while (offset < il.Length)
        {
            var code = il[offset++];
            OpCode opcode;
            if (code == 0xFE)
            {
                if (offset >= il.Length || !TwoByte.TryGetValue(il[offset++], out opcode))
                {
                    yield break;
                }
            }
            else if (!OneByte.TryGetValue(code, out opcode))
            {
                yield break;
            }

            var width = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, offset)),
                _ => 4,
            };

            MemberInfo? operand = null;
            if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                try
                {
                    operand = module.ResolveMember(BitConverter.ToInt32(il, offset), typeContext, methodContext);
                }
                catch (Exception)
                {
                    // A token this context cannot resolve names nothing attributable to a node; the walk stays in step either way.
                }
            }

            offset += width;
            yield return new Instruction(opcode, operand);
        }
    }

    /// <summary>The nodes a BOOT.md declares in its `## Dependencies` section: every link resolving to a node's API.md, and the links that resolve to none.</summary>
    public static (IReadOnlySet<Node> Nodes, IReadOnlyList<string> Unresolved) DeclaredDependencies(Node node)
    {
        var boot = File.ReadAllText(node.Boot).ReplaceLineEndings("\n");
        var section = Regex.Match(boot, @"^## Dependencies\s*$(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        var declared = new HashSet<Node>();
        var unresolved = new List<string>();
        if (!section.Success)
        {
            return (declared, unresolved);
        }

        var byDirectory = Nodes.ToDictionary(n => Path.GetFullPath(n.Directory), n => n, StringComparer.OrdinalIgnoreCase);
        foreach (Match link in Regex.Matches(section.Groups[1].Value, @"\]\(([^)\s]+)\)"))
        {
            var target = link.Groups[1].Value;
            if (!target.EndsWith("API.md", StringComparison.Ordinal))
            {
                continue;
            }

            var directory = Path.GetFullPath(Path.Combine(node.Directory, Path.GetDirectoryName(target) ?? string.Empty));
            if (byDirectory.TryGetValue(directory, out var found))
            {
                declared.Add(found);
            }
            else
            {
                unresolved.Add(target);
            }
        }

        return (declared, unresolved);
    }

    /// <summary>
    /// The types the bodies of a type's methods bind to: the members their instructions name, with the types those members'
    /// signatures carry. The signatures matter: an enum used only through its literals is an integer in the IL and appears in no
    /// token of its own, yet the property it is assigned to names it, and that is where the use is caught.
    /// </summary>
    private static IEnumerable<Type> BodyTypes(Type type)
    {
        foreach (var method in MethodsOf(type))
        {
            foreach (var instruction in Instructions(method))
            {
                switch (instruction.Operand)
                {
                    case Type named:
                        yield return named;
                        break;
                    case MethodBase called:
                        if (called.DeclaringType is { } owner)
                        {
                            yield return owner;
                        }

                        if (called is MethodInfo { IsGenericMethod: true } generic)
                        {
                            foreach (var argument in generic.GetGenericArguments())
                            {
                                yield return argument;
                            }
                        }

                        if (called is MethodInfo info)
                        {
                            yield return info.ReturnType;
                        }

                        foreach (var parameter in called.GetParameters())
                        {
                            yield return parameter.ParameterType;
                        }

                        break;
                    case FieldInfo field:
                        if (field.DeclaringType is { } holder)
                        {
                            yield return holder;
                        }

                        yield return field.FieldType;
                        break;
                    case MemberInfo member:
                        if (member.DeclaringType is { } declaring)
                        {
                            yield return declaring;
                        }

                        break;
                }
            }
        }
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        var bare = type.IsByRef || type.IsArray || type.IsPointer ? type.GetElementType() ?? type : type;
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

    private static MethodBody? Body(MethodBase method)
    {
        try
        {
            return method.GetMethodBody();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Dictionary<byte, OpCode> OpCodeTable(bool single)
    {
        var table = new Dictionary<byte, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode)
            {
                continue;
            }

            var value = (ushort)opcode.Value;
            if (single ? value <= 0xFF && opcode.Value != 0xFE : value > 0xFF)
            {
                table[(byte)(value & 0xFF)] = opcode;
            }
        }

        return table;
    }

    private static string FindRoot()
    {
        var directory = Path.GetDirectoryName(ThisFile());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "AGENTS.md")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("the tree root (a directory with AGENTS.md) was not found above " + ThisFile());
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static IReadOnlyList<Node> FindNodes()
    {
        var nodes = new List<Node>();
        Walk(Root);
        return nodes.OrderBy(node => node.RelativePath, StringComparer.Ordinal).ToList();

        void Walk(string directory)
        {
            if (File.Exists(Path.Combine(directory, "BOOT.md")) && File.Exists(Path.Combine(directory, "API.md")))
            {
                var projects = System.IO.Directory.GetFiles(directory, "*.csproj");
                if (projects.Length > 1)
                {
                    throw new InvalidOperationException($"{Relative(directory)} holds {projects.Length} projects; a node has one assembly (root BOOT.md)");
                }

                nodes.Add(new Node(Relative(directory), directory, projects.Length == 1 ? Path.GetFileNameWithoutExtension(projects[0]) : null));
            }

            foreach (var child in System.IO.Directory.GetDirectories(directory))
            {
                if (!Skipped.Contains(Path.GetFileName(child)))
                {
                    Walk(child);
                }
            }
        }
    }

    private static IReadOnlyDictionary<Node, Assembly> LoadAssemblies()
    {
        var assemblies = new Dictionary<Node, Assembly>();
        foreach (var node in Nodes.Where(node => node.AssemblyName is not null))
        {
            try
            {
                assemblies[node] = Assembly.Load(new AssemblyName(node.AssemblyName!));
            }
            catch (FileNotFoundException e)
            {
                throw new InvalidOperationException(
                    $"the assembly of {node.Name} ({node.AssemblyName}) is not in the build output of Protocol.Tests: " +
                    "add a project reference to it in this node's project file", e);
            }
        }

        return assemblies;
    }
}
