using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FillInTheTextBot.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum Appeal
    {
        Default,
        NoOfficial,
        Official,
    }
}
