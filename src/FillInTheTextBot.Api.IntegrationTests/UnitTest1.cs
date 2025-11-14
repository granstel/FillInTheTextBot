using Yandex.Dialogs.Models;
using Newtonsoft.Json;
using System;

namespace FillInTheTextBot.Api.IntegrationTests;

public class Tests
{
    [Test]
    public void Happy_path_test()
    {
        var rnd = new Random();

        var payload = new
        {
            meta = new
            {
                locale = "ru-RU",
                timezone = "UTC",
                client_id = $"client-{Guid.NewGuid():N}",
                interfaces = new
                {
                    screen = new { },
                    payments = new { },
                    account_linking = new { },
                    geolocation_sharing = new { }
                }
            },
            session = new
            {
                message_id = rnd.Next(0, 1000),
                session_id = Guid.NewGuid().ToString("N"),
                skill_id = Guid.NewGuid().ToString("N"),
                user = new { user_id = Guid.NewGuid().ToString("N") },
                application = new { application_id = Guid.NewGuid().ToString("N") },
                user_id = Guid.NewGuid().ToString("N"),
                @new = true
            },
            request = new
            {
                command = string.Empty,
                original_utterance = string.Empty,
                nlu = new
                {
                    tokens = Array.Empty<string>(),
                    entities = Array.Empty<object>(),
                    intents = new { }
                },
                markup = new { dangerous_context = false },
                type = "SimpleUtterance"
            },
            state = new
            {
                session = new { },
                user = new { },
                application = new { }
            },
            version = "1.0"
        };

        var json = JsonConvert.SerializeObject(payload);
        var input = JsonConvert.DeserializeObject<Yandex.Dialogs.Models.Input.InputModel>(json);

        Assert.That(input, Is.Not.Null);
    }
}