namespace Jint.DebugAdapter
{
    public sealed class SourceIdComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            ReadOnlySpan<char> xSpan = x.AsSpan();
            ReadOnlySpan<char> ySpan = y.AsSpan();
            return Equals(xSpan, ySpan);
        }

        public int GetHashCode(string obj)
        {
            ReadOnlySpan<char> span = obj.AsSpan();
            return GetHashCode(span);
        }

        public static bool Equals(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {
            ReadOnlySpan<char> xRoot = Path.GetPathRoot(x);
            ReadOnlySpan<char> xRest = x.Slice(xRoot.Length);

            ReadOnlySpan<char> yRoot = Path.GetPathRoot(y);
            ReadOnlySpan<char> yRest = y.Slice(yRoot.Length);

            return xRoot.Equals(yRoot, StringComparison.OrdinalIgnoreCase)
                && xRest.Equals(yRest, StringComparison.Ordinal);
        }

        public static int GetHashCode(ReadOnlySpan<char> span)
        {
            ReadOnlySpan<char> root = Path.GetPathRoot(span);
            ReadOnlySpan<char> rest = span.Slice(root.Length);

            int rootCode = string.GetHashCode(root, StringComparison.OrdinalIgnoreCase);
            int restCode = string.GetHashCode(rest, StringComparison.Ordinal);
            return HashCode.Combine(rootCode, restCode);
        }
    }
}
