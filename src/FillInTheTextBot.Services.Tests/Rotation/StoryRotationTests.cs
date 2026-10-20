using System;
using System.Collections.Generic;
using System.Linq;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Configuration;
using FillInTheTextBot.Services.Rotation;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class StoryRotationTests
    {
        private static readonly Dictionary<int, string> IntentNames = new()
        {
            [1] = "1-One-text",
            [2] = "2-Two-text",
            [3] = "3-Secret-text",
            [33] = "33-LibraryMystery-text",
            [34] = "34-LibrarianMystery-text",
            [40] = "40-Forty-text"
        };

        private RotationStore _store;
        private ManualTime _time;

        private StoryRotation _target;

        [SetUp]
        public void InitTest()
        {
            var catalog = new StoryCatalog(new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [33] = 0, [34] = 2, [40] = 0 });

            var rules = new RotationRules
            {
                FirstCycle = ["text-40", "text-33", "text-34-1"],
                Chains = [["text-33", "text-34-1"]],
                Hidden = ["text-3"]
            };

            _store = new RotationStore();
            _store.Apply(new RotationSnapshot(catalog, rules, new RotationSources()));

            _time = new ManualTime();

            _target = new StoryRotation(_store, new RotationConfiguration(), new Random(1), _time);
        }

        [Test]
        public void SelectEvent_NewUserCompletesAll_FirstCycleOrderWithChainThenTextsOver()
        {
            var state = new RotationState();


            var events = PlayCycle(state);


            Assert.That(events, Is.EqualTo(new[] { "text-40", "text-33", "text-34-1", "text-1", "text-2", "texts-over" }));
            Assert.That(state.Cycle, Is.EqualTo(1));
        }

        [Test]
        public void SelectEvent_SecondCycle_RandomOrderOfAllUnits()
        {
            var state = new RotationState { Cycle = 1 };


            var events = PlayCycle(state);


            Assert.That(events.Last(), Is.EqualTo("texts-over"));
            Assert.That(events.Take(5), Is.EquivalentTo(new[] { "text-40", "text-33", "text-34-1", "text-1", "text-2" }));
            Assert.That(events.IndexOf("text-34-1"), Is.EqualTo(events.IndexOf("text-33") + 1));
        }

        [Test]
        public void SelectEvent_ManyCycles_EachOrderStartsNotWithLastOfPrevious()
        {
            var state = new RotationState { Cycle = 1 };
            var orders = new HashSet<string>();


            for (var cycle = 0; cycle < 30; cycle++)
            {
                var last = state.Last;
                var events = PlayCycle(state);

                Assert.That(events[0], Is.Not.EqualTo(last is null ? null : $"text-{last}"));

                orders.Add(string.Join(",", events));
            }


            Assert.That(orders.Count, Is.GreaterThan(1), "Каждый круг перемешивается заново");
        }

        [Test]
        public void SelectEvent_CurrentNotCompleted_AbandonedAndClosed()
        {
            var state = new RotationState();
            Issue(state, _target.SelectEvent(state));


            var eventName = _target.SelectEvent(state);


            Assert.That(eventName, Is.EqualTo("text-33"));
            Assert.That(state.IsDone(40), Is.True);
            Assert.That(state.Current, Is.Null);
        }

        [Test]
        public void SelectEvent_ChainHeadAbandoned_FollowerSkipped()
        {
            var state = new RotationState();
            state.MarkDone(40);
            Issue(state, _target.SelectEvent(state));


            var eventName = _target.SelectEvent(state);


            Assert.That(eventName, Is.EqualTo("text-1"));
        }

        [Test]
        public void SelectEvent_ChainHeadCompletedByName_FollowerFirst()
        {
            var state = new RotationState();
            Issue(state, "text-33");
            Complete(state, 33);


            var eventName = _target.SelectEvent(state);


            Assert.That(eventName, Is.EqualTo("text-34-1"));
        }

        [Test]
        public void SelectEvent_FollowerAlreadyDone_NotRepeated()
        {
            var state = new RotationState();
            state.MarkDone(34);
            Issue(state, "text-33");
            Complete(state, 33);


            var eventName = _target.SelectEvent(state);


            Assert.That(eventName, Is.EqualTo("text-40"));
        }

        [Test]
        public void SelectEvent_HiddenStory_NeverSelected()
        {
            var state = new RotationState { Cycle = 1 };


            var events = Enumerable.Range(0, 10).SelectMany(_ => PlayCycle(state));


            Assert.That(events, Does.Not.Contain("text-3"));
        }

        [Test]
        public void SelectEvent_Unavailable_SkippedUntilExpired()
        {
            var state = new RotationState();
            _target.MarkUnavailable("text-40");


            var whileUnavailable = _target.SelectEvent(state);
            _time.Now = _time.Now.AddMinutes(11);
            var afterExpiration = _target.SelectEvent(new RotationState());


            Assert.That(whileUnavailable, Is.EqualTo("text-33"));
            Assert.That(afterExpiration, Is.EqualTo("text-40"));
        }

        [Test]
        public void TryMarkIssued_OtherIntent_FalseStateUnchanged()
        {
            var state = new RotationState { Current = 1 };


            var result = _target.TryMarkIssued(state, "text-40", "Default Fallback Intent");


            Assert.That(result, Is.False);
            Assert.That(state.Current, Is.EqualTo(1));
            Assert.That(state.IsDone(1), Is.False);
        }

        [Test]
        public void TryMarkIssued_OtherStoryByName_PreviousAbandoned()
        {
            var state = new RotationState { Current = 1 };


            var result = _target.TryMarkIssued(state, "text-40", IntentNames[40]);


            Assert.That(result, Is.True);
            Assert.That(state.IsDone(1), Is.True);
            Assert.That(state.Current, Is.EqualTo(40));
            Assert.That(state.Last, Is.EqualTo(40));
        }

        [Test]
        public void TryMarkIssued_NextPart_SameStoryContinues()
        {
            var state = new RotationState { Current = 34, Last = 34 };


            var result = _target.TryMarkIssued(state, "text-34-2", "34-LibrarianMystery-text2");


            Assert.That(result, Is.True);
            Assert.That(state.Current, Is.EqualTo(34));
            Assert.That(state.IsDone(34), Is.False);
        }

        [Test]
        public void TryMarkIssued_TextsOver_True()
        {
            var state = new RotationState();


            var result = _target.TryMarkIssued(state, "texts-over", "TextsOver");


            Assert.That(result, Is.True);
        }

        [TestCase("34-LibrarianMystery-text1", false, "saveToRepeat", false)]
        [TestCase("34-LibrarianMystery-text2", true, "saveToRepeat", false)]
        [TestCase("34-LibrarianMystery-text2", false, "saveToRepeat", true)]
        [TestCase("34-LibrarianMystery-text2", false, "GetText", false)]
        public void TrackCompletion_Dialog_DoneOnlyWhenLastPartSaved(string intentName, bool parametersIncomplete,
            string action, bool expectedDone)
        {
            var state = new RotationState { Current = 34 };
            var dialog = new Dialog { IntentName = intentName, ParametersIncomplete = parametersIncomplete, Action = action };


            _target.TrackCompletion(state, dialog);


            Assert.That(state.IsDone(34), Is.EqualTo(expectedDone));
            Assert.That(state.Current, Is.EqualTo(expectedDone ? null : 34));
        }

        [Test]
        public void GetState_NoSnapshot_Null()
        {
            var target = new StoryRotation(new RotationStore(), new RotationConfiguration(), new Random(1), _time);


            var state = target.GetState(null, 0);


            Assert.That(state, Is.Null);
        }

        [Test]
        public void GetState_NoSerializedState_MigratedFromLegacyIndex()
        {
            var state = _target.GetState(null, 2);


            Assert.That(state.Cycle, Is.EqualTo(0));
            Assert.That(state.IsDone(40), Is.True);
            Assert.That(state.IsDone(33), Is.True);
            Assert.That(state.IsDone(34), Is.False);
        }

        [Test]
        public void GetState_SerializedState_LegacyIndexIgnored()
        {
            var state = _target.GetState("""{"v":2,"cycle":4,"done":""}""", 2);


            Assert.That(state.Cycle, Is.EqualTo(4));
            Assert.That(state.IsDone(40), Is.False);
        }

        private List<string> PlayCycle(RotationState state)
        {
            var events = new List<string>();

            for (var step = 0; step < 20; step++)
            {
                var eventName = _target.SelectEvent(state);
                events.Add(eventName);

                if (eventName == StoryRotation.TextsOverEvent)
                {
                    return events;
                }

                StoryCatalog.TryParseEvent(eventName, out var number, out _);

                Issue(state, eventName);
                Complete(state, number);
            }

            throw new InvalidOperationException("Круг не закончился");
        }

        private void Issue(RotationState state, string eventName)
        {
            StoryCatalog.TryParseEvent(eventName, out var number, out _);

            _target.TryMarkIssued(state, eventName, IntentNames[number]);
        }

        private void Complete(RotationState state, int number)
        {
            var intentName = number == 34 ? "34-LibrarianMystery-text2" : IntentNames[number];

            _target.TrackCompletion(state, new Dialog { IntentName = intentName, Action = "saveToRepeat" });
        }

        private sealed class ManualTime : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }
    }
}
