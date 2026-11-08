using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json.Serialization;
using MarusiaModels = MailRu.Marusia.Models;
using SberModels = Sber.SmartApp.Models;
using YandexModels = Yandex.Dialogs.Models;

namespace FillInTheTextBot.Api.Serialization
{
    internal static class VoiceWebhookJson
    {
        internal static void Configure(JsonSerializerOptions options)
        {
            options.PropertyNamingPolicy = null;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.AllowTrailingCommas = true;
            options.ReadCommentHandling = JsonCommentHandling.Skip;
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { ApplyVoiceContract } };
            options.Converters.Add(new VoiceStateConverter<MarusiaModels.State>());
            options.Converters.Add(new VoiceStateConverter<YandexModels.State>());
        }

        private static void ApplyVoiceContract(JsonTypeInfo typeInfo)
        {
            var assembly = typeInfo.Type.Assembly;
            if (typeInfo.Kind != JsonTypeInfoKind.Object ||
                assembly != typeof(MarusiaModels.OutputModel).Assembly &&
                assembly != typeof(YandexModels.OutputModel).Assembly &&
                assembly != typeof(SberModels.Response).Assembly)
            {
                return;
            }

            var inputSessionType = typeInfo.Type == typeof(YandexModels.Session) ? typeof(YandexModels.Input.InputSession)
                : typeInfo.Type == typeof(MarusiaModels.Session) ? typeof(MarusiaModels.Input.InputSession)
                : null;
            if (inputSessionType is not null)
            {
                typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    DerivedTypes = { new JsonDerivedType(inputSessionType) }
                };
            }

            // The voice SDKs declare their wire names using Newtonsoft attributes.
            var contract = typeInfo.Type.GetCustomAttribute<Newtonsoft.Json.JsonObjectAttribute>();
            var useSnakeCase = contract?.NamingStrategyType == typeof(SnakeCaseNamingStrategy);
            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is not MemberInfo member)
                {
                    continue;
                }

                var attribute = member.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>();
                property.Name = attribute?.PropertyName ?? (useSnakeCase
                    ? JsonNamingPolicy.SnakeCaseLower.ConvertName(member.Name)
                    : member.Name);
            }
        }

        private sealed class VoiceStateConverter<TState> : JsonConverter<TState> where TState : Dictionary<string, object>, new()
        {
            public override TState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException();
                }

                var state = new TState();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return state;
                    }

                    var key = reader.GetString();
                    if (!reader.Read())
                    {
                        break;
                    }

                    // The SDK state helpers look up CLR primitives by type.
                    state[key] = reader.TokenType switch
                    {
                        JsonTokenType.True => true,
                        JsonTokenType.False => false,
                        JsonTokenType.Null => null,
                        JsonTokenType.String => reader.GetString(),
                        JsonTokenType.Number when reader.TryGetInt64(out var number) => number,
                        JsonTokenType.Number => reader.GetDouble(),
                        _ => JsonElement.ParseValue(ref reader)
                    };
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, TState value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize<Dictionary<string, object>>(writer, value, options);
            }
        }
    }
}
