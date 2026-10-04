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

        /// <summary>При пересечении кампаний первой идёт та, что выше в списке.</summary>
        public RotationCampaign[] Campaigns { get; set; } = [];

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
            ValidateChains(catalog, errors);
            ValidateCampaigns(catalog, errors);

            return errors;
        }

        private void ValidateChains(StoryCatalog catalog, List<string> errors)
        {
            if (Chains is null)
            {
                errors.Add($"{nameof(Chains)}: должен быть массивом");

                return;
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
        }

        private void ValidateCampaigns(StoryCatalog catalog, List<string> errors)
        {
            if (Campaigns is null)
            {
                errors.Add($"{nameof(Campaigns)}: должен быть массивом");

                return;
            }

            var ids = new HashSet<string>();

            foreach (var campaign in Campaigns)
            {
                var name = $"{nameof(Campaigns)} «{campaign?.Id}»";

                if (string.IsNullOrEmpty(campaign?.Id) || !ids.Add(campaign.Id))
                {
                    errors.Add($"{name}: id пустой или повторяется");
                }

                if (campaign is null)
                {
                    continue;
                }

                var isFromValid = RotationCampaign.TryParseDate(campaign.From, out var from, out var isFromYearly);
                var isToValid = RotationCampaign.TryParseDate(campaign.To, out var to, out var isToYearly);

                if (!isFromValid || !isToValid || isFromYearly != isToYearly || !isFromYearly && from > to)
                {
                    errors.Add($"{name}: даты должны быть MM-DD или YYYY-MM-DD в одном формате, from не позже to");
                }

                if (campaign.Texts?.Length == 0)
                {
                    errors.Add($"{name}: нет историй");
                }

                ValidateList(name, campaign.Texts, catalog, errors);
            }
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
