using System.Text.Json;
using Jither.DebugAdapter.Protocol.Events;
using Jither.DebugAdapter.Protocol.Requests;
using Jither.DebugAdapter.Protocol.Responses;
using Jither.SourceGen;

namespace Jither.DebugAdapter.Protocol
{
    internal delegate T DeserializerFunc<T>(JsonElement element, JsonSerializerOptions options);

    internal static partial class ProtocolMessageRegistry
    {
        private static readonly Dictionary<string, DeserializerFunc<ProtocolMessage>> requests = new();
        private static readonly Dictionary<string, Func<ProtocolArguments>> arguments = new();
        private static readonly Dictionary<string, DeserializerFunc<ProtocolMessage>> responses = new();
        private static readonly Dictionary<string, DeserializerFunc<ProtocolMessage>> events = new();

        static ProtocolMessageRegistry()
        {
            VisitRegisterRequest();
            VisitRegisterResponse();
            VisitRegisterEvent();
        }

        private static void RegisterRequest<[VisitorGen(typeof(ProtocolArguments))] T>()
            where T : ProtocolArguments, new()
        {
            // Using convention for argument command name: Camel-cased type name with Arguments suffix removed.
            string command = typeof(T).Name.Replace("Arguments", string.Empty);
            command = char.ToLowerInvariant(command[0]) + command[1..];
            var requestType = typeof(IncomingProtocolRequest<T>);
            requests.Add(command, static (e, o) => e.Deserialize<IncomingProtocolRequest<T>>(o));
            arguments.Add(command, static () => new T());
        }

        private static void RegisterResponse<[VisitorGen(typeof(ProtocolResponseBody))] T>()
            where T : ProtocolResponseBody
        {
            // Using convention for response body command name: Camel-cased type name with ResponseBody suffix removed.
            string command = typeof(T).Name.Replace("Response", string.Empty);
            command = char.ToLowerInvariant(command[0]) + command[1..];
            responses.Add(command, static (e, o) => e.Deserialize<IncomingProtocolResponse<T>>(o));
        }

        private static void RegisterEvent<[VisitorGen(typeof(ProtocolEventBody))] T>()
            where T : ProtocolEventBody
        {
            // Using convention for event body event name: Camel-cased type name with EventBody suffix removed.
            string command = typeof(T).Name.Replace("Event", string.Empty);
            command = char.ToLowerInvariant(command[0]) + command[1..];
            events.Add(command, static (e, o) => e.Deserialize<IncomingProtocolEvent<T>>(o));
        }

        public static DeserializerFunc<ProtocolMessage> GetRequestType(string command)
        {
            return requests.GetValueOrDefault(command) ?? throw new NotSupportedException($"Unsupported request command: {command}");
        }

        public static Func<ProtocolArguments> GetArgumentFactory(string command)
        {
            return arguments.GetValueOrDefault(command) ?? throw new NotSupportedException($"Unsupported request arguments command: {command}");
        }

        public static DeserializerFunc<ProtocolMessage> GetResponseType(string command)
        {
            return responses.GetValueOrDefault(command) ?? throw new NotSupportedException($"Unsupported response command: {command}");
        }

        public static DeserializerFunc<ProtocolMessage> GetEventType(string evt)
        {
            return events.GetValueOrDefault(evt) ?? throw new NotSupportedException($"Unsupported event type: {evt}");
        }
    }
}
