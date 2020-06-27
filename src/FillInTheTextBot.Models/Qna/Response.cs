using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FillInTheTextBot.Models.Qna
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class Response
    {
        [JsonProperty]
        public AnswerModel[] Answers { get; set; }

        [JsonProperty]
        public Error Error { get; set; }
    }
}
