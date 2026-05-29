using System;
using System.Collections.Generic;
using System.Globalization;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services
{
    /// <summary>
    /// Встроенные вычислители значений для отдельных историй. Привязаны к ключу текста (textKey):
    /// когда бот собирается отрендерить такой текст, посчитанное значение кладётся в контекст Dialogflow
    /// и едет вместе с запросом рендера (без отдельного сетевого вызова), а в тексте подставляется через #context.
    /// Срабатывает при любом способе запуска истории (по названию, как «самая свежая», случайно и т.п.),
    /// потому что textKey — общий для всех точек входа.
    /// Если вычислитель вернул null, контекст не создаётся — в Dialogflow срабатывает фоллбэк
    /// (у параметра пустой defaultValue и заданы prompts), и значение спрашивается у игрока.
    /// </summary>
    public static class StoryComputations
    {
        /// <summary>Московское смещение (UTC+3, без перехода на летнее время).</summary>
        private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

        private sealed class Definition
        {
            public string ContextName { get; init; }
            public string ParameterName { get; init; }
            public int LifeSpan { get; init; }
            public Func<DateTime, string> Compute { get; init; }
        }

        /// <summary>Ключ — textKey истории, значение — что и куда вычислять.</summary>
        private static readonly IReadOnlyDictionary<string, Definition> Definitions =
            new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase)
            {
                // Каникулы на пляже: «сколько дней до конца лета» вместо вопроса игроку.
                ["text-37-1"] = new Definition
                {
                    ContextName = "summer-days",
                    ParameterName = "daysLeft",
                    LifeSpan = 2,
                    Compute = SummerDaysLeft
                }
            };

        /// <summary>
        /// Строит контекст с вычисленным значением для указанного текста, используя текущую московскую дату.
        /// Возвращает false, если для текста нет вычислителя или значение неприменимо (тогда context = null).
        /// </summary>
        public static bool TryBuildContext(string textKey, out Context context)
        {
            return TryBuildContext(textKey, MoscowToday(), out context);
        }

        /// <summary>Перегрузка с явной датой — для тестов.</summary>
        public static bool TryBuildContext(string textKey, DateTime today, out Context context)
        {
            context = null;

            if (string.IsNullOrEmpty(textKey) || !Definitions.TryGetValue(textKey, out var definition))
            {
                return false;
            }

            var value = definition.Compute(today);

            if (value == null)
            {
                return false;
            }

            context = new Context
            {
                Name = definition.ContextName,
                LifeSpan = definition.LifeSpan,
                Parameters = new Dictionary<string, string>
                {
                    [definition.ParameterName] = value
                }
            };

            return true;
        }

        /// <summary>Текущая дата по Москве (UTC+3).</summary>
        public static DateTime MoscowToday()
        {
            return DateTimeOffset.UtcNow.ToOffset(MoscowOffset).Date;
        }

        /// <summary>
        /// Сколько дней осталось до конца лета (30 августа) от указанной даты.
        /// Возвращает null, если осталось меньше одного дня (лето уже прошло).
        /// </summary>
        private static string SummerDaysLeft(DateTime today)
        {
            var endOfSummer = new DateTime(today.Year, 8, 30);

            var daysLeft = (endOfSummer - today.Date).Days;

            return daysLeft < 1 ? null : daysLeft.ToString(CultureInfo.InvariantCulture);
        }
    }
}
