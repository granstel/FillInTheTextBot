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

            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryBuildContext_NullTextKey_False()
        {
            var result = StoryComputations.TryBuildContext(null, new DateTime(2026, 6, 1), out var context);

            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryBuildContext_CaseInsensitive()
        {
            var result = StoryComputations.TryBuildContext("TEXT-37-1", new DateTime(2026, 8, 29), out var context);

            Assert.That(result, Is.True);
            Assert.That(context, Is.Not.Null);
        }

        [Test]
        public void TryBuildContext_MidSummer_FillsContext()
        {
            // 1 июня 2026 → до 30 августа 90 дней
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 6, 1), out var context);

            Assert.That(result, Is.True);
            Assert.That(context.Name, Is.EqualTo("summer-days"));
            Assert.That(context.LifeSpan, Is.EqualTo(2));
            Assert.That(context.Parameters["daysLeft"], Is.EqualTo("90"));
        }

        [Test]
        public void TryBuildContext_SeasonStart_May29_FillsContext()
        {
            // 29 мая — старт сезона: до 30 августа 93 дня
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 5, 29), out var context);

            Assert.That(result, Is.True);
            Assert.That(context.Parameters["daysLeft"], Is.EqualTo("93"));
        }

        [Test]
        public void TryBuildContext_BeforeSeason_FallbackNoContext()
        {
            // 28 мая — ещё до старта сезона → фоллбэк-вопрос
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 5, 28), out var context);

            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryBuildContext_NewYear_FallbackNoContext()
        {
            // 1 января — вне сезона → значение не передаётся, бот спрашивает число
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2027, 1, 1), out var context);

            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryBuildContext_DayBeforeEnd_One()
        {
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 8, 29), out var context);

            Assert.That(result, Is.True);
            Assert.That(context.Parameters["daysLeft"], Is.EqualTo("1"));
        }

        [Test]
        public void TryBuildContext_EndOfSummer_FallbackNoContext()
        {
            // 30 августа: осталось меньше дня → значение не передаётся, срабатывает фоллбэк-вопрос
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 8, 30), out var context);

            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryBuildContext_AfterSummer_FallbackNoContext()
        {
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 9, 15), out var context);

            Assert.That(result, Is.False);
            Assert.That(context, Is.Null);
        }

        [Test]
        public void TryBuildContext_IgnoresTimeComponent()
        {
            var result = StoryComputations.TryBuildContext(BeachVacationFirstPart, new DateTime(2026, 8, 29, 23, 59, 0), out var context);

            Assert.That(result, Is.True);
            Assert.That(context.Parameters["daysLeft"], Is.EqualTo("1"));
        }
    }
}
