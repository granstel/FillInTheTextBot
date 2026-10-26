using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Истории агента, найденные по именам файлов интентов: номер истории и число её частей.
    /// У одночастной истории частей 0, её событие — text-NN; у многочастной — text-NN-K.
    /// </summary>
    public sealed partial class StoryCatalog
    {
        private readonly SortedDictionary<int, int> _parts;

        public StoryCatalog(IDictionary<int, int> parts)
        {
            _parts = new SortedDictionary<int, int>(parts);
        }

        public IReadOnlyCollection<int> Numbers => _parts.Keys;

        public static StoryCatalog FromFileNames(IEnumerable<string> fileNames)
        {
            var parts = new Dictionary<int, int>();

            foreach (var fileName in fileNames)
            {
                var match = FileNamePattern().Match(fileName);

                if (!match.Success)
                {
                    continue;
                }

                var number = int.Parse(match.Groups[1].Value);
                var part = ParsePart(match.Groups[2].Value);

                parts[number] = parts.TryGetValue(number, out var known) && known > part ? known : part;
            }

            var catalog = new StoryCatalog(parts);

            return catalog;
        }

        public static bool TryParseEvent(string eventName, out int number, out int part)
        {
            number = 0;
            part = 0;

            var match = EventPattern().Match(eventName ?? string.Empty);

            if (!match.Success)
            {
                return false;
            }

            number = int.Parse(match.Groups[1].Value);
            part = ParsePart(match.Groups[2].Value);

            return true;
        }

        public bool Contains(int number)
        {
            return _parts.ContainsKey(number);
        }

        public string GetEntryEvent(int number)
        {
            var eventName = _parts[number] == 0 ? $"text-{number}" : $"text-{number}-1";

            return eventName;
        }

        /// <summary>
        /// Событие, с которого история начинается: text-NN у одночастной, text-NN-1 у многочастной.
        /// </summary>
        public bool TryGetStoryByEntryEvent(string eventName, out int number)
        {
            var isStory = TryParseEvent(eventName, out number, out _) && Contains(number);

            return isStory && string.Equals(GetEntryEvent(number), eventName);
        }

        public bool IsLastPart(int number, int part)
        {
            var isLast = _parts.TryGetValue(number, out var parts) && parts == part;

            return isLast;
        }

        private static int ParsePart(string value)
        {
            var part = string.IsNullOrEmpty(value) ? 0 : int.Parse(value);

            return part;
        }

        [GeneratedRegex(@"^(\d+)-.+-text(\d*)\.json$")]
        private static partial Regex FileNamePattern();

        [GeneratedRegex(@"^text-(\d+)(?:-(\d+))?$")]
        private static partial Regex EventPattern();
    }
}
