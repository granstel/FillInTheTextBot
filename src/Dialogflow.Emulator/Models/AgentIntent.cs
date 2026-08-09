using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dialogflow.Emulator.Models;

/// <summary>
/// Интент из выгрузки агента Dialogflow ES (файл intents/&lt;имя&gt;.json).
/// </summary>
public sealed class AgentIntent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("events")]
    public AgentEvent[]? Events { get; set; }

    [JsonPropertyName("responses")]
    public AgentResponse[]? Responses { get; set; }

    /// <summary>
    /// Обучающие фразы из файла intents/&lt;имя&gt;_usersays_&lt;lang&gt;.json.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyCollection<string> TrainingPhrases { get; set; } = [];
}

public sealed class AgentEvent
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class AgentResponse
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("parameters")]
    public AgentParameter[]? Parameters { get; set; }

    [JsonPropertyName("messages")]
    public AgentMessage[]? Messages { get; set; }
}

/// <summary>
/// Параметр интента. Обязательные (required) со своими промптами эмулятор набивает
/// по очереди (слот-филлинг), необязательные со статическим value отдаёт как есть.
/// </summary>
public sealed class AgentParameter
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("prompts")]
    public AgentPrompt[]? Prompts { get; set; }
}

public sealed class AgentPrompt
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

public sealed class AgentMessage
{
    /// <summary>
    /// Тип сообщения: "0" — текст, остальные типы (кнопки, карточки) эмулятор не отдаёт.
    /// </summary>
    [JsonPropertyName("type")]
    public JsonElement Type { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    /// <summary>
    /// В выгрузке значение бывает и строкой, и массивом строк.
    /// </summary>
    [JsonPropertyName("speech")]
    public JsonElement Speech { get; set; }
}

/// <summary>
/// Файл обучающих фраз intents/&lt;имя&gt;_usersays_&lt;lang&gt;.json.
/// </summary>
public sealed class AgentUserSays
{
    [JsonPropertyName("data")]
    public AgentUserSaysPart[]? Data { get; set; }
}

public sealed class AgentUserSaysPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
