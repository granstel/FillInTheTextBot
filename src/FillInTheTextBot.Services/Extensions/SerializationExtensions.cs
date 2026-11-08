using System.Text.Encodings.Web;
using System.Text.Json;

namespace FillInTheTextBot.Services.Extensions
{
    public static class SerializationExtensions
    {
        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        internal static string Serialize(this object obj, JsonSerializerOptions options = null)
        {
            if (!(obj is string result))
            {
                result = JsonSerializer.Serialize(obj, options ?? WriteOptions);
            }

            return result;
        }

        public static T Deserialize<T>(this object obj, JsonSerializerOptions options = null)
        {
            switch (obj)
            {
                case T deserialize:
                    return deserialize;
                case string serialized:
                    return JsonSerializer.Deserialize<T>(serialized, options ?? ReadOptions);
                default:
                    return default;
            }
        }
    }
}