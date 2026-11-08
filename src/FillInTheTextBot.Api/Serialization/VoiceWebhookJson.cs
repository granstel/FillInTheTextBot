using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        }
    }
}
