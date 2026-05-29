using System;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests
{
    [TestFixture]
    public class StoryComputationsTests
    {
        private const string BeachVacationFirstPart = "text-37-1";

        [Test]
        public void TryBuildContext_TextWithoutComputation_False()
        {
            var result = StoryComputations.TryBuildContext("text-99", new DateTime(2026, 6, 1), out var context);

            Assert.False(result);
            Assert.Null(context);
        }

        [Test]
        public void TryBuildContext_NullTextKey_False()
        {
            var result = StoryComputations.TryBuildContext(null, new DateTime(2026, 6, 1), out var context);

            Assert.False(result);
            Assert.Null(context);
        }

        [Test]
        public void TryBuildContext_CaseInsensitive()
        {
            var result = StoryComputations.TryBuildContext("TEXT-37-1", new DateTime(2026, 8, 29), out var context);

            Assert.True(result);
            Assert.NotNull(context);
        }

        [Test]
        public void TryBuildContext_MidSummer_FillsContext()
        {
            // 1 июня 2026 → до 30 августа 90 дней
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 6, 1), out var context);

            Assert.True(result);
            Assert.AreEqual("summer-days", context.Name);
            Assert.AreEqual(2, context.LifeSpan);
            Assert.AreEqual("90", context.Parameters["daysLeft"]);
        }

        [Test]
        public void TryBuildContext_DayBeforeEnd_One()
        {
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 8, 29), out var context);

            Assert.True(result);
            Assert.AreEqual("1", context.Parameters["daysLeft"]);
        }

        [Test]
        public void TryBuildContext_EndOfSummer_FallbackNoContext()
        {
            // 30 августа: осталось меньше дня → значение не передаётся, срабатывает фоллбэк-вопрос
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 8, 30), out var context);

            Assert.False(result);
            Assert.Null(context);
        }

        [Test]
        public void TryBuildContext_AfterSummer_FallbackNoContext()
        {
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 9, 15), out var context);

            Assert.False(result);
            Assert.Null(context);
        }

        [Test]
        public void TryBuildContext_IgnoresTimeComponent()
        {
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 8, 29, 23, 59, 0), out var context);

            Assert.True(result);
            Assert.AreEqual("1", context.Parameters["daysLeft"]);
        }
    }
}
