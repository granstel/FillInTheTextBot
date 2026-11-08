using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Ротация пользователя. Хранится на площадке строкой вида
    /// {"Cycle":3,"Done":"//////8f","Current":12,"Next":34,"Last":12}.
    /// </summary>
    public sealed class RotationState
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly HashSet<int> _done = [];

        /// <summary>0 — первый круг, он идёт в заданном порядке.</summary>
        public int Cycle { get; set; }

        /// <summary>Закрытые в круге истории — битовая маска по номерам в base64.</summary>
        [JsonInclude]
        private string Done
        {
            get => EncodeDone(_done);
            set => _done.UnionWith(DecodeDone(value));
        }

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

        /// <summary>null — строка пустая или повреждена.</summary>
        public static RotationState Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            try
            {
                var state = JsonSerializer.Deserialize<RotationState>(value, SerializerOptions);

                return state;
            }
            catch (Exception e) when (e is JsonException or FormatException)
            {
                return null;
            }
        }

        public string Serialize()
        {
            var value = JsonSerializer.Serialize(this, SerializerOptions);

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
