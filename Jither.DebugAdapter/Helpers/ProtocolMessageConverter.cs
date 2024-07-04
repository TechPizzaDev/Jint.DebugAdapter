using System.Text.Json;
using System.Text.Json.Serialization;
using Jither.DebugAdapter.Protocol;

namespace Jither.DebugAdapter.Helpers
{
    internal class ProtocolMessageConverter : JsonConverter<ProtocolMessage>
    {
        public override ProtocolMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            ThrowHelper.ExpectJsonToken(reader, JsonTokenType.StartObject);

            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                var typeName = JsonHelper.GetProperty(doc.RootElement, "type"u8);
                var result = GetConcreteType(typeName.GetString(), doc.RootElement, options);
                return result;
            }
        }

        private string GetCommand(JsonElement element)
        {
            if (element.TryGetProperty("command", out var commandProp))
            {
                return commandProp.GetString();
            }
            return null;
        }

        private string GetEvent(JsonElement element)
        {
            if (element.TryGetProperty("event", out var evtProp))
            {
                return evtProp.GetString();
            }
            return null;
        }

        private ProtocolMessage GetConcreteType(string typeName, JsonElement element, JsonSerializerOptions options)
        {
            switch (typeName)
            {
                case BaseProtocolRequest.TypeName:
                {
                    string command = GetCommand(element);
                    var result = ProtocolMessageRegistry.GetRequestType(command).Invoke(element, options);

                    // For equal treatment when dealing with argument-less requests, instantiantiate empty arguments object 
                    if (result is IncomingProtocolRequest req && req.UntypedArguments == null)
                    {
                        var argumentsFactory = ProtocolMessageRegistry.GetArgumentFactory(command);
                        req.Sanitize(argumentsFactory.Invoke());
                    }
                    return result;
                }

                case BaseProtocolResponse.TypeName:
                {
                    string command = GetCommand(element);
                    return ProtocolMessageRegistry.GetResponseType(command).Invoke(element, options);
                }

                case BaseProtocolEvent.TypeName:
                {
                    string evt = GetEvent(element);
                    return ProtocolMessageRegistry.GetEventType(evt).Invoke(element, options);
                }

                default:
                    return ThrowHelper.ThrowUnsupportedProtocolMessage<ProtocolMessage>(typeName);
            }
        }

        public override void Write(Utf8JsonWriter writer, ProtocolMessage value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
