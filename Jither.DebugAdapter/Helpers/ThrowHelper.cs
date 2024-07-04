using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Jither.DebugAdapter.Helpers;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static T ThrowUnsupportedProtocolMessage<T>(string typeName)
    {
        throw new NotSupportedException($"Unsupported protocol message type: {typeName}");
    }
}
