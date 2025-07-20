using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FillInTheTextBot.Services.Rasa.Models;

public class RasaResponse
{
    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("buttons")]
    public List<RasaButton> Buttons { get; set; }

    [JsonPropertyName("custom")]
    public Dictionary<string, object> Custom { get; set; }
}

public class RasaButton
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; }
}