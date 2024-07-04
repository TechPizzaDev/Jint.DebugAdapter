﻿using System.Text.Json.Serialization;
using Jither.DebugAdapter.Protocol.Requests;

namespace Jither.DebugAdapter.Protocol
{
    public abstract class BaseProtocolRequest : ProtocolMessage
    {
        public const string TypeName = "request";
        
        [JsonPropertyOrder(-10)]
        public string Command { get; set; }

        public abstract ProtocolArguments UntypedArguments { get; }

        public BaseProtocolRequest()
        {
            Type = TypeName;
        }
    }

    public class ProtocolRequest : BaseProtocolRequest
    {
        [JsonIgnore]
        public ProtocolArguments Arguments { get; private set; }
        
        [JsonIgnore]
        public override ProtocolArguments UntypedArguments => Arguments;

        [JsonPropertyName("arguments"), JsonPropertyOrder(100)]
        public object SerializedArguments => Arguments;

        public ProtocolRequest(string command, ProtocolArguments arguments)
        {
            Command = command;
            Arguments = arguments;
        }
    }

    public abstract class IncomingProtocolRequest : BaseProtocolRequest
    {
        internal abstract void Sanitize(ProtocolArguments arguments);
    }

    public class IncomingProtocolRequest<TArguments> : IncomingProtocolRequest 
        where TArguments: ProtocolArguments
    {
        [JsonPropertyOrder(100)]
        public TArguments Arguments { get; set; }

        [JsonIgnore]
        public override ProtocolArguments UntypedArguments => Arguments;

        internal override void Sanitize(ProtocolArguments arguments)
        {
            Arguments = arguments as TArguments;
        }
    }
}
