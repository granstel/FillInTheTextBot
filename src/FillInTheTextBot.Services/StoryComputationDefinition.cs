using System;

namespace FillInTheTextBot.Services
{
    /// <summary>
    /// Описание встроенного вычислителя для одной истории: какой контекст и параметр Dialogflow
    /// заполнить и как посчитать значение от даты. Используется в <see cref="StoryComputations"/>.
    /// </summary>
    internal sealed class StoryComputationDefinition
    {
        public string ContextName { get; init; }

        public string ParameterName { get; init; }

        public int LifeSpan { get; init; }

        public Func<DateTime, string> Compute { get; init; }
    }
}
