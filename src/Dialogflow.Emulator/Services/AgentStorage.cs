using System.Text.Json;
using Dialogflow.Emulator.Models;

namespace Dialogflow.Emulator.Services;

/// <summary>
/// Читает выгрузку агента Dialogflow ES с диска и держит интенты в памяти.
/// </summary>
public sealed class AgentStorage : IAgentStorage
{
    private const string UserSaysMarker = "_usersays_";
    private const string TextMessageType = "0";
    private const string FallbackIntentName = "Default Fallback Intent";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<AgentStorage> _log;

    private readonly Dictionary<string, AgentIntent> _intentsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AgentIntent> _intentsByEvent = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AgentIntent> _intentsByPhrase = new(StringComparer.OrdinalIgnoreCase);

    public AgentStorage(ILogger<AgentStorage> log)
    {
        _log = log;
    }

    public async Task InitializeAsync(string agentPath)
    {
        var intentsPath = Path.Combine(agentPath, "intents");

        if (!Directory.Exists(intentsPath))
        {
            _log.LogWarning("Intents directory not found at {Path}", intentsPath);

            return;
        }

        var intentFiles = Directory.GetFiles(intentsPath, "*.json")
            .Where(file => !file.Contains(UserSaysMarker, StringComparison.OrdinalIgnoreCase));

        foreach (var file in intentFiles)
        {
            var intent = await ReadIntentAsync(file);

            if (intent?.Name is null)
            {
                continue;
            }

            intent.TrainingPhrases = await ReadTrainingPhrasesAsync(file);

            Register(intent);
        }

        _log.LogInformation(
            "Agent loaded from {Path}: {IntentCount} intents, {EventCount} events, {PhraseCount} training phrases",
            agentPath, _intentsByName.Count, _intentsByEvent.Count, _intentsByPhrase.Count);
    }

    public AgentIntent? FindByEvent(string eventName)
    {
        return _intentsByEvent.GetValueOrDefault(eventName);
    }

    public AgentIntent? FindByText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return _intentsByPhrase.GetValueOrDefault(text.Trim());
    }

    public AgentIntent? GetFallback()
    {
        return _intentsByName.GetValueOrDefault(FallbackIntentName);
    }

    public static string? GetText(AgentIntent? intent)
    {
        var messages = intent?.Responses?.FirstOrDefault()?.Messages;

        if (messages is null)
        {
            return null;
        }

        var textMessage = messages.FirstOrDefault(m => IsTextMessage(m));

        if (textMessage is null)
        {
            return null;
        }

        return ReadSpeech(textMessage.Speech);
    }

    public static string? GetAction(AgentIntent? intent)
    {
        return intent?.Responses?.FirstOrDefault()?.Action;
    }

    private void Register(AgentIntent intent)
    {
        _intentsByName[intent.Name!] = intent;

        foreach (var name in intent.Events?.Select(e => e.Name).Where(n => !string.IsNullOrEmpty(n)) ?? [])
        {
            // Бот шлёт события в своём регистре (Welcome), в выгрузке они лежат
            // в верхнем (WELCOME) — сравнение регистронезависимое
            _intentsByEvent.TryAdd(name!, intent);
        }

        foreach (var phrase in intent.TrainingPhrases)
        {
            _intentsByPhrase.TryAdd(phrase, intent);
        }
    }

    private async Task<AgentIntent?> ReadIntentAsync(string file)
    {
        try
        {
            await using var stream = File.OpenRead(file);

            var intent = await JsonSerializer.DeserializeAsync<AgentIntent>(stream, SerializerOptions);

            return intent;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to load intent from {File}", file);

            return null;
        }
    }

    private async Task<IReadOnlyCollection<string>> ReadTrainingPhrasesAsync(string intentFile)
    {
        var directory = Path.GetDirectoryName(intentFile)!;
        var name = Path.GetFileNameWithoutExtension(intentFile);

        var userSaysFiles = Directory.GetFiles(directory, $"{name}{UserSaysMarker}*.json");

        var phrases = new List<string>();

        foreach (var file in userSaysFiles)
        {
            try
            {
                await using var stream = File.OpenRead(file);

                var userSays = await JsonSerializer.DeserializeAsync<AgentUserSays[]>(stream, SerializerOptions);

                var filePhrases = userSays?
                    .Select(u => string.Concat(u.Data?.Select(d => d.Text) ?? []).Trim())
                    .Where(p => !string.IsNullOrEmpty(p));

                phrases.AddRange(filePhrases ?? []);
            }
            catch (Exception e)
            {
                _log.LogError(e, "Failed to load training phrases from {File}", file);
            }
        }

        return phrases;
    }

    private static bool IsTextMessage(AgentMessage message)
    {
        // В выгрузке type бывает и числом, и строкой
        var type = message.Type.ValueKind switch
        {
            JsonValueKind.String => message.Type.GetString(),
            JsonValueKind.Number => message.Type.GetInt32().ToString(),
            _ => null
        };

        return string.Equals(type, TextMessageType) && string.IsNullOrEmpty(message.Platform);
    }

    private static string? ReadSpeech(JsonElement speech)
    {
        return speech.ValueKind switch
        {
            JsonValueKind.String => speech.GetString(),
            JsonValueKind.Array => speech.EnumerateArray().FirstOrDefault().GetString(),
            _ => null
        };
    }
}
