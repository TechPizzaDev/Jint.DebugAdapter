﻿using System.Text.Json.Serialization;
using Jither.DebugAdapter.Protocol.Responses;

namespace Jither.DebugAdapter.Protocol
{
    public abstract class BaseProtocolResponse : ProtocolMessage
    {
        public const string TypeName = "response";

        [JsonPropertyOrder(-10)]
        public string Command { get; set; }

        [JsonPropertyOrder(-9)]
        public bool Success { get; set; }

        [JsonPropertyName("request_seq")]
        public long RequestSeq { get; set; }
        
        public string Message { get; set; }
        
        public abstract ProtocolResponseBody UntypedBody { get; }

        public BaseProtocolResponse()
        {
            Type = TypeName;
        }
    }

    public class ProtocolResponse : BaseProtocolResponse
    {
        [JsonIgnore]
        public ProtocolResponseBody Body { get; private set; }

        [JsonIgnore]
        public override ProtocolResponseBody UntypedBody => Body;

        [JsonPropertyName("body"), JsonPropertyOrder(100)]
        public object SerializedBody => Body;

        public ProtocolResponse(string command, long requestSeq, bool success, Responses.ProtocolResponseBody body, string message = null)
        {
            Command = command;
            RequestSeq = requestSeq;
            Success = success;
            Body = body;
            Message = message;
        }
    }

    public class IncomingProtocolResponse<T> : BaseProtocolResponse where T: Responses.ProtocolResponseBody
    {
        [JsonPropertyOrder(100)]
        public T Body { get; set; }

        [JsonIgnore]
        public override ProtocolResponseBody UntypedBody => Body;
    }
}
