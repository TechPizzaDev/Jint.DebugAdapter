using System.Text.Json.Serialization;
using Jither.DebugAdapter.Protocol.Events;
using Jither.DebugAdapter.Protocol.Requests;
using Jither.DebugAdapter.Protocol.Responses;
using Jither.SourceGen;

namespace Jither.DebugAdapter.Protocol
{
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
    [VisitorGen(typeof(ProtocolArguments), typeof(ProtocolResponseBody), typeof(ProtocolEventBody))]
    [DecorateVisitorGen(typeof(JsonSerializableAttribute))]
    internal partial class ProtocolJsonContext : JsonSerializerContext
    {
    }
}
