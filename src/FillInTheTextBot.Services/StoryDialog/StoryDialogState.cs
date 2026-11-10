using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StoryDialog.Contracts.V1;

namespace FillInTheTextBot.Services.StoryDialog;

internal sealed class StoryDialogState
{
    internal const int MaxStories = 4;
    private const int MaxLength = 700;
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 8,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    [JsonRequired, JsonPropertyName("c")] public string ConversationId { get; set; }
    [JsonRequired, JsonPropertyName("v")] public ulong Version { get; set; }
    [JsonRequired, JsonPropertyName("s")] public Status Status { get; set; }
    [JsonRequired, JsonPropertyName("i")] public string StoryId { get; set; }
    [JsonRequired, JsonPropertyName("o")] public string[] StoryOrder { get; set; } = Array.Empty<string>();

    internal string Serialize()
    {
        var result = JsonSerializer.Serialize(this, Options);
        return result;
    }

    internal static bool TryRead(string value, string conversationId, out StoryDialogState state)
    {
        state = null;
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<StoryDialogState>(value, Options);
            if (parsed == null || parsed.ConversationId != conversationId || parsed.Version > long.MaxValue ||
                !Enum.IsDefined(parsed.Status) || !IsStoryId(parsed.StoryId) ||
                (parsed.Version == 0) != (parsed.Status == Status.Unspecified) ||
                parsed.StoryOrder == null || parsed.StoryOrder.Length > MaxStories ||
                (parsed.Version > 0 && parsed.StoryOrder.Length == 0) ||
                parsed.StoryOrder.Any(id => !IsStoryId(id)))
            {
                return false;
            }

            var uniqueStories = parsed.StoryOrder.Distinct(StringComparer.Ordinal).Count();
            if (uniqueStories != parsed.StoryOrder.Length)
            {
                return false;
            }

            state = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool IsStoryId(string id)
    {
        var result = !string.IsNullOrEmpty(id) && id.Length <= 64 &&
            id.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '.');
        return result;
    }
}
