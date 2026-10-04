using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Ротация пользователя. Хранится на площадке строкой вида
    /// {"v":2,"cycle":3,"done":"//////8f","current":12,"next":34,"last":12}.
    /// </summary>
    public sealed class RotationState
    {
        private const int Version = 2;

        private readonly HashSet<int> _done = [];

        /// <summary>0 — первый круг, он идёт в заданном порядке.</summary>
        public int Cycle { get; set; }

        /// <summary>Выдана и ещё не дособрана.</summary>
        public int? Current { get; set; }

        /// <summary>Следующее звено цепочки.</summary>
        public int? Next { get; set; }

        public int? Last { get; set; }

        public bool IsDone(int number)
        {
            return _done.Contains(number);
        }

        public void MarkDone(int number)
        {
            _done.Add(number);
        }

        public void StartNextCycle()
        {
            Cycle++;
            _done.Clear();
        }

        /// <summary>
        /// Из прежнего индекса NEXTTEXTINDEX: выданные истории первого круга считаются закрытыми.
        /// </summary>
        public static RotationState FromLegacyIndex(int nextTextIndex, IReadOnlyList<int> firstCycle)
        {
            var state = new RotationState
            {
                Cycle = nextTextIndex >= firstCycle.Count ? 1 : 0
            };

            state._done.UnionWith(firstCycle.Take(nextTextIndex));

            return state;
        }

        /// <summary>null — строка пустая, повреждена или другой версии.</summary>
        public static RotationState Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            try
            {
                var json = JObject.Parse(value);

                if (json.Value<int?>("v") != Version)
                {
                    return null;
                }

                var state = new RotationState
                {
                    Cycle = json.Value<int?>("cycle") ?? 0,
                    Current = json.Value<int?>("current"),
                    Next = json.Value<int?>("next"),
                    Last = json.Value<int?>("last")
                };

                var done = DecodeDone(json.Value<string>("done"));
                state._done.UnionWith(done);

                return state;
            }
            catch (Exception e) when (e is JsonException or FormatException or InvalidCastException)
            {
                return null;
            }
        }

        public string Serialize()
        {
            var json = new JObject
            {
                ["v"] = Version,
                ["cycle"] = Cycle,
                ["done"] = EncodeDone(_done)
            };

            AddIfSet(json, "current", Current);
            AddIfSet(json, "next", Next);
            AddIfSet(json, "last", Last);

            var value = json.ToString(Formatting.None);

            return value;
        }

        private static void AddIfSet(JObject json, string name, int? value)
        {
            if (value is not null)
            {
                json[name] = value.Value;
            }
        }

        private static string EncodeDone(ICollection<int> done)
        {
            if (done.Count == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[done.Max() / 8 + 1];

            foreach (var number in done)
            {
                bytes[number / 8] |= (byte)(1 << number % 8);
            }

            var encoded = Convert.ToBase64String(bytes);

            return encoded;
        }

        private static IEnumerable<int> DecodeDone(string encoded)
        {
            var bytes = Convert.FromBase64String(encoded ?? string.Empty);

            for (var number = 0; number < bytes.Length * 8; number++)
            {
                if ((bytes[number / 8] & 1 << number % 8) != 0)
                {
                    yield return number;
                }
            }
        }
    }
}
