using System.Collections.Generic;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Services.Mapping
{
    /// <summary>
    /// Map emotion key to request source
    /// </summary>
    public static class EmotionsKeysMap
    {
        /// <summary>
        /// Emotion keys for sources
        /// </summary>
        public static IDictionary<Source, string> SourceEmotionKeys = new Dictionary<Source, string>
        {
            { Source.Sber, "sberEmotion" }
        };
    }
}
