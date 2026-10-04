using FillInTheTextBot.Services.Rotation;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Rotation
{
    [TestFixture]
    public class RotationStateTests
    {
        [Test]
        public void Serialize_State_ParsedBack()
        {
            var state = new RotationState { Cycle = 3, Current = 12, Next = 34, Last = 12 };
            state.MarkDone(0);
            state.MarkDone(12);
            state.MarkDone(52);


            var parsed = RotationState.Parse(state.Serialize());


            Assert.That(parsed.Cycle, Is.EqualTo(3));
            Assert.That(parsed.Current, Is.EqualTo(12));
            Assert.That(parsed.Next, Is.EqualTo(34));
            Assert.That(parsed.Last, Is.EqualTo(12));
            Assert.That(parsed.IsDone(0), Is.True);
            Assert.That(parsed.IsDone(12), Is.True);
            Assert.That(parsed.IsDone(52), Is.True);
            Assert.That(parsed.IsDone(1), Is.False);
        }

        [Test]
        public void Serialize_ManyDone_Compact()
        {
            var state = new RotationState();

            for (var number = 0; number <= 52; number++)
            {
                state.MarkDone(number);
            }


            var serialized = state.Serialize();


            Assert.That(serialized, Is.EqualTo("""{"v":2,"cycle":0,"done":"////////Hw=="}"""));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("12")]
        [TestCase("not json")]
        [TestCase("""{"v":1,"cycle":0}""")]
        [TestCase("""{"v":2,"done":"*"}""")]
        [TestCase("""{"v":2,"cycle":"x"}""")]
        public void Parse_Invalid_Null(string value)
        {
            var state = RotationState.Parse(value);


            Assert.That(state, Is.Null);
        }

        [Test]
        public void StartNextCycle_DoneStories_ClearedAndCycleIncremented()
        {
            var state = new RotationState { Cycle = 1 };
            state.MarkDone(5);


            state.StartNextCycle();


            Assert.That(state.Cycle, Is.EqualTo(2));
            Assert.That(state.IsDone(5), Is.False);
        }

        [Test]
        public void FromLegacyIndex_InsideFirstCycle_IssuedAreDone()
        {
            var state = RotationState.FromLegacyIndex(2, [7, 3, 9]);


            Assert.That(state.Cycle, Is.EqualTo(0));
            Assert.That(state.IsDone(7), Is.True);
            Assert.That(state.IsDone(3), Is.True);
            Assert.That(state.IsDone(9), Is.False);
        }

        [TestCase(3)]
        [TestCase(5)]
        public void FromLegacyIndex_PastFirstCycle_SecondCycleAllDone(int nextTextIndex)
        {
            var state = RotationState.FromLegacyIndex(nextTextIndex, [7, 3, 9]);


            Assert.That(state.Cycle, Is.EqualTo(1));
            Assert.That(state.IsDone(9), Is.True);
        }
    }
}
