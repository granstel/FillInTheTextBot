using System.Text.Json.Serialization;

namespace FillInTheTextBot.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<Appeal>))]
    public enum Appeal
    {
        Default,
        NoOfficial,
        Official,
    }
}
