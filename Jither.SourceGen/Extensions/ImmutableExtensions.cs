using System;
using System.Collections.Immutable;

namespace Jither.SourceGen.Extensions;

internal static class ImmutableExtensions
{
    public static int ToHashCode<T>(this ImmutableArray<T> array)
    {
        HashCode code = new();
        foreach (T item in array)
        {
            code.Add(item?.GetHashCode() ?? 0);
        }
        return code.ToHashCode();
    }
}
