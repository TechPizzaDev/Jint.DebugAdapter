// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jither.SourceGen.Helpers;

/// <summary>
/// A helper type to build sequences of values with pooled buffers.
/// </summary>
/// <typeparam name="T">The type of items to create sequences for.</typeparam>
internal struct ImmutableArrayBuilder<T> : IDisposable
{
    /// <summary>
    /// The shared <see cref="ObjectPool{T}"/> instance to share <see cref="Writer"/> objects.
    /// </summary>
    private static readonly ObjectPool<Writer> SharedObjectPool = new(static () => new Writer());

    /// <summary>
    /// The rented <see cref="Writer"/> instance to use.
    /// </summary>
    private Writer? writer;

    /// <summary>
    /// Creates a new <see cref="ImmutableArrayBuilder{T}"/> object.
    /// </summary>
    public ImmutableArrayBuilder()
    {
        writer = SharedObjectPool.Allocate();
    }

    /// <summary>
    /// Gets the data written to the underlying buffer so far, as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public readonly Span<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => writer!.WrittenSpan;
    }

    /// <summary>
    /// Gets the number of elements currently written in the current instance.
    /// </summary>
    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => writer!.Count;
    }

    /// <summary>
    /// Advances the current writer and gets a <see cref="Span{T}"/> to the requested memory area.
    /// </summary>
    /// <param name="requestedSize">The requested size to advance by.</param>
    /// <returns>A <see cref="Span{T}"/> to the requested memory area.</returns>
    /// <remarks>
    /// No other data should be written to the builder while the returned <see cref="Span{T}"/>
    /// is in use, as it could invalidate the memory area wrapped by it, if resizing occurs.
    /// </remarks>
    public readonly Span<T> Advance(int requestedSize)
    {
        return writer!.Advance(requestedSize);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Add(T)"/>
    public readonly void Add(T item)
    {
        writer!.Add(item);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Add(T)"/>
    public readonly void Add(T item, int repeatCount)
    {
        writer!.Add(item, repeatCount);
    }

    /// <summary>
    /// Adds the specified items to the end of the array.
    /// </summary>
    /// <param name="items">The items to add at the end of the array.</param>
    public readonly void AddRange(ReadOnlySpan<T> items)
    {
        writer!.AddRange(items);
    }

    /// <summary>
    /// Adds the specified items to the end of the array.
    /// </summary>
    /// <param name="items">The items to add at the end of the array.</param>
    /// <param name="repeatCount">The amount of times to repeat the sequence.</param>
    public readonly void AddRange(ReadOnlySpan<T> items, int repeatCount)
    {
        writer!.AddRange(items, repeatCount);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Clear"/>
    public readonly void Clear()
    {
        writer!.Clear();
    }

    /// <summary>
    /// Inserts an item to the builder at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
    /// <param name="item">The object to insert into the current instance.</param>
    public readonly void Insert(int index, T item)
    {
        writer!.Insert(index, item);
    }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> instance for the current builder.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> instance for the current builder.</returns>
    /// <remarks>
    /// The builder should not be mutated while an enumerator is in use.
    /// </remarks>
    public readonly IEnumerable<T> AsEnumerable()
    {
        return writer!;
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.ToImmutable"/>
    public readonly ImmutableArray<T> ToImmutable()
    {
        T[] array = writer!.WrittenSpan.ToArray();

        return ImmutableCollectionsMarshal.AsImmutableArray(array);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.ToArray"/>
    public readonly T[] ToArray()
    {
        return writer!.WrittenSpan.ToArray();
    }

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        return writer!.WrittenSpan.ToString();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Writer? writer = this.writer;

        this.writer = null;

        if (writer is not null)
        {
            writer.Clear();

            SharedObjectPool.Free(writer);
        }
    }

    /// <summary>
    /// A class handling the actual buffer writing.
    /// </summary>
    private sealed class Writer : IList<T>, IReadOnlyList<T>
    {
        /// <summary>
        /// The underlying <typeparamref name="T"/> array.
        /// </summary>
        private T[] array;

        /// <summary>
        /// The starting offset within <see cref="array"/>.
        /// </summary>
        private int index;

        /// <summary>
        /// Creates a new <see cref="Writer"/> instance with the specified parameters.
        /// </summary>
        public Writer()
        {
            if (typeof(T) == typeof(char))
            {
                array = new T[1024];
            }
            else
            {
                array = new T[8];
            }

            index = 0;
        }

        /// <inheritdoc/>
        public int Count => index;

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.WrittenSpan"/>
        public Span<T> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(array, 0, index);
        }

        /// <inheritdoc/>
        bool ICollection<T>.IsReadOnly => true;

        /// <inheritdoc/>
        T IReadOnlyList<T>.this[int index] => WrittenSpan[index];

        /// <inheritdoc/>
        T IList<T>.this[int index]
        {
            get => WrittenSpan[index];
            set => throw new NotSupportedException();
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Advance"/>
        public Span<T> Advance(int requestedSize)
        {
            EnsureCapacity(requestedSize);

            Span<T> span = array.AsSpan(index, requestedSize);

            index += requestedSize;

            return span;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Add"/>
        public void Add(T value, int repeatCount)
        {
            EnsureCapacity(repeatCount);

            array.AsSpan(index, repeatCount).Fill(value);
            index += repeatCount;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Add"/>
        public void Add(T value)
        {
            Add(value, 1);
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.AddRange"/>
        public void AddRange(ReadOnlySpan<T> items, int repeatCount)
        {
            EnsureCapacity(items.Length * repeatCount);

            for (int i = 0; i < repeatCount; i++)
            {
                items.CopyTo(array.AsSpan(index));

                index += items.Length;
            }
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.AddRange"/>
        public void AddRange(ReadOnlySpan<T> items)
        {
            AddRange(items, 1);
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Insert"/>
        public void Insert(int index, T item)
        {
            if (index < 0 || index > this.index)
            {
                ImmutableArrayBuilder.ThrowArgumentOutOfRangeExceptionForIndex();
            }

            EnsureCapacity(1);

            if (index < this.index)
            {
                Array.Copy(array, index, array, index + 1, this.index - index);
            }

            array[index] = item;
            this.index++;
        }

        /// <summary>
        /// Clears the items in the current writer.
        /// </summary>
        public void Clear()
        {
            if (typeof(T) != typeof(byte) &&
                typeof(T) != typeof(char) &&
                typeof(T) != typeof(int))
            {
                array.AsSpan(0, index).Clear();
            }

            index = 0;
        }

        /// <summary>
        /// Ensures that <see cref="array"/> has enough free space to contain a given number of new items.
        /// </summary>
        /// <param name="requestedSize">The minimum number of items to ensure space for in <see cref="array"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requestedSize)
        {
            if (requestedSize > array.Length - index)
            {
                ResizeBuffer(requestedSize);
            }
        }

        /// <summary>
        /// Resizes <see cref="array"/> to ensure it can fit the specified number of new items.
        /// </summary>
        /// <param name="sizeHint">The minimum number of items to ensure space for in <see cref="array"/>.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ResizeBuffer(int sizeHint)
        {
            int minimumSize = index + sizeHint;
            int requestedSize = Math.Max(array.Length * 2, minimumSize);

            T[] newArray = new T[requestedSize];

            Array.Copy(array, newArray, index);

            array = newArray;
        }

        /// <inheritdoc/>
        int IList<T>.IndexOf(T item)
        {
            return Array.IndexOf(array, item, 0, index);
        }

        /// <inheritdoc/>
        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        bool ICollection<T>.Contains(T item)
        {
            return Array.IndexOf(array, item, 0, index) >= 0;
        }

        /// <inheritdoc/>
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(this.array, 0, array, arrayIndex, index);
        }

        /// <inheritdoc/>
        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            T?[] array = this.array!;
            int length = index;

            for (int i = 0; i < length; i++)
            {
                yield return array[i]!;
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>) this).GetEnumerator();
        }
    }
}

/// <summary>
/// Private helpers for the <see cref="ImmutableArrayBuilder{T}"/> type.
/// </summary>
file static class ImmutableArrayBuilder
{
    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> for <c>"index"</c>.
    /// </summary>
    public static void ThrowArgumentOutOfRangeExceptionForIndex()
    {
        throw new ArgumentOutOfRangeException("index");
    }
}