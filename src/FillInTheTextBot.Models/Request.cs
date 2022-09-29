using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Request
    {
        public Request()
        {
            RequiredContexts = new List<Context>();
        }

        public static string IsOldUserKey => nameof(IsOldUser).ToUpper();

        public Source Source { get; set; }

        public string ChatHash { get; set; }

        public string UserHash { get; set; }

        public string Text { get; set; }

        public string Language { get; set; }

        public string SessionId { get; set; }

        public bool? NewSession { get; set; }

        public List<Context> RequiredContexts { get; set; }

        public bool IsOldUser { get; set; }
        
        public bool HasScreen { get; set; }

        public string ClientId { get; set; }

        public int NextTextIndex { get; set; }

        public ICollection<string> PassedTexts { get; set; }

        public string ScopeKey { get; set; }

        public Appeal Appeal { get; set; }
    }
}