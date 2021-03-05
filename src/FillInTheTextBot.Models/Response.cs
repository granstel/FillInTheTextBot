using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Response
    {
        public static string NextTextIndexStorageKey => nameof(NextTextIndex).ToUpper();
        public static string ScopeStorageKey => nameof(ScopeKey).ToUpper();

        public string ChatHash { get; set; }

        public string UserHash { get; set; }

        public string Text { get; set; }

        public string AlternativeText { get; set; }

        public bool Finished { get; set; }

        public Button[] Buttons { get; set; }

        public int NextTextIndex { get; set; }

        public string ScopeKey { get; set; }
        
        public IDictionary<string, string> Emotions { get; set; }
    }
}