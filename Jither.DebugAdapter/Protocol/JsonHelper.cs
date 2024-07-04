﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jither.DebugAdapter.Helpers;

namespace Jither.DebugAdapter.Protocol
{
    internal static class JsonHelper
    {
        private static readonly JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions outputOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        static JsonHelper()
        {
            options.Converters.Add(new StringEnumConverter());
            outputOptions.Converters.Add(new StringEnumConverter());
        }

        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }

        public static string SerializeForOutput<T>(T obj)
        {
            return JsonSerializer.Serialize<object>(obj, outputOptions);
        }

        public static string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize<object>(obj, options);
        }

        public static JsonElement GetProperty(JsonElement element, ReadOnlySpan<byte> name)
        {
            if (!element.TryGetProperty(name, out var prop))
            {
                Throw(name);
            }
            return prop;

            static void Throw(ReadOnlySpan<byte> name)
            {
                throw new JsonException($"\"{Encoding.UTF8.GetString(name)}\" property not found.");
            }
        }
    }
}
