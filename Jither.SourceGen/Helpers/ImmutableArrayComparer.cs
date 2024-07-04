using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jither.SourceGen.Helpers;

/// <summary>
/// Generic comparer to compare two <see cref="ImmutableArray{T}"/> instances element by element.
/// </summary>
/// <typeparam name="T">The type of immutable array element.</typeparam>
internal sealed class ImmutableArrayComparer<T> : IEqualityComparer<ImmutableArray<T>>
{
    private readonly IEqualityComparer<T>? _elementComparer;

    /// <summary>
    /// Creates an <see cref="ImmutableArrayComparer{T}"/> with a custom comparer for the elements of the collection.
    /// </summary>
    /// <param name="elementComparer">The comparer instance for the collection elements.</param>
    public ImmutableArrayComparer(IEqualityComparer<T>? elementComparer)
    {
        _elementComparer = elementComparer;
    }

    public bool Equals(ImmutableArray<T> x, ImmutableArray<T> y)
    {
        return x.AsSpan().SequenceEqual(y.AsSpan(), _elementComparer);
    }

    public int GetHashCode(ImmutableArray<T> obj)
    {
        HashCode sum = new();
        ReadOnlySpan<T> span = obj.AsSpan();
        if (_elementComparer is null ||
            _elementComparer == EqualityComparer<T>.Default)
        {
            if (typeof(T) == typeof(byte) ||
                typeof(T) == typeof(char))
            {
                ReadOnlySpan<byte> byteSpan = MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(span)),
                    checked(span.Length * Unsafe.SizeOf<T>()));

                sum.AddBytes(byteSpan);
            }
            else
            {
                foreach (T value in span)
                {
                    int hash = value != null ? EqualityComparer<T>.Default.GetHashCode(value) : 0;
                    sum.Add(hash);
                }
            }
        }
        else
        {
            foreach (T value in span)
            {
                int hash = value != null ? _elementComparer.GetHashCode(value) : 0;
                sum.Add(hash);
            }
        }
        return sum.ToHashCode();
    }
}
