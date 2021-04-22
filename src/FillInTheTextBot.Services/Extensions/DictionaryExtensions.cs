using System.Collections.Generic;
using System.Linq;

namespace FillInTheTextBot.Services.Extensions
{
    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
        {
            if (dictionary?.Any() != true)
            {
                return defaultValue;
            }

            if (key == null)
            {
                return defaultValue;
            }

            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
