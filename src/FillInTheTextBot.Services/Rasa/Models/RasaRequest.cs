using System.Text.Json.Serialization;

namespace FillInTheTextBot.Services.Rasa.Models;

public class RasaRequest
{
    [JsonPropertyName("sender")]
    public string Sender { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}