namespace Dialogflow.Emulator.Models;

using System.Text.Json.Serialization;

public record Intent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("responses")] IReadOnlyList<IntentResponse> Responses,
    [property: JsonPropertyName("events")] IReadOnlyList<IntentEvent>? Events
);

public record IntentResponse(
    [property: JsonPropertyName("messages")] IReadOnlyList<ResponseMessage> Messages
);

public record ResponseMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("speech")] IReadOnlyList<string>? Speech
);

public record IntentEvent(
    [property: JsonPropertyName("name")] string Name
);
