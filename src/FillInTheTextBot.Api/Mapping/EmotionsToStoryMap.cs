using System.Collections.Generic;
using FillInTheTextBot.Models;

namespace FillInTheTextBot.Api.Mapping
{
    /// <summary>
    /// Map emotions for ready story to request source
    /// </summary>
    public static class EmotionsToStoryMap
    {
        public static IDictionary<Source, ICollection<string>> SourceEmotions = new Dictionary<Source, ICollection<string>>
        {
            { 
                Source.Sber,
                new []
                {
                    "radost",
                    "predvkusheniye",
                    "simpatiya",
                    "igrivost",
                    "udovolstvie",
                    "laugh"
                }
            }
        };
    }
}
