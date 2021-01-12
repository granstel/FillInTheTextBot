using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Request
    {
        public Request()
        {
            Payload = new Dictionary<string, string>();
        }

        public Source? Source { get; set; }

        public string ChatHash { get; set; }

        public string UserHash { get; set; }

        public string Text { get; set; }

        public string Language { get; set; }

        public string SessionId { get; set; }

        public bool? NewSession { get; set; }

        public string RequiredContext { get; set; }

        public bool ClearContexts { get; set; }

        public bool IsOldUser { get; set; }
        
        public bool HasScreen { get; set; }

        public string ClientId { get; set; }

        public IDictionary<string, string> Payload { get; set; }

        public void FillPayloadFrom(IDictionary<string, object> source)
        {
            foreach (var keyValuePair in source)
            {
                if (!Payload.ContainsKey(keyValuePair.Key))
                {
                    Payload.Add(keyValuePair.Key, keyValuePair.Value.ToString());
                }
            }
        }
    }
}