using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Ротация пользователя. Хранится на площадке строкой вида
    /// {"v":2,"cycle":3,"done":"//////8f","current":12,"next":34,"last":12}.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class RotationState
    {
        private const int CurrentVersion = 2;

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HashSet<int> _done = [];

        public RotationState() : this(CurrentVersion)
        {
        }

        [JsonConstructor]
        private RotationState(int v)
        {
            Version = v;
        }

        [JsonProperty("v", Order = 0)]
        private int Version { get; }

        /// <summary>0 — первый круг, он идёт в заданном порядке.</summary>
        [JsonProperty("cycle", Order = 1)]
        public int Cycle { get; set; }

        /// <summary>Закрытые в круге истории — битовая маска по номерам в base64.</summary>
        [JsonProperty("done", Order = 2)]
        private string Done
        {
            get => EncodeDone(_done);
            set => _done.UnionWith(DecodeDone(value));
        }

        /// <summary>Выдана и ещё не дособрана.</summary>
        [JsonProperty("current", Order = 3)]
        public int? Current { get; set; }

        /// <summary>Следующее звено цепочки.</summary>
        [JsonProperty("next", Order = 4)]
        public int? Next { get; set; }

        [JsonProperty("last", Order = 5)]
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
                var state = JsonConvert.DeserializeObject<RotationState>(value);

                return state?.Version == CurrentVersion ? state : null;
            }
            catch (Exception e) when (e is JsonException or FormatException)
            {
                return null;
            }
        }

        public string Serialize()
        {
            var value = JsonConvert.SerializeObject(this, SerializerSettings);

            return value;
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
