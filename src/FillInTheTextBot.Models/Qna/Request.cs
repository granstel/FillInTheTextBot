using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FillInTheTextBot.Models.Qna
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class Request
    {
        public Request(string question)
        {
            Question = question;
        }

        [JsonProperty]
        public string Question { get; set; }
    }
}
