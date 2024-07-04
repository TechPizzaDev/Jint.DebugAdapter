using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Jither.SourceGen.Extensions;

internal static class NotNullExtensions
{
    public static IncrementalValuesProvider<TSource> NotNullable<TSource>(this IncrementalValuesProvider<TSource?> source)
        where TSource : struct
    {
        return source.Where(item => item.HasValue).Select((item, ct) => item.GetValueOrDefault());
    }

    public static IncrementalValuesProvider<TSource> NotNull<TSource>(this IncrementalValuesProvider<TSource?> source)
    {
        return source.Where(item => item is not null)!;
    }

    public static IEnumerable<TSource> NotNull<TSource>(this IEnumerable<TSource?> source)
    {
        return source.Where(item => item is not null)!;
    }

    public static IEnumerable<TSource> NotNull<TSource>(this ImmutableArray<TSource?> source)
    {
        return source.Where(item => item is not null)!;
    }
}
