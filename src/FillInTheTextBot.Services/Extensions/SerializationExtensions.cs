using Newtonsoft.Json;

namespace FillInTheTextBot.Services.Extensions
{
    public static class SerializationExtensions
    {
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