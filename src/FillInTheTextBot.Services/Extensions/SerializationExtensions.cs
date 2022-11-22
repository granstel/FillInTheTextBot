using Newtonsoft.Json;

namespace FillInTheTextBot.Services.Extensions
{
    public static class SerializationExtensions
    {
        internal static string Serialize(this object obj, JsonSerializerSettings settings = null)
        {
            if (!(obj is string result))
            {
                result = JsonConvert.SerializeObject(obj, settings);
            }

            return result;
        }

        public static T Deserialize<T>(this object obj, JsonSerializerSettings settings = null)
        {
            switch (obj)
            {
                case T deserialize:
                    return deserialize;
                case string serialized:
                    return JsonConvert.DeserializeObject<T>(serialized, settings);
                default:
                    return default;
            }
        }
    }
}