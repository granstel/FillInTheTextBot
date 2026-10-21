using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Правила ротации из Dialogflow/rotation.json. Истории указываются входными событиями.
    /// </summary>
    public sealed class RotationRules
    {
        private static readonly JsonSerializerSettings ParseSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Error
        };

        public static RotationRules Empty => new();

        /// <summary>Порядок первого круга. Истории не из списка идут следом по номеру.</summary>
        public string[] FirstCycle { get; set; } = [];

        /// <summary>Звенья каждой цепочки выдаются подряд.</summary>
        public string[][] Chains { get; set; } = [];

        /// <summary>Не участвуют в ротации, доступны по названию.</summary>
        public string[] Hidden { get; set; } = [];

        /// <exception cref="JsonException">Невалидный JSON или неизвестное поле.</exception>
        public static RotationRules Parse(string json)
        {
            var rules = JsonConvert.DeserializeObject<RotationRules>(json, ParseSettings)
                        ?? throw new JsonSerializationException("Пустой файл правил");

            return rules;
        }

        public IReadOnlyList<string> Validate(StoryCatalog catalog)
        {
            var errors = new List<string>();

            ValidateList(nameof(FirstCycle), FirstCycle, catalog, errors);
            ValidateList(nameof(Hidden), Hidden, catalog, errors);

            if (Chains is null)
            {
                errors.Add($"{nameof(Chains)}: должен быть массивом");

                return errors;
            }

            var chained = new HashSet<string>();

            foreach (var chain in Chains)
            {
                ValidateList(nameof(Chains), chain, catalog, errors);

                if (chain?.Length < 2)
                {
                    errors.Add($"{nameof(Chains)}: в цепочке меньше двух историй");
                }

                foreach (var eventName in chain?.Where(e => !chained.Add(e)) ?? [])
                {
                    errors.Add($"{nameof(Chains)}: «{eventName}» входит в несколько цепочек");
                }
            }

            return errors;
        }

        /// <summary>
        /// Первый круг: истории из <see cref="FirstCycle"/>, затем остальные по номеру, кроме скрытых.
        /// </summary>
        public IReadOnlyList<int> GetFirstCycle(StoryCatalog catalog)
        {
            var hidden = ToNumbers(Hidden, catalog).ToHashSet();

            var listed = ToNumbers(FirstCycle, catalog);
            var rest = catalog.Numbers;

            var firstCycle = listed.Concat(rest)
                .Distinct()
                .Where(number => !hidden.Contains(number))
                .ToList();

            return firstCycle;
        }

        private static IEnumerable<int> ToNumbers(IEnumerable<string> events, StoryCatalog catalog)
        {
            foreach (var eventName in events)
            {
                if (catalog.TryGetStoryByEntryEvent(eventName, out var number))
                {
                    yield return number;
                }
            }
        }

        private static void ValidateList(string listName, string[] events, StoryCatalog catalog, List<string> errors)
        {
            if (events is null)
            {
                errors.Add($"{listName}: должен быть массивом");

                return;
            }

            var unique = new HashSet<string>();

            foreach (var eventName in events)
            {
                if (!unique.Add(eventName))
                {
                    errors.Add($"{listName}: «{eventName}» повторяется");
                }

                if (!catalog.TryGetStoryByEntryEvent(eventName, out _))
                {
                    errors.Add($"{listName}: «{eventName}» не входное событие известной истории");
                }
            }
        }
    }
}
