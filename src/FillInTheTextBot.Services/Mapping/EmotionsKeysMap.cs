using System.Collections.Generic;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services.Mapping
{
    public static class EmotionsKeysMap
    {
        public static IDictionary<Source, string> SourceEmotionsKey = new Dictionary<Source, string>
        {
            { Source.Sber, "sberEmotion" }
        };
    }
}
