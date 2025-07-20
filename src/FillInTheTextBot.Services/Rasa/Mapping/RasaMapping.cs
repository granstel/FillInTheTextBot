using System;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Rasa.Models;

namespace FillInTheTextBot.Services.Rasa.Mapping;

public static class RasaMapping
{
    public static Dialog ToDialog(this IEnumerable<RasaResponse> rasaResponses, Dialog destination = null)
    {
        destination ??= new Dialog();

        var responses = rasaResponses?.ToList() ?? new List<RasaResponse>();
        
        // Берем первый ответ с текстом
        var mainResponse = responses.FirstOrDefault(r => !string.IsNullOrEmpty(r.Text));
        
        if (mainResponse != null)
        {
            destination.Response = mainResponse.Text;
            destination.Buttons = GetButtons(mainResponse);
            destination.Parameters = GetParameters(mainResponse);
            destination.Payload = GetPayload(mainResponse);
            destination.Action = GetAction(mainResponse);
            destination.EndConversation = string.Equals(destination.Action, "endConversation");
        }

        return destination;
    }

    private static IDictionary<string, string> GetParameters(RasaResponse response)
    {
        var dictionary = new Dictionary<string, string>();

        if (response.Custom?.ContainsKey("parameters") == true)
        {
            var parameters = response.Custom["parameters"] as Dictionary<string, object>;
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    dictionary.Add(param.Key, param.Value?.ToString() ?? string.Empty);
                }
            }
        }

        return dictionary;
    }

    private static Button[] GetButtons(RasaResponse response)
    {
        if (response.Buttons?.Any() != true)
            return System.Array.Empty<Button>();

        return response.Buttons.Select(b => new Button
        {
            Text = b.Title,
            Url = b.Payload,
            IsQuickReply = true
        }).ToArray();
    }

    private static Payload GetPayload(RasaResponse response)
    {
        if (response.Custom?.ContainsKey("payload") == true)
        {
            // Здесь может быть более сложная логика для конвертации Payload
            return new Payload();
        }

        return null;
    }

    private static string GetAction(RasaResponse response)
    {
        if (response.Custom?.ContainsKey("action") == true)
        {
            return response.Custom["action"]?.ToString();
        }

        return null;
    }
}