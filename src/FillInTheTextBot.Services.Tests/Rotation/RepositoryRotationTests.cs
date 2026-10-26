using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FillInTheTextBot.Services.Rotation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    /// <summary>
    /// Бот находит истории по именам файлов агента и читает rotation.json из master,
    /// поэтому соглашение об именах и файл правил проверяются до мерджа.
    /// </summary>
    [TestFixture]
    public class RepositoryRotationTests
    {
        private static readonly Regex StoryEventPattern = new(@"^text-\d+(-\d+)?$");

        private string _intentsPath;
        private string[] _fileNames;

        [OneTimeSetUp]
        public void InitFixture()
        {
            var dialogflowPath = Path.Combine(GetRepositoryRoot(), "Dialogflow");

            _intentsPath = Path.Combine(dialogflowPath, "FillInTheTextBot-eu", "intents");
            _fileNames = Directory.GetFiles(_intentsPath, "*.json").Select(Path.GetFileName).ToArray();
        }

        [Test]
        public void StoryFiles_EachHasEventMatchingFileName()
        {
            var catalog = StoryCatalog.FromFileNames(_fileNames);

            var mismatches = _fileNames
                .Select(fileName => (fileName, expected: GetExpectedEvent(fileName)))
                .Where(file => file.expected is not null)
                .Select(file => (file.fileName, file.expected, events: ReadEvents(file.fileName)))
                .Where(file => file.events.Length != 1 || file.events[0] != file.expected)
                .Select(file => $"{file.fileName}: ожидалось {file.expected}, есть [{string.Join(", ", file.events)}]")
                .ToList();


            Assert.That(catalog.Numbers, Is.Not.Empty);
            Assert.That(mismatches, Is.Empty);
        }

        [Test]
        public void StoryEvents_EachInFileMatchingPattern()
        {
            var misplaced = _fileNames
                .Where(fileName => !fileName.Contains("_usersays_"))
                .SelectMany(fileName => ReadEvents(fileName).Select(eventName => (fileName, eventName)))
                .Where(pair => StoryEventPattern.IsMatch(pair.eventName))
                .Where(pair => GetExpectedEvent(pair.fileName) != pair.eventName)
                .Select(pair => $"{pair.fileName}: {pair.eventName}")
                .ToList();


            Assert.That(misplaced, Is.Empty);
        }

        [Test]
        public void RotationRules_Valid()
        {
            var json = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "Dialogflow", "rotation.json"));

            var catalog = StoryCatalog.FromFileNames(_fileNames);


            var rules = RotationRules.Parse(json);
            var errors = rules.Validate(catalog);


            Assert.That(errors, Is.Empty);
        }

        private static string GetExpectedEvent(string fileName)
        {
            var match = Regex.Match(fileName, @"^(\d+)-.+-text(\d*)\.json$");

            if (!match.Success)
            {
                return null;
            }

            var number = match.Groups[1].Value;
            var part = match.Groups[2].Value;

            var expected = string.IsNullOrEmpty(part) ? $"text-{number}" : $"text-{number}-{part}";

            return expected;
        }

        private string[] ReadEvents(string fileName)
        {
            var json = File.ReadAllText(Path.Combine(_intentsPath, fileName));

            var intent = JObject.Parse(json);

            var events = intent["events"]?.Select(e => e.Value<string>("name")).ToArray() ?? [];

            return events;
        }

        private static string GetRepositoryRoot()
        {
            var directory = TestContext.CurrentContext.TestDirectory;

            while (directory is not null && !File.Exists(Path.Combine(directory, "src", "FillInTheTextBot.slnx")))
            {
                directory = Directory.GetParent(directory)?.FullName;
            }

            return directory ?? throw new InvalidOperationException("Не найден корень репозитория");
        }
    }
}
