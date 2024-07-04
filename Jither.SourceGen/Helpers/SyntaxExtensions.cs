// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jither.SourceGen.Helpers;

public static class SyntaxExtensions
{
    public static SyntaxTokenList StripTriviaFromTokens(this SyntaxTokenList tokenList)
    {
        return SyntaxFactory.TokenList(tokenList.Select(n => n.WithoutTrivia()));
    }

    public static SyntaxTokenList StripAccessibilityModifiers(this SyntaxTokenList tokenList)
    {
        return SyntaxFactory.TokenList(tokenList.Where(t =>
        {
            return t.Kind() is
                SyntaxKind.PublicKeyword or
                SyntaxKind.InternalKeyword or
                SyntaxKind.ProtectedKeyword or
                SyntaxKind.PrivateKeyword;
        }));
    }

    public static ImmutableArray<string> AddToModifiers(
        this ImmutableArray<string> modifiers,
        string modifierToAdd)
    {
        if (modifiers.Contains(modifierToAdd))
        {
            return modifiers;
        }

        // https://github.com/dotnet/csharplang/blob/main/meetings/2018/LDM-2018-04-04.md#ordering-of-ref-and-partial-keywords
        int idxPartial = modifiers.IndexOf("partial");
        int idxRef = modifiers.IndexOf("ref");

        int idxInsert = (idxPartial, idxRef) switch
        {
            (-1, -1) => modifiers.Length,
            (-1, _) => idxRef,
            (_, -1) => idxPartial,
            (_, _) => Math.Min(idxPartial, idxRef)
        };

        return modifiers.Insert(idxInsert, modifierToAdd);
    }

    public static bool IsInPartialContext(
        this TypeDeclarationSyntax syntax,
        [NotNullWhen(false)] out SyntaxToken? nonPartialIdentifier)
    {
        for (SyntaxNode? parentNode = syntax;
            parentNode is TypeDeclarationSyntax typeDecl;
            parentNode = parentNode.Parent)
        {
            if (!typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                nonPartialIdentifier = typeDecl.Identifier;
                return false;
            }
        }
        nonPartialIdentifier = null;
        return true;
    }

    [return: NotNullIfNotNull(nameof(node))]
    public static SyntaxNode? TopNode(this SyntaxNode? node)
    {
        SyntaxNode? current = node;
        while (current != null)
        {
            SyntaxNode? next = current.Parent;
            if (next == null)
            {
                break;
            }
            current = next;
        }
        return current;
    }
}
