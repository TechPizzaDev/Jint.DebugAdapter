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

    public static void ExpectJsonToken(in Utf8JsonReader reader, JsonTokenType expectedType)
    {
        if (reader.TokenType != expectedType)
        {
            Throw(reader, expectedType);
        }

        static void Throw(in Utf8JsonReader reader, JsonTokenType expectedType)
        {
            throw new JsonException($"Expected {expectedType} token but got {reader.TokenType}.");
        }
    }
}
