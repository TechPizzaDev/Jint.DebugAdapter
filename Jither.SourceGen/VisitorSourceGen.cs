using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Jither.SourceGen.Extensions;
using Jither.SourceGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jither.SourceGen;

class TypeSymbolContext
{
    ImmutableArray<INamedTypeSymbol> typesWithBases;
    ImmutableArray<INamedTypeSymbol> typesWithoutBases;
    ImmutableArray<INamedTypeSymbol> typesWithInterfaces;

    public TypeSymbolContext(IEnumerable<INamedTypeSymbol> types)
    {
        using ImmutableArrayBuilder<INamedTypeSymbol> baseBuilder = new();
        using ImmutableArrayBuilder<INamedTypeSymbol> noBaseBuilder = new();
        using ImmutableArrayBuilder<INamedTypeSymbol> interfaceBuilder = new();

        foreach (INamedTypeSymbol type in types)
        {
            if (type.BaseType != null)
            {
                baseBuilder.Add(type);
            }
            else
            {
                noBaseBuilder.Add(type);
            }

            if (type.AllInterfaces.Length > 0)
            {
                interfaceBuilder.Add(type);
            }
        }

        typesWithBases = baseBuilder.ToImmutable();
        typesWithoutBases = noBaseBuilder.ToImmutable();
        typesWithInterfaces = interfaceBuilder.ToImmutable();
    }

    public IEnumerable<INamedTypeSymbol> FindMatches(ImmutableArray<INamedTypeSymbol?> constraintTypes, VisitorGenOptions desc)
    {
        //if (constraintTypes.IsDefaultOrEmpty)
        //{
        //    return typesWithBases.Concat(typesWithoutBases);
        //}

        Debug.Assert(!constraintTypes.Contains(null));

        INamedTypeSymbol? baseType = constraintTypes.FirstOrDefault(s => s?.TypeKind != TypeKind.Interface);

        IEnumerable<INamedTypeSymbol>? types = null;
        if (baseType != null)
        {
            types = typesWithBases.Where(t =>
            {
                if (!desc.IncludeAbstract && t.IsAbstract)
                {
                    return false;
                }
                return HasBaseType(t, baseType);
            });
        }

        if (!constraintTypes.Any(s => s?.TypeKind == TypeKind.Interface))
        {
            return types ?? [];
        }

        types ??= typesWithInterfaces;

        HashSet<INamedTypeSymbol> constraintSet = new(
            constraintTypes.Where(s => s?.TypeKind == TypeKind.Interface)!,
            SymbolEqualityComparer.Default);

        return types.Where(t =>
        {
            if (!desc.IncludeAbstract && t.IsAbstract)
            {
                return false;
            }

            HashSet<INamedTypeSymbol> definedSet = new(t.AllInterfaces, SymbolEqualityComparer.Default);
            return definedSet.IsSupersetOf(constraintSet);
        });
    }

    static bool HasBaseType(INamedTypeSymbol targetType, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol symbol = targetType;
            symbol.BaseType != null;
            symbol = symbol.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, baseType))
            {
                return true;
            }
        }
        return false;
    }
}

readonly record struct VisitorGenOptions(bool IncludeAbstract, bool SolveConstraints);

readonly record struct VisitorGenDesc(string Type, ImmutableArray<string> Sources) : IEquatable<VisitorGenDesc>
{
    public bool Equals(VisitorGenDesc other)
    {
        return Type == other.Type
            && Sources.SequenceEqual(other.Sources);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type.GetHashCode(), Sources.ToHashCode());
    }
}

readonly record struct DecorateVisitorGenDesc(string Type, bool Splat, string Suffix);

[Generator]
public partial class VisitInheritorsSourceGen : IIncrementalGenerator
{
    static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
            foreach (var nestedType in GetNestedTypes(type))
                yield return nestedType;

        foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
            foreach (var type in GetAllTypes(nestedNamespace))
                yield return type;
    }

    static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nestedType in type.GetTypeMembers()
            .SelectMany(GetNestedTypes))
            yield return nestedType;
    }

    private static IEnumerable<(AttributeData Attrib, IEnumerable<INamedTypeSymbol> Types)> GetAttribSymbols(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellation)
    {
        foreach (var attrib in context.Attributes)
        {
            var symbols = attrib.ConstructorArguments
                .Where(a => a.Kind == TypedConstantKind.Array)
                .SelectMany(a => a.Values)
                .Select(t => t.Value as INamedTypeSymbol)
                .NotNull();

            if (symbols.Any())
            {
                var distinct = symbols.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                yield return (attrib, distinct);
            }
        }
    }

    private static (
        ContainingSyntaxContext ContainCtx,
        string TargetSymbol,
        ImmutableArray<VisitorGenDesc> FoundSymbols)
        TransformVisitorGenAttribs(
            GeneratorAttributeSyntaxContext context,
            CancellationToken cancellation)
    {
        Dictionary<INamedTypeSymbol, VisitorGenOptions> attribMap = new(SymbolEqualityComparer.Default);

        var attribs = GetAttribSymbols(context, cancellation);

        foreach ((AttributeData attrib, IEnumerable<INamedTypeSymbol> types) in attribs)
        {
            bool includeAbstract = attrib.NamedArguments
                .FirstOrDefault(p => p.Key == "IncludeAbstract")
                .Value.Value as bool? ?? false;

            bool solveConstraints = attrib.NamedArguments
                .FirstOrDefault(p => p.Key == "SolveConstraints")
                .Value.Value as bool? ?? true;

            VisitorGenOptions desc = new(includeAbstract, solveConstraints);

            foreach (INamedTypeSymbol type in types)
            {
                attribMap[type] = desc;
            }
        }

        ImmutableArray<VisitorGenDesc> finalSymbols;
        if (attribMap.Count > 0)
        {
            TypeSymbolContext ctx = new(GetAllTypes(context.SemanticModel.Compilation.GlobalNamespace));

            Dictionary<INamedTypeSymbol, List<KeyValuePair<INamedTypeSymbol, VisitorGenOptions>>> foundSymbols = new(SymbolEqualityComparer.Default);

            void AddFound(INamedTypeSymbol key, KeyValuePair<INamedTypeSymbol, VisitorGenOptions> value)
            {
                if (!foundSymbols.TryGetValue(key, out List<KeyValuePair<INamedTypeSymbol, VisitorGenOptions>>? list))
                {
                    list = new();
                    foundSymbols.Add(key, list);
                }
                list.Add(value);
            }

            void Iterate(IEnumerable<KeyValuePair<INamedTypeSymbol, VisitorGenOptions>> values)
            {
                foreach (var pair in values)
                {
                    foreach (INamedTypeSymbol symbol in ctx.FindMatches([pair.Key], pair.Value))
                    {
                        if (pair.Value.SolveConstraints && symbol.IsGenericType)
                        {
                            var typeArguments = symbol.TypeArguments
                                .Select(ts => ts is ITypeParameterSymbol tps
                                    ? ctx.FindMatches(
                                        tps.ConstraintTypes.Select(ct => ct as INamedTypeSymbol).ToImmutableArray(),
                                        pair.Value)
                                    : []);

                            var constructPairs = CartesianProduct(typeArguments)
                                .Select(args =>
                                {
                                    var c = symbol.Construct(args.Cast<ITypeSymbol>().ToImmutableArray(), default);
                                    return new KeyValuePair<INamedTypeSymbol, VisitorGenOptions>(c, pair.Value);
                                });

                            foreach (var constructPair in constructPairs)
                            {
                                AddFound(constructPair.Key, new(pair.Key, constructPair.Value));
                            }
                            continue;
                        }
                        AddFound(symbol, pair);
                    }
                }
            }

            Iterate(attribMap);

            finalSymbols = foundSymbols
                .Select(s => new VisitorGenDesc(
                    s.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    s.Value.Select(s => s.Key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).ToImmutableArray()))
                .ToImmutableArray();
        }
        else
        {
            finalSymbols = [];
        }

        return (
            ContainingSyntaxContext.FromInclusive(context.TargetNode),
            context.TargetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            finalSymbols);
    }

    private static (string TargetSymbol, ImmutableArray<DecorateVisitorGenDesc> FoundAttribs) TransformDecorateAttribs(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellation)
    {
        Dictionary<INamedTypeSymbol, DecorateVisitorGenDesc> attribMap = new(SymbolEqualityComparer.Default);

        var attribs = GetAttribSymbols(context, cancellation);

        foreach ((AttributeData attrib, IEnumerable<INamedTypeSymbol> types) in attribs)
        {
            foreach (INamedTypeSymbol type in types)
            {
                string suffix = attrib.NamedArguments
                    .FirstOrDefault(p => p.Key == "Suffix")
                    .Value.Value as string ?? "";

                bool splat = false;

                string typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                attribMap[type] = new DecorateVisitorGenDesc(typeName, splat, suffix);
            }
        }

        return (
            context.TargetSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            attribMap.Values.ToImmutableArray());
    }

    public static IEnumerable<IEnumerable<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences)
    {
        IEnumerable<IEnumerable<T>> seed = [[]];
        return sequences.Aggregate(
            seed,
            (acc, seq) => acc.SelectMany(_ => seq, (accseq, item) => accseq.Append(item)));
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        InitTypeVisitors(context);
        InitTypeParamVisitors(context);

        //IncrementalValuesProvider<SymbolDecl> enumTypes = semanticEnumTypes
        //    .Where(decl => !decl.TypesInAttributes.IsDefaultOrEmpty)
        //    .Select((decl, ct) =>
        //    {
        //        ImmutableArray<string> typesInAttributes = decl.TypesInAttributes
        //            .Select(s => s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
        //            .ToImmutableArray();
        //
        //        return new SymbolDecl(decl.ContainingSyntax, typesInAttributes);
        //    });
        //
        //context.RegisterSourceOutput(enumTypes, GenerateCode);
    }

    private static void InitTypeParamVisitors(IncrementalGeneratorInitializationContext context)
    {
        var visitorAttrs = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Jither.SourceGen.VisitorGenAttribute",
                (node, _) => node is TypeParameterSyntax,
                (ctx, ct) =>
                {
                    var method = ctx.TargetNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                    return (TransformVisitorGenAttribs(ctx, ct), method?.Identifier.ToString());
                });

        context.RegisterSourceOutput(visitorAttrs, static (prodCtx, visitorCtx) =>
        {
            var (ctxTuple, methodId) = visitorCtx;
            var (containingSyntax, visitorSymbol, visitorSymbols) = ctxTuple;

            using IndentedTextWriter writer = new();

            writer.WriteLine("// <auto-generated />");

            var namespaces = containingSyntax.Namespaces.AsSpan();
            for (int i = 0; i < namespaces.Length; i++)
            {
                ContainingNamespaceSyntax ns = namespaces[i];
                _ = ns.WriteTo(writer);
            }

            var types = containingSyntax.Types.AsSpan();
            for (int i = 0; i < types.Length; i++)
            {
                ContainingTypeSyntax ts = types[i];

                _ = ts.WriteTo(writer, makePartial: true);

                if (i == types.Length - 1)
                {
                    writer.WriteLine($"private static void Visit{methodId}()");
                    using var methodBlock = writer.WriteBlock();

                    foreach (VisitorGenDesc visitorDesc in visitorSymbols)
                    {
                        //writer.WriteLine($"// {visitorDesc.Type} found by {string.Join(", ", visitorDesc.Sources)}");

                        writer.WriteLine($"{methodId}<{visitorDesc.Type}>();");
                    }
                }
            }

            writer.DecreaseAllIndent();

            string fileName = string.Join('.', containingSyntax.Types.Select(t => t.Identifier));
            string sourceStr = writer.ToString();

            prodCtx.AddSource($"{fileName}.{methodId}.g.cs", sourceStr);
        });
    }

    private static void InitTypeVisitors(IncrementalGeneratorInitializationContext context)
    {
        var visitorAttrs = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Jither.SourceGen.VisitorGenAttribute",
                (node, _) => node is TypeDeclarationSyntax,
                TransformVisitorGenAttribs);

        var decorateAttrs = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Jither.SourceGen.DecorateVisitorGenAttribute",
                (node, _) => node is TypeDeclarationSyntax,
                TransformDecorateAttribs)
            .Collect();

        var visitAndDecorateAttrs = visitorAttrs.Combine(decorateAttrs).WithTrackingName("VisitorGenContext");

        context.RegisterSourceOutput(visitAndDecorateAttrs, static (prodCtx, tuple) =>
        {
            var (visitorCtx, decorateAttrs) = tuple;
            var (containingSyntax, visitorSymbol, visitorSymbols) = visitorCtx;

            using IndentedTextWriter writer = new();

            writer.WriteLine("// <auto-generated />");

            foreach (var (decoratedSymbol, decorateSymbols) in decorateAttrs)
            {
                if (decoratedSymbol != visitorSymbol)
                {
                    continue;
                }

                var namespaces = containingSyntax.Namespaces.AsSpan();
                for (int i = 0; i < namespaces.Length; i++)
                {
                    ContainingNamespaceSyntax ns = namespaces[i];
                    _ = ns.WriteTo(writer);
                }

                var types = containingSyntax.Types.AsSpan();
                for (int i = 0; i < types.Length; i++)
                {
                    ContainingTypeSyntax ts = types[i];
                    if (i == types.Length - 1)
                    {
                        WriteDecorators(writer, visitorSymbols, decorateSymbols);
                    }
                    _ = ts.WriteTo(writer, makePartial: true);
                }
            }

            writer.DecreaseAllIndent();

            string fileName = string.Join('.', containingSyntax.Types.Select(t => t.Identifier));
            string sourceStr = writer.ToString();

            prodCtx.AddSource($"{fileName}.g.cs", sourceStr);
        });
    }

    private static void WriteDecorators(
        IndentedTextWriter writer,
        ImmutableArray<VisitorGenDesc> visitorSymbols,
        ImmutableArray<DecorateVisitorGenDesc> decorateSymbols)
    {
        foreach (VisitorGenDesc visitorDesc in visitorSymbols)
        {
            //writer.WriteLine($"// {visitorDesc.Type} found by {string.Join(", ", visitorDesc.Sources)}");

            foreach (DecorateVisitorGenDesc decorateDesc in decorateSymbols)
            {
                if (decorateDesc.Splat)
                {
                    continue;
                }

                WriteDecorator(writer, decorateDesc.Type, [visitorDesc.Type], decorateDesc.Suffix);
            }

            writer.WriteLine();
        }

        foreach (DecorateVisitorGenDesc decorateDesc in decorateSymbols)
        {
            if (!decorateDesc.Splat)
            {
                continue;
            }

            WriteDecorator(writer, decorateDesc.Type, visitorSymbols.Select(d => d.Type), decorateDesc.Suffix);
        }
    }

    private static void WriteDecorator(
        IndentedTextWriter writer, string decoratorSymbol, IEnumerable<string> typeSymbols, string decoratorSuffix)
    {
        using var attribBlock = writer.WriteBlock("[", IndentedBlockFlags.None);
        writer.Write(decoratorSymbol);

        using var paramsBlock = writer.WriteBlock("(", IndentedBlockFlags.LineAfterPrefix | IndentedBlockFlags.ZeroWidth);
        bool hasPrev = false;

        foreach (var symbol in typeSymbols)
        {
            if (hasPrev)
            {
                writer.WriteLine(",");
            }

            writer.Write($"typeof({symbol})");

            hasPrev = true;
        }

        if (!string.IsNullOrWhiteSpace(decoratorSuffix))
        {
            if (hasPrev)
            {
                writer.WriteLine(",");
            }
            writer.Write(decoratorSuffix);
        }
    }
}

public readonly record struct SymbolDecl(
    ContainingSyntaxContext ContainingSyntax,
    ImmutableArray<string> TypesInAttributes) :
    IEquatable<SymbolDecl>
{
    public bool Equals(SymbolDecl other)
    {
        return ContainingSyntax.Equals(other.ContainingSyntax)
            && TypesInAttributes.SequenceEqual(other.TypesInAttributes);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            ContainingSyntax.GetHashCode(),
            TypesInAttributes.ToHashCode());
    }
}