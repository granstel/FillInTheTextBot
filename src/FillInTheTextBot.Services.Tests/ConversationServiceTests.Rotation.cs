using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using FillInTheTextBot.Models;
using FillInTheTextBot.Services.Rotation;
using Moq;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    public partial class ConversationServiceTests
    {
        #region Ротация

        private void SetupRotation()
        {
            var catalog = new StoryCatalog(new Dictionary<int, int> { [1] = 0, [2] = 0, [33] = 0, [34] = 2 });

            var rules = new RotationRules
            {
                FirstCycle = ["text-2", "text-1"],
                Chains = [["text-33", "text-34-1"]]
            };

            _rotationStore.Apply(new RotationSnapshot(catalog, rules, new RotationSources()));
        }

        private void SetupTextDialog(string textKey, string intentName)
        {
            _dialogflowService
                .Setup(s => s.GetResponseAsync($"event:{textKey}", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<Context>>()))
                .ReturnsAsync(new Dialog { IntentName = intentName, Response = intentName });
        }

        [Test]
        public async Task GetResponseAsync_RotationLoadedNewUser_FirstStoryOfFirstCycle()
        {
            SetupRotation();
            SetupDialog(new Dialog { Action = "GetText", Response = "Поехали!" });
            SetupTextDialog("text-2", "2-Two-text");


            var result = await _target.GetResponseAsync(new Request());


            var state = RotationState.Parse(result.RotationState);

            Assert.That(result.Text, Is.EqualTo("Поехали!  2-Two-text"));
            Assert.That(state.Current, Is.EqualTo(2));
            Assert.That(state.Last, Is.EqualTo(2));
            string[] texts;
            _cache.Verify(c => c.TryGet("Texts", out texts, It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public async Task GetResponseAsync_LegacyIndex_ContinuesFirstCycle()
        {
            SetupRotation();
            SetupDialog(new Dialog { Action = "GetText" });
            SetupTextDialog("text-1", "1-One-text");


            var result = await _target.GetResponseAsync(new Request { NextTextIndex = 1 });


            var state = RotationState.Parse(result.RotationState);

            Assert.That(state.Current, Is.EqualTo(1));
            Assert.That(state.IsDone(2), Is.True);
        }

        [Test]
        public async Task GetResponseAsync_AllAttemptsNotRecognized_PauseHintInsteadOfStory()
        {
            SetupRotation();
            SetupDialog(new Dialog { Action = "GetText" });
            SetupTextDialog("text-2", "Default Fallback Intent");
            SetupTextDialog("text-1", "Default Fallback Intent");
            SetupTextDialog("text-33", "Default Fallback Intent");
            SetupTextDialog("texts-over", "TextsOver");


            var result = await _target.GetResponseAsync(new Request());


            var state = RotationState.Parse(result.RotationState);

            Assert.That(result.Text, Does.EndWith("TextsOver"));
            Assert.That(state.Cycle, Is.EqualTo(0), "Круг не закончился — истории только недоступны");
            Assert.That(state.IsDone(2) || state.IsDone(1) || state.IsDone(33), Is.False);
        }

        [Test]
        public async Task GetResponseAsync_LastRemainingStoryNotRecognized_PausePreservesProgress()
        {
            SetupRotation();
            SetupDialog(new Dialog { Action = "GetText" });
            SetupTextDialog("text-1", "Default Fallback Intent");
            SetupTextDialog("texts-over", "TextsOver");

            var state = new RotationState { Last = 33 };
            state.MarkDone(2);
            state.MarkDone(33);
            state.MarkDone(34);
            var beforePause = state.Serialize();


            var result = await _target.GetResponseAsync(new Request { RotationState = beforePause });
            var nextResult = await _target.GetResponseAsync(new Request { RotationState = result.RotationState });


            Assert.That(result.Text, Does.EndWith("TextsOver"));
            Assert.That(nextResult.Text, Does.EndWith("TextsOver"));
            Assert.That(result.RotationState, Is.EqualTo(beforePause));
            Assert.That(nextResult.RotationState, Is.EqualTo(beforePause));
        }

        [Test]
        public async Task GetResponseAsync_StoryEventNotRecognized_NextStoryIssued()
        {
            SetupRotation();
            SetupDialog(new Dialog { Action = "GetText" });
            SetupTextDialog("text-2", "Default Fallback Intent");
            SetupTextDialog("text-1", "1-One-text");


            var result = await _target.GetResponseAsync(new Request());


            var state = RotationState.Parse(result.RotationState);

            Assert.That(result.Text, Does.EndWith("1-One-text"));
            Assert.That(state.Current, Is.EqualTo(1));
            Assert.That(state.IsDone(2), Is.False, "Неизвестная агенту история не считается пройденной");
        }

        [Test]
        public async Task GetResponseAsync_StoryByName_PreviousAbandoned()
        {
            SetupRotation();
            var dialog = new Dialog { Action = "GetText" };
            dialog.Parameters.Add("textKey", "text-2");
            SetupDialog(dialog);
            SetupTextDialog("text-2", "2-Two-text");

            var request = new Request { RotationState = new RotationState { Current = 1 }.Serialize() };


            var result = await _target.GetResponseAsync(request);


            var state = RotationState.Parse(result.RotationState);

            Assert.That(state.Current, Is.EqualTo(2));
            Assert.That(state.IsDone(1), Is.True);
        }

        [Test]
        public async Task GetResponseAsync_ChainHeadCompleted_DoneAndNextLinkPending()
        {
            SetupRotation();
            SetupDialog(new Dialog { Action = "saveToRepeat", IntentName = "33-Head-text" });

            var request = new Request { RotationState = new RotationState { Current = 33 }.Serialize() };


            var result = await _target.GetResponseAsync(request);


            var state = RotationState.Parse(result.RotationState);

            Assert.That(state.IsDone(33), Is.True);
            Assert.That(state.Current, Is.Null);
            Assert.That(state.Next, Is.EqualTo(34));
        }

        [Test]
        public async Task GetResponseAsync_RotationNotLoaded_StatePassedThrough()
        {
            SetupDialog(new Dialog());

            var request = new Request { RotationState = _fixture.Create<string>() };


            var result = await _target.GetResponseAsync(request);


            Assert.That(result.RotationState, Is.EqualTo(request.RotationState));
        }

        #endregion Ротация
    }
}
