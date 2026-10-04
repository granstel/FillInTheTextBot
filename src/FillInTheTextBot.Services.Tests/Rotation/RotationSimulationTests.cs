using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    /// <summary>
    /// Прогон ротации по правилам из репозитория — проверка правки rotation.json до мерджа.
    /// Последовательности видны в выводе: dotnet test --filter RotationSimulationTests --logger "console;verbosity=detailed"
    /// </summary>
    [TestFixture]
    public class RotationSimulationTests
    {
        private RotationStore _store;
        private Dictionary<int, string> _lastIntentNames;

        [OneTimeSetUp]
        public void InitFixture()
        {
            var root = TestContext.CurrentContext.TestDirectory;

            while (!File.Exists(Path.Combine(root, "src", "FillInTheTextBot.slnx")))
            {
                root = Directory.GetParent(root)!.FullName;
            }

            var intentsPath = Path.Combine(root, "Dialogflow", "FillInTheTextBot-eu", "intents");
            var files = Directory.GetFiles(intentsPath, "*.json").Select(Path.GetFileName).ToArray();

            var sources = new RotationSources
            {
                StoryFiles = files,
                RulesJson = File.ReadAllText(Path.Combine(root, "Dialogflow", "rotation.json"))
            };

            RotationSnapshot.TryCreate(sources, DateTimeOffset.UtcNow, out var snapshot, out var errors);
            Assert.That(errors, Is.Empty);

            _store = new RotationStore();
            _store.Apply(snapshot);

            _lastIntentNames = files
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => StoryCatalog.TryParseIntentName(name, out var number, out var part) && snapshot.Catalog.IsLastPart(number, part))
                .ToDictionary(name => int.Parse(name.Split('-')[0]));
        }

        [Test]
        public void NewUser_Today()
        {
            Simulate("Новый пользователь, сегодня", new RotationState(), DateTimeOffset.UtcNow);
        }

        [Test]
        public void OldUser_Today()
        {
            Simulate("Пользователь на третьем круге, сегодня", new RotationState { Cycle = 3 }, DateTimeOffset.UtcNow);
        }

        [Test]
        public void EachCampaign_FirstDay()
        {
            foreach (var campaign in _store.Snapshot.Rules.Campaigns)
            {
                var from = campaign.From.Length == 5 ? $"{DateTime.UtcNow.Year}-{campaign.From}" : campaign.From;

                var day = DateTime.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                Simulate($"Кампания «{campaign.Id}», {day:yyyy-MM-dd}", new RotationState { Cycle = 3 }, new DateTimeOffset(day.AddHours(12), TimeSpan.Zero));
            }
        }

        private void Simulate(string title, RotationState state, DateTimeOffset now)
        {
            var target = new StoryRotation(_store, new RotationConfiguration(), new Random(1), new FixedTime(now));

            var events = new List<string>();

            for (var step = 0; step < 1000; step++)
            {
                var eventName = target.SelectEvent(state);
                events.Add(eventName);

                if (eventName == StoryRotation.TextsOverEvent)
                {
                    break;
                }

                StoryCatalog.TryParseEvent(eventName, out var number, out _);

                target.TryMarkIssued(state, eventName, _lastIntentNames[number]);
                target.TrackCompletion(state, new Dialog { IntentName = _lastIntentNames[number], Action = "saveToRepeat" });
            }

            TestContext.Out.WriteLine($"{title}:{Environment.NewLine}{string.Join(", ", events)}{Environment.NewLine}");

            Assert.That(events.Last(), Is.EqualTo(StoryRotation.TextsOverEvent), "Круг должен закончиться");
            Assert.That(events, Is.Unique, "За круг история выпадает один раз");
        }

        private sealed class FixedTime(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return now;
            }
        }
    }
}
