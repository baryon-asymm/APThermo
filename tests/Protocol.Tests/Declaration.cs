using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APThermo.Protocol.Tests;

/// <summary>Binds a documented C# block in the node namespace, using the built assemblies as references.
/// Actual members always come from the metadata image, even when the block declares a type of the same name.</summary>
internal static class Declaration
{
    private static readonly Lazy<(CSharpCompilation Compilation, Dictionary<string, SyntaxTree> Imports)> Metadata = new(CreateReferenceCompilation);

    private static readonly SymbolDisplayFormat Signature = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType |
            SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeConstantValue |
            SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeDefaultValue |
            SymbolDisplayParameterOptions.IncludeExtensionThis,
        propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Checks only the types and members shown in this block. Each error carries the source Markdown line.</summary>
    public static IEnumerable<string> Problems(string block, string defaultNamespace, string file, int startLine)
    {
        var (tree, root, model) = Bind(block, defaultNamespace);
        // Documentation can omit method bodies, base lists and implementations. Name resolution and
        // parameter defaults still have to be unambiguous to compare their compiled meaning.
        var errors = tree.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Concat(model.GetDiagnostics().Where(diagnostic => diagnostic.Id is "CS0104" or "CS0234" or "CS0246"
                or "CS1736" or "CS1750")).ToList();
        foreach (var error in errors)
        {
            yield return $"{file}:{startLine + error.Location.GetLineSpan().StartLinePosition.Line}: {error.GetMessage(CultureInfo.InvariantCulture)}";
        }

        if (errors.Count > 0)
        {
            yield break;
        }

        // Enumerate member declarations, never statements inside a method or accessor body.
        var declarations = root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or BaseTypeDeclarationSyntax)
            .OfType<MemberDeclarationSyntax>().Where(n => n is not BaseNamespaceDeclarationSyntax).ToList();
        if (declarations.Count == 0)
        {
            yield return $"{file}:{startLine}: implemented block contains no C# declarations";
        }

        foreach (var syntax in declarations)
        {
            var location = $"{file}:{startLine + syntax.GetLocation().GetLineSpan().StartLinePosition.Line}";
            foreach (var problem in CheckDeclaration(model, syntax))
            {
                yield return $"{location}: {problem}";
            }
        }
    }

    private static (SyntaxTree Tree, CompilationUnitSyntax Root, SemanticModel Model) Bind(string block, string defaultNamespace)
    {
        var tree = CSharpSyntaxTree.ParseText(block);
        var root = tree.GetCompilationUnitRoot();
        if (!root.Members.Any(member => member is BaseNamespaceDeclarationSyntax))
        {
            // Keep usings and extern aliases at compilation-unit level (also supports global using).
            var container = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(defaultNamespace))
                .WithMembers(root.Members);
            root = root.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(container));
            tree = CSharpSyntaxTree.Create(root);
            root = tree.GetCompilationUnitRoot();
        }

        var namespaces = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Select(ns =>
            string.Join(".", ns.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(part => part.Name.ToString())));
        var imports = namespaces.Append(defaultNamespace).Distinct(StringComparer.Ordinal)
            .Select(ns => Metadata.Value.Imports.GetValueOrDefault(ns)).OfType<SyntaxTree>().Distinct();
        var compilation = Metadata.Value.Compilation.AddSyntaxTrees(imports.Append(tree));
        // Tree contracts also describe internal members and their internal parameter/return types.
        var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
        return (tree, root, model);
    }

    private static bool IsRecordStruct(Type type)
    {
        if (!type.IsValueType || !type.GetInterfaces().Any(i => i.IsGenericType &&
            i.GetGenericTypeDefinition() == typeof(IEquatable<>) && i.GetGenericArguments()[0] == type))
        {
            return false;
        }

        bool HasSynthesizedOperator(string name)
        {
            var op = type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.DeclaredOnly, binder: null, types: [type, type], modifiers: null);
            return op?.ReturnType == typeof(bool) && op.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);
        }

        return HasSynthesizedOperator("op_Equality") && HasSynthesizedOperator("op_Inequality");
    }

    private static IEnumerable<string> CheckDeclaration(SemanticModel model, MemberDeclarationSyntax syntax)
    {
        if (model.GetDeclaredSymbol(syntax) is INamedTypeSymbol type)
        {
            foreach (var problem in CheckType(type, syntax))
            {
                yield return problem;
            }

            yield break;
        }

        var symbols = syntax is BaseFieldDeclarationSyntax field
            ? field.Declaration.Variables.Select(v => model.GetDeclaredSymbol(v))
            : [model.GetDeclaredSymbol(syntax)];
        foreach (var member in symbols)
        {
            if (member is not (IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol) || member.ContainingType is null)
            {
                yield return $"unsupported ✅ declaration ({syntax.Kind()}); show the containing type and a C# declaration";
            }
            else if (MetadataType(member.ContainingType) is { } actual && MemberProblem(actual, member) is { } problem)
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> CheckType(INamedTypeSymbol type, MemberDeclarationSyntax syntax)
    {
        var actual = MetadataType(type);
        if (actual is null)
        {
            yield return $"no matching {type.TypeKind} {type.ToDisplayString()} under ✅";
            yield break;
        }

        var expectedShape = TypeHeader(type, type.IsRecord);
        var actualShape = TypeHeader(actual, IsCompiledRecord(actual));
        if (actualShape != expectedShape)
        {
            yield return $"{type.ToDisplayString()}: documented type '{expectedShape}', compiled type '{actualShape}'";
            yield break;
        }

        if (type.DelegateInvokeMethod is { } invoke && MemberProblem(actual, invoke) is { } delegateProblem)
        {
            yield return delegateProblem;
        }

        if (syntax is TypeDeclarationSyntax { ParameterList: { } parameters })
        {
            foreach (var problem in CheckPrimaryConstructor(type, actual, syntax, parameters))
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<string> CheckPrimaryConstructor(INamedTypeSymbol type, INamedTypeSymbol actual,
        MemberDeclarationSyntax syntax, ParameterListSyntax parameters)
    {
        // A positional record promises both the callable constructor and its positional properties.
        var constructor = type.InstanceConstructors.FirstOrDefault(c => c.DeclaringSyntaxReferences.Any(r => r.GetSyntax() == syntax));
        if (constructor is null)
        {
            yield return $"could not bind the documented constructor of {type}";
        }
        else if (MemberProblem(actual, constructor) is { } constructorProblem)
        {
            yield return constructorProblem;
        }

        if (syntax is not RecordDeclarationSyntax)
        {
            yield break;
        }

        foreach (var parameter in parameters.Parameters)
        {
            var property = type.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().FirstOrDefault();
            if (property is not null && MemberProblem(actual, property) is { } propertyProblem)
            {
                yield return propertyProblem;
            }
        }
    }

    private static string? MemberProblem(INamedTypeSymbol owner, ISymbol expected)
    {
        var candidates = owner.GetMembers(expected.Name);
        if (candidates.IsEmpty)
        {
            return $"{owner} has no member named {expected.Name}, declared under ✅";
        }

        var signature = expected.ToDisplayString(Signature);
        var matches = candidates.Any(candidate => candidate.Kind == expected.Kind &&
            candidate.ToDisplayString(Signature) == signature &&
            (expected is not IPropertySymbol property || (candidate is IPropertySymbol compiled &&
                AccessorMatches(property.GetMethod, compiled.GetMethod) && AccessorMatches(property.SetMethod, compiled.SetMethod))));
        return matches ? null :
            $"{owner}.{expected.Name}: documented '{Describe(expected)}'; compiled: {string.Join(" | ", candidates.Select(Describe))}";
    }

    private static bool AccessorMatches(IMethodSymbol? expected, IMethodSymbol? compiled) => expected is null
        ? compiled is null || compiled.DeclaredAccessibility == Accessibility.Private
        : compiled is not null && expected.DeclaredAccessibility == compiled.DeclaredAccessibility && expected.IsInitOnly == compiled.IsInitOnly;

    private static string Describe(ISymbol member) => member.ToDisplayString(Signature) +
        (member is IPropertySymbol p ? $" {{ {Accessor(p.GetMethod, "get")}{Accessor(p.SetMethod, p.SetMethod?.IsInitOnly == true ? "init" : "set")} }}" : string.Empty);

    private static string Accessor(IMethodSymbol? method, string name) => method is null ? string.Empty : $"{method.DeclaredAccessibility} {name}; ";

    private static bool IsCompiledRecord(INamedTypeSymbol type)
    {
        // Roslyn identifies record classes in metadata, but only source symbols identify record structs.
        if (type.TypeKind != TypeKind.Struct)
        {
            return type.IsRecord;
        }

        var assembly = NodeAssemblies.Assemblies.Values.First(candidate => candidate.GetName().Name == type.ContainingAssembly.Identity.Name);
        return IsRecordStruct(assembly.GetType(MetadataName(type), throwOnError: true)!);
    }

    private static string TypeHeader(INamedTypeSymbol type, bool record)
    {
        string kind;
        if (type.TypeKind == TypeKind.Class)
        {
            var modifier = type.IsStatic ? "static " : type.IsAbstract ? "abstract " : type.IsSealed ? "sealed " : string.Empty;
            kind = modifier + (record ? "record class" : "class");
        }
        else
        {
            kind = type.TypeKind == TypeKind.Struct
                ? (type.IsReadOnly ? "readonly " : string.Empty) + (type.IsRefLikeType ? "ref " : string.Empty) +
                    (record ? "record struct" : "struct")
                : type.TypeKind == TypeKind.Enum ? $"enum : {type.EnumUnderlyingType?.ToDisplayString(Signature)}"
                : type.TypeKind == TypeKind.Interface ? "interface"
                : type.TypeKind == TypeKind.Delegate ? "delegate"
                : type.TypeKind.ToString();
        }

        return SyntaxFacts.GetText(type.DeclaredAccessibility) + " " + kind + " " + type.ToDisplayString(Signature);
    }

    private static string MetadataName(INamedTypeSymbol declared)
    {
        var name = declared.MetadataName;
        for (var parent = declared.ContainingType; parent is not null; parent = parent.ContainingType)
        {
            name = parent.MetadataName + "+" + name;
        }

        if (!declared.ContainingNamespace.IsGlobalNamespace)
        {
            name = declared.ContainingNamespace.ToDisplayString() + "." + name;
        }

        return name;
    }

    private static INamedTypeSymbol? MetadataType(INamedTypeSymbol declared)
    {
        // The reference compilation contains no document types and therefore cannot return the declaration itself.
        var type = Metadata.Value.Compilation.GetTypeByMetadataName(MetadataName(declared));
        return type is not null && NodeAssemblies.Assemblies.Values.Any(assembly =>
            assembly.GetName().Name == type.ContainingAssembly.Identity.Name) ? type : null;
    }

    private static (CSharpCompilation Compilation, Dictionary<string, SyntaxTree> Imports) CreateReferenceCompilation()
    {
        var assemblies = NodeAssemblies.Assemblies.Values.Distinct().ToArray();
        var imports = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            _ = paths.Add(assembly.Location);
            var treeTypes = assembly.GetTypes();
            var referencedTypes = treeTypes.SelectMany(TypeShape.Shape).SelectMany(part => TypeShape.Unwrap(part.Type))
                .Where(type => !type.IsGenericParameter).Distinct().ToList();
            paths.UnionWith(referencedTypes.Select(type => type.Assembly.Location));
            var visible = treeTypes.Concat(referencedTypes).Select(t => t.Namespace).OfType<string>()
                .Distinct(StringComparer.Ordinal).Select(ns => $"global using {ns};");
            var syntax = CSharpSyntaxTree.ParseText(string.Join("\n", visible));
            foreach (var ns in treeTypes.Select(t => t.Namespace).OfType<string>().Distinct(StringComparer.Ordinal))
            {
                _ = imports.TryAdd(ns, syntax);
            }
        }

        var platform = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException("No runtime assembly paths");
        paths.UnionWith(platform.Split(Path.PathSeparator));
        var references = paths.Select(path => MetadataReference.CreateFromFile(path));
        string[] defaults = ["System", "System.Collections.Generic", "System.Diagnostics.CodeAnalysis", "System.IO",
            "System.Linq", "System.Net.Http", "System.Runtime.CompilerServices", "System.Threading", "System.Threading.Tasks"];
        var standard = CSharpSyntaxTree.ParseText(string.Join("\n", defaults.Select(ns => $"global using {ns};")));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable, metadataImportOptions: MetadataImportOptions.All);
        return (CSharpCompilation.Create("ProtocolDocument", [standard], references, options), imports);
    }
}
