using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FillInTheTextBot.Models.Qna
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class AnswerModel
    {
        [JsonProperty]
        public string Answer { get; set; }

        [JsonProperty]
        public string[] Questions { get; set; }

        [JsonProperty]
        public float Score { get; set; }
    }
}
