using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jither.SourceGen.Helpers;

public readonly record struct ContainingSyntaxContext(
    ImmutableArray<ContainingNamespaceSyntax> Namespaces,
    ImmutableArray<ContainingTypeSyntax> Types)
{
    public static ContainingSyntaxContext FromInclusive(SyntaxNode rootNode)
    {
        return new ContainingSyntaxContext(
            GetContainingNamespaces(rootNode),
            GetContainingTypes(rootNode));
    }

    private static ImmutableArray<ContainingTypeSyntax> GetContainingTypes(SyntaxNode rootNode)
    {
        // TODO: option for duplicating Attributes?

        ImmutableArrayBuilder<ContainingTypeSyntax> builder = new();

        for (SyntaxNode? parent = rootNode.FirstAncestorOrSelf<TypeDeclarationSyntax>();
            parent is TypeDeclarationSyntax declaration;
            parent = parent.Parent)
        {
            ImmutableArray<string> typeParameters =
                declaration.TypeParameterList?.Parameters.Select(s =>
                {
                    string variance = s.VarianceKeyword.ToString();
                    string id = s.Identifier.ToString();
                    return variance != "" ? $"{variance} {id}" : id;
                })
                .ToImmutableArray() ?? ImmutableArray<string>.Empty;

            ContainingTypeSyntax syntax = new(
                declaration.Kind(),
                declaration.Modifiers.Select(m => m.ToString()).ToImmutableArray(),
                declaration.Identifier.ToString(),
                typeParameters);

            builder.Add(syntax);
        }

        builder.WrittenSpan.Reverse();

        return builder.ToImmutable();
    }

    private static ImmutableArray<ContainingNamespaceSyntax> GetContainingNamespaces(SyntaxNode rootNode)
    {
        // TODO: option for duplicating namespace elements (extern, usings)?

        ImmutableArrayBuilder<ContainingNamespaceSyntax> builder = new();

        for (SyntaxNode? parent = rootNode.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
            parent is BaseNamespaceDeclarationSyntax declaration;
            parent = parent.Parent)
        {
            ContainingNamespaceSyntax syntax = new(
                declaration.Kind(),
                declaration.Name.ToString());

            builder.Add(syntax);
        }

        builder.WrittenSpan.Reverse();

        return builder.ToImmutable();
    }

    public bool Equals(ContainingSyntaxContext other)
    {
        return Types.SequenceEqual(other.Types)
            && Namespaces.SequenceEqual(other.Namespaces);
    }

    public override int GetHashCode()
    {
        HashCode code = new();
        foreach (ContainingNamespaceSyntax ns in Namespaces)
        {
            code.Add(ns.GetHashCode());
        }
        foreach (ContainingTypeSyntax ts in Types)
        {
            code.Add(ts.GetHashCode());
        }
        return code.ToHashCode();
    }

    public List<IndentedTextWriter.Block> WriteTo(IndentedTextWriter writer, bool makePartial)
    {
        List<IndentedTextWriter.Block> blocks = new();

        foreach (ContainingNamespaceSyntax ns in Namespaces)
        {
            blocks.Add(ns.WriteTo(writer));
        }
        foreach (ContainingTypeSyntax ts in Types)
        {
            blocks.Add(ts.WriteTo(writer, makePartial));
        }

        return blocks;
    }
}
