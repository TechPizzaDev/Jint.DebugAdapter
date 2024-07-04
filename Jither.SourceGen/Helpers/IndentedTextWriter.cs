// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable IDE0290

namespace Jither.SourceGen.Helpers;

/// <summary>
/// A helper type to build sequences of values with pooled buffers.
/// </summary>
public sealed class IndentedTextWriter : IDisposable
{
    private readonly record struct Indentation(string? Suffix, IndentedBlockFlags Mode);

    /// <summary>
    /// The default indentation (4 spaces).
    /// </summary>
    private const int DefaultIndentation = 4;

    /// <summary>
    /// The default new line (<c>'\n'</c>).
    /// </summary>
    private const char DefaultNewLine = '\n';

    /// <summary>
    /// The <see cref="ImmutableArrayBuilder{T}"/> instance that text will be written to.
    /// </summary>
    private ImmutableArrayBuilder<char> builder;

    /// <summary>
    /// The cached array of available indentations, as text.
    /// </summary>
    private readonly Stack<Indentation> indentations;

    private int indentLevel;

    /// <summary>
    /// Creates a new <see cref="IndentedTextWriter"/> object.
    /// </summary>
    public IndentedTextWriter()
    {
        builder = new ImmutableArrayBuilder<char>();
        indentations = new Stack<Indentation>();
    }

    public ReadOnlySpan<char> WrittenSpan => builder.WrittenSpan;

    /// <summary>
    /// Add the leading whitespace if needed.
    /// </summary>
    private void TryAddLeadingWhitespace()
    {
        if (builder.Count == 0 || builder.WrittenSpan[^1] == DefaultNewLine)
        {
            builder.Add(' ', indentLevel * DefaultIndentation);
        }
    }

    /// <summary>
    /// Advances the current writer and gets a <see cref="Span{T}"/> to the requested memory area.
    /// </summary>
    /// <param name="requestedSize">The requested size to advance by.</param>
    /// <returns>A <see cref="Span{T}"/> to the requested memory area.</returns>
    /// <remarks>
    /// No other data should be written to the writer while the returned <see cref="Span{T}"/>
    /// is in use, as it could invalidate the memory area wrapped by it, if resizing occurs.
    /// </remarks>
    public Span<char> Advance(int requestedSize)
    {
        TryAddLeadingWhitespace();

        return builder.Advance(requestedSize);
    }

    [return: NotNullIfNotNull(nameof(prefix))]
    public static string? GetSuffix(string? prefix)
    {
        if (prefix == null)
        {
            return null;
        }

        return prefix switch
        {
            "" => "",
            "{" => "}",
            "[" => "]",
            "(" => ")",
            "/*" => "*/",
            string p when !p.StartsWith('\"') => "\"",
            string p when p.Contains('\"') => p,
            _ => throw new ArgumentOutOfRangeException(nameof(prefix)),
        };
    }

    /// <summary>
    /// Increases the current indentation level by pushing <paramref name="suffix"/> onto the indentation stack. 
    /// </summary>
    public Block IncreaseIndent(string? suffix, IndentedBlockFlags mode = IndentedBlockFlags.Default)
    {
        indentations.Push(new(suffix, mode));
        if ((mode & IndentedBlockFlags.ZeroWidth) == 0)
        {
            indentLevel++;
        }
        return new Block(this);
    }

    /// <summary>
    /// Increases the current indentation level.
    /// </summary>
    /// <remarks>
    /// Pushes <see langword="null"/> onto the indentation stack. 
    /// </remarks>
    public Block IncreaseIndent(IndentedBlockFlags mode = IndentedBlockFlags.Default)
    {
        return IncreaseIndent(null, mode);
    }

    /// <summary>
    /// Decreases the current indentation level.
    /// </summary>
    public void DecreaseIndent()
    {
        Indentation indent = indentations.Pop();
        if ((indent.Mode & IndentedBlockFlags.ZeroWidth) == 0)
        {
            indentLevel--;
        }

        if ((indent.Mode & IndentedBlockFlags.LineBeforeSuffix) != 0)
        {
            WriteLine();
        }
        if (indent.Suffix != null)
        {
            Write(indent.Suffix);
        }
        if ((indent.Mode & IndentedBlockFlags.LineAfterSuffix) != 0)
        {
            WriteLine();
        }
    }

    public void DecreaseAllIndent()
    {
        while (indentations.Count > 0)
        {
            DecreaseIndent();
        }
    }

    /// <summary>
    /// Writes a block to the underlying buffer.
    /// </summary>
    /// <returns>A <see cref="Block"/> value to close the open block with.</returns>
    public Block WriteBlock(string? prefix, string? suffix, IndentedBlockFlags mode = IndentedBlockFlags.Default)
    {
        if ((mode & IndentedBlockFlags.LineBeforePrefix) != 0)
        {
            WriteLine();
        }
        if (prefix != null)
        {
            Write(prefix);
        }
        if ((mode & IndentedBlockFlags.LineAfterPrefix) != 0)
        {
            WriteLine();
        }
        return IncreaseIndent(suffix, mode);
    }

    public Block WriteBlock(string? prefix = "{", IndentedBlockFlags mode = IndentedBlockFlags.Default)
    {
        string? suffix = GetSuffix(prefix);
        return WriteBlock(prefix, suffix, mode);
    }

    /// <summary>
    /// Writes content to the underlying buffer.
    /// </summary>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void Write(string? content, bool isMultiline = false)
    {
        Write(content.AsSpan(), isMultiline);
    }

    /// <summary>
    /// Writes content to the underlying buffer.
    /// </summary>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void Write(ReadOnlySpan<char> content, bool isMultiline = false)
    {
        if (isMultiline)
        {
            while (content.Length > 0)
            {
                int newLineIndex = content.IndexOf(DefaultNewLine);

                if (newLineIndex < 0)
                {
                    // There are no new lines left, so the content can be written as a single line
                    WriteRawText(content);

                    break;
                }
                else
                {
                    ReadOnlySpan<char> line = content[..newLineIndex];

                    // Write the current line (if it's empty, we can skip writing the text entirely).
                    // This ensures that raw multiline string literals with blank lines don't have
                    // extra whitespace at the start of those lines, which would otherwise happen.
                    WriteIf(!line.IsEmpty, line);
                    WriteLine();

                    // Move past the new line character (the result could be an empty span)
                    content = content[(newLineIndex + 1)..];
                }
            }
        }
        else
        {
            WriteRawText(content);
        }
    }

    /// <summary>
    /// Writes content to the underlying buffer.
    /// </summary>
    /// <param name="handler">The interpolated string handler with content to write.</param>
    public void Write([InterpolatedStringHandlerArgument("")] ref WriteInterpolatedStringHandler handler)
    {
        _ = this;
    }

    /// <summary>
    /// Writes content to the underlying buffer depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void WriteIf(bool condition, string content, bool isMultiline = false)
    {
        if (condition)
        {
            Write(content.AsSpan(), isMultiline);
        }
    }

    /// <summary>
    /// Writes content to the underlying buffer depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void WriteIf(bool condition, ReadOnlySpan<char> content, bool isMultiline = false)
    {
        if (condition)
        {
            Write(content, isMultiline);
        }
    }

    /// <summary>
    /// Writes content to the underlying buffer depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="handler">The interpolated string handler with content to write.</param>
    public void WriteIf(bool condition, [InterpolatedStringHandlerArgument("", nameof(condition))] ref WriteIfInterpolatedStringHandler handler)
    {
        _ = this;
    }

    /// <summary>
    /// Writes a line to the underlying buffer.
    /// </summary>
    /// <param name="skipIfPresent">Indicates whether to skip adding the line if there already is one.</param>
    public void WriteLine(bool skipIfPresent = false)
    {
        if (skipIfPresent && builder.WrittenSpan is [.., '\n', '\n'])
        {
            return;
        }

        builder.Add(DefaultNewLine);
    }

    /// <summary>
    /// Writes content to the underlying buffer and appends a trailing new line.
    /// </summary>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void WriteLine(string content, bool isMultiline = false)
    {
        WriteLine(content.AsSpan(), isMultiline);
    }

    /// <summary>
    /// Writes content to the underlying buffer and appends a trailing new line.
    /// </summary>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void WriteLine(ReadOnlySpan<char> content, bool isMultiline = false)
    {
        Write(content, isMultiline);
        WriteLine();
    }

    /// <summary>
    /// Writes content to the underlying buffer and appends a trailing new line.
    /// </summary>
    /// <param name="handler">The interpolated string handler with content to write.</param>
    public void WriteLine([InterpolatedStringHandlerArgument("")] ref WriteInterpolatedStringHandler handler)
    {
        WriteLine();
    }

    /// <summary>
    /// Writes a line to the underlying buffer depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="skipIfPresent">Indicates whether to skip adding the line if there already is one.</param>
    public void WriteLineIf(bool condition, bool skipIfPresent = false)
    {
        if (condition)
        {
            WriteLine(skipIfPresent);
        }
    }

    /// <summary>
    /// Writes content to the underlying buffer and appends a trailing new line depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void WriteLineIf(bool condition, string content, bool isMultiline = false)
    {
        if (condition)
        {
            WriteLine(content.AsSpan(), isMultiline);
        }
    }

    /// <summary>
    /// Writes content to the underlying buffer and appends a trailing new line depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="content">The content to write.</param>
    /// <param name="isMultiline">Whether the input content is multiline.</param>
    public void WriteLineIf(bool condition, ReadOnlySpan<char> content, bool isMultiline = false)
    {
        if (condition)
        {
            Write(content, isMultiline);
            WriteLine();
        }
    }

    /// <summary>
    /// Writes content to the underlying buffer and appends a trailing new line depending on an input condition.
    /// </summary>
    /// <param name="condition">The condition to use to decide whether or not to write content.</param>
    /// <param name="handler">The interpolated string handler with content to write.</param>
    public void WriteLineIf(bool condition, [InterpolatedStringHandlerArgument("", nameof(condition))] ref WriteIfInterpolatedStringHandler handler)
    {
        if (condition)
        {
            WriteLine();
        }
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return builder.WrittenSpan.Trim().ToString();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        builder.Dispose();
    }

    /// <summary>
    /// Writes raw text to the underlying buffer, adding leading indentation if needed.
    /// </summary>
    /// <param name="content">The raw text to write.</param>
    private void WriteRawText(ReadOnlySpan<char> content)
    {
        TryAddLeadingWhitespace();

        builder.AddRange(content);
    }

    /// <summary>
    /// A delegate representing a callback to write data into an <see cref="IndentedTextWriter"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of data to use.</typeparam>
    /// <param name="value">The input data to use to write into <paramref name="writer"/>.</param>
    /// <param name="writer">The <see cref="IndentedTextWriter"/> instance to write into.</param>
    public delegate void Callback<T>(T value, IndentedTextWriter writer);

    /// <summary>
    /// Represents an indented block that needs to be closed.
    /// </summary>
    /// <param name="writer">The input <see cref="IndentedTextWriter"/> instance to wrap.</param>
    public struct Block(IndentedTextWriter writer) : IDisposable
    {
        /// <summary>
        /// The <see cref="IndentedTextWriter"/> instance to write to.
        /// </summary>
        private IndentedTextWriter? writer = writer;

        /// <inheritdoc/>
        public void Dispose()
        {
            IndentedTextWriter? writer = this.writer;

            this.writer = null;

            if (writer is not null)
            {
                writer.DecreaseIndent();
            }
        }
    }

    /// <summary>
    /// Provides a handler used by the language compiler to append interpolated strings into <see cref="IndentedTextWriter"/> instances.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [InterpolatedStringHandler]
    public readonly ref struct WriteInterpolatedStringHandler
    {
        /// <summary>The associated <see cref="IndentedTextWriter"/> to which to append.</summary>
        private readonly IndentedTextWriter writer;

        /// <summary>Creates a handler used to append an interpolated string into a <see cref="StringBuilder"/>.</summary>
        /// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="writer">The associated <see cref="IndentedTextWriter"/> to which to append.</param>
        /// <remarks>
        /// This is intended to be called only by compiler-generated code. 
        /// Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
        public WriteInterpolatedStringHandler(int literalLength, int formattedCount, IndentedTextWriter writer)
        {
            this.writer = writer;
        }

        /// <summary>Writes the specified string to the handler.</summary>
        /// <param name="value">The string to write.</param>
        public void AppendLiteral(string value) => writer.Write(value);

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        public void AppendFormatted(string? value) => AppendFormatted<string?>(value);

        /// <summary>Writes the specified character span to the handler.</summary>
        /// <param name="value">The span to write.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => writer.Write(value);

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value)
        {
            if (value is not null)
            {
                writer.Write(value.ToString());
            }
        }

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="format">The format string.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value, string? format)
        {
            if (value is IFormattable)
            {
                writer.Write(((IFormattable) value).ToString(format, CultureInfo.InvariantCulture));
            }
            else if (value is not null)
            {
                writer.Write(value.ToString());
            }
        }
    }

    /// <summary>
    /// Provides a handler used by the language compiler to conditionally append interpolated strings into <see cref="IndentedTextWriter"/> instances.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [InterpolatedStringHandler]
    public readonly ref struct WriteIfInterpolatedStringHandler
    {
        /// <summary>The associated <see cref="WriteInterpolatedStringHandler"/> to use.</summary>
        private readonly WriteInterpolatedStringHandler handler;

        /// <summary>Creates a handler used to append an interpolated string into a <see cref="StringBuilder"/>.</summary>
        /// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="writer">The associated <see cref="IndentedTextWriter"/> to which to append.</param>
        /// <param name="condition">The condition to use to decide whether or not to write content.</param>
        /// <param name="shouldAppend">A value indicating whether formatting should proceed.</param>
        /// <remarks>
        /// This is intended to be called only by compiler-generated code.
        /// Arguments are not validated as they'd otherwise be for members intended to be used directly.
        /// </remarks>
        public WriteIfInterpolatedStringHandler(int literalLength, int formattedCount, IndentedTextWriter writer, bool condition, out bool shouldAppend)
        {
            if (condition)
            {
                handler = new WriteInterpolatedStringHandler(literalLength, formattedCount, writer);
                shouldAppend = true;
            }
            else
            {
                handler = default;
                shouldAppend = false;
            }
        }

        /// <inheritdoc cref="WriteInterpolatedStringHandler.AppendLiteral(string)"/>
        public void AppendLiteral(string value) => handler.AppendLiteral(value);

        /// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted(string?)"/>
        public void AppendFormatted(string? value) => handler.AppendFormatted(value);

        /// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted(ReadOnlySpan{char})"/>
        public void AppendFormatted(ReadOnlySpan<char> value) => handler.AppendFormatted(value);

        /// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted{T}(T)"/>
        public void AppendFormatted<T>(T value) => handler.AppendFormatted(value);

        /// <inheritdoc cref="WriteInterpolatedStringHandler.AppendFormatted{T}(T, string?)"/>
        public void AppendFormatted<T>(T value, string? format) => handler.AppendFormatted(value, format);
    }
}