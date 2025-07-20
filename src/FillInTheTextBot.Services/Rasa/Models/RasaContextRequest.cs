using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FillInTheTextBot.Services.Rasa.Models;

public class RasaContextRequest
{
    [JsonPropertyName("sender_id")]
    public string SenderId { get; set; }

    [JsonPropertyName("slots")]
    public Dictionary<string, object> Slots { get; set; }
}