using System;
using System.Globalization;

namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>
    /// Пока кампания активна, её истории идут в начале каждого круга.
    /// Даты включительно: MM-DD — ежегодно (в том числе через Новый год), YYYY-MM-DD — разово.
    /// </summary>
    public sealed class RotationCampaign
    {
        private const int YearlyDateLength = 5;

        /// <summary>Високосный, чтобы ежегодная дата 02-29 была допустимой.</summary>
        private const int YearlyBaseYear = 2000;

        public string Id { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        /// <summary>false — порядок как в <see cref="Texts"/>, true — случайный на каждом круге.</summary>
        public bool Shuffle { get; set; }

        /// <summary>Вне дат истории кампании не выпадают в ротации.</summary>
        public bool HideOutside { get; set; }

        public string[] Texts { get; set; } = [];

        public bool IsActive(DateTime today)
        {
            if (!TryParseDate(From, out var from, out var isYearly) || !TryParseDate(To, out var to, out _))
            {
                return false;
            }

            if (!isYearly)
            {
                return from <= today.Date && today.Date <= to;
            }

            var day = new DateTime(YearlyBaseYear, today.Month, today.Day);

            var isActive = from <= to
                ? from <= day && day <= to
                : day >= from || day <= to;

            return isActive;
        }

        internal static bool TryParseDate(string value, out DateTime date, out bool isYearly)
        {
            isYearly = value?.Length == YearlyDateLength;

            var text = isYearly ? $"{YearlyBaseYear}-{value}" : value;

            var isParsed = DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

            return isParsed;
        }
    }
}
