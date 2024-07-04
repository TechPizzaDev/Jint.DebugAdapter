using System;
using Microsoft.CodeAnalysis.CSharp;

namespace Jither.SourceGen.Helpers;

public readonly struct ContainingNamespaceSyntax(SyntaxKind typeKind, string name) :
    IEquatable<ContainingNamespaceSyntax>
{
    public SyntaxKind TypeKind { get; init; } = typeKind;

    public string Name { get; init; } = name;

    public bool Equals(ContainingNamespaceSyntax other)
    {
        return TypeKind == other.TypeKind
            && Name == other.Name;
    }

    public override bool Equals(object? obj) => obj is ContainingNamespaceSyntax other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(TypeKind, Name);
    }

    public IndentedTextWriter.Block WriteTo(IndentedTextWriter writer)
    {
        writer.Write("namespace ");
        writer.Write(Name);

        if (TypeKind == SyntaxKind.FileScopedNamespaceDeclaration)
        {
            writer.WriteLine(";");
            writer.WriteLine();
            return default;
        }
        else
        {
            writer.WriteLine();
            return writer.WriteBlock();
        }
    }
}
