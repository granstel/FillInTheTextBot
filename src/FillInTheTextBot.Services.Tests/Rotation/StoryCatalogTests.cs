using FillInTheTextBot.Services.Rotation;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class StoryCatalogTests
    {
        private static readonly string[] FileNames =
        {
            "33-LibraryMystery.json",
            "33-LibraryMystery-text.json",
            "33-LibraryMystery-text_usersays_ru.json",
            "34-LibrarianMystery.json",
            "34-LibrarianMystery-text1.json",
            "34-LibrarianMystery-text2.json",
            "34-LibrarianMystery-text2_usersays_ru.json",
            "TextsOver.json",
            "TextsList - long stories.json"
        };

        [Test]
        public void FromFileNames_StoryFiles_FindsStoriesAndParts()
        {
            var catalog = StoryCatalog.FromFileNames(FileNames);


            Assert.That(catalog.Numbers, Is.EqualTo(new[] { 33, 34 }));
            Assert.That(catalog.GetEntryEvent(33), Is.EqualTo("text-33"));
            Assert.That(catalog.GetEntryEvent(34), Is.EqualTo("text-34-1"));
            Assert.That(catalog.IsLastPart(33, 0), Is.True);
            Assert.That(catalog.IsLastPart(34, 1), Is.False);
            Assert.That(catalog.IsLastPart(34, 2), Is.True);
        }

        [TestCase("text-33", true, 33)]
        [TestCase("text-34-1", true, 34)]
        [TestCase("text-34-2", false, 34)]
        [TestCase("text-34", false, 34)]
        [TestCase("text-35", false, 35)]
        [TestCase("texts-over", false, 0)]
        public void TryGetStoryByEntryEvent_Event_RecognizesOnlyEntryEvents(string eventName, bool expected, int expectedNumber)
        {
            var catalog = StoryCatalog.FromFileNames(FileNames);


            var result = catalog.TryGetStoryByEntryEvent(eventName, out var number);


            Assert.That(result, Is.EqualTo(expected));
            Assert.That(number, Is.EqualTo(expectedNumber));
        }

        [TestCase("text-7", true, 7, 0)]
        [TestCase("text-34-2", true, 34, 2)]
        [TestCase("texts-over", false, 0, 0)]
        [TestCase(null, false, 0, 0)]
        public void TryParseEvent_Event_ParsesNumberAndPart(string eventName, bool expected, int expectedNumber, int expectedPart)
        {
            var result = StoryCatalog.TryParseEvent(eventName, out var number, out var part);


            Assert.That(result, Is.EqualTo(expected));
            Assert.That(number, Is.EqualTo(expectedNumber));
            Assert.That(part, Is.EqualTo(expectedPart));
        }
    }
}
