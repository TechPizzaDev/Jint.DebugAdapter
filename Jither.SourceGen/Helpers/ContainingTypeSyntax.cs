// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jither.SourceGen.Extensions;
using Microsoft.CodeAnalysis.CSharp;

namespace Jither.SourceGen.Helpers;

public readonly struct ContainingTypeSyntax(
    SyntaxKind typeKind,
    ImmutableArray<string> modifiers,
    string identifier,
    ImmutableArray<string> typeParameters) :
    IEquatable<ContainingTypeSyntax>
{
    public SyntaxKind TypeKind { get; init; } = typeKind;

    public ImmutableArray<string> Modifiers { get; init; } = modifiers;

    public string Identifier { get; init; } = identifier;

    public ImmutableArray<string> TypeParameters { get; init; } = typeParameters;

    public bool Equals(ContainingTypeSyntax other)
    {
        return TypeKind == other.TypeKind
            && Modifiers.SequenceEqual(other.Modifiers)
            && Identifier == other.Identifier
            && TypeParameters.SequenceEqual(other.TypeParameters);
    }

    public override bool Equals(object? obj) => obj is ContainingTypeSyntax other && Equals(other);

    public override int GetHashCode()
    {
        HashCode code = new();
        code.Add(TypeKind.GetHashCode());
        code.Add(Modifiers.ToHashCode());
        code.Add(Identifier.GetHashCode());
        code.Add(TypeParameters.ToHashCode());
        return code.ToHashCode();
    }

    public IndentedTextWriter.Block WriteTo(IndentedTextWriter writer, bool makePartial)
    {
        string typeWord = TypeKind switch
        {
            SyntaxKind.ClassDeclaration => "class ",
            SyntaxKind.StructDeclaration => "struct ",
            SyntaxKind.RecordDeclaration => "record ",
            SyntaxKind.RecordStructDeclaration => "record struct ",
            SyntaxKind.EnumDeclaration => "enum ",
            SyntaxKind.InterfaceDeclaration => "interface ",
            SyntaxKind.DelegateDeclaration => "delegate ",
            _ => throw new Exception("Unknown syntax kind: " + TypeKind)
        };

        ImmutableArray<string> modifiers = Modifiers;
        if (makePartial)
        {
            // TODO: don't add partial to delegate
            modifiers = modifiers.AddToModifiers("partial");
        }

        List<string> words = modifiers
            .Aggregate(new List<string>(modifiers.Length * 2 + 2 + TypeParameters.Length * 2 + 2), (w, t) =>
            {
                w.Add(t.ToString());
                w.Add(" ");
                return w;
            });

        words.Add(typeWord);
        words.Add(Identifier.ToString());

        if (TypeParameters.Length > 0)
        {
            words.Add("<");
            for (int i = 0; i < TypeParameters.Length; i++)
            {
                words.Add(TypeParameters[i].ToString());

                if (i != TypeParameters.Length - 1)
                    words.Add(", ");
            }
            words.Add(">");
        }

        writer.WriteLine(string.Concat(words));
        return writer.WriteBlock();
    }
}
