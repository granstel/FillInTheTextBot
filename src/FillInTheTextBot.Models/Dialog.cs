using System.Collections.Generic;
using System.Linq;

namespace FillInTheTextBot.Models
{
    public class Dialog
    {
        public Dialog()
        {
            Parameters = new Dictionary<string, string>();
            Buttons = new Button[0];
        }

        public IDictionary<string, string> Parameters { get; set; }

        public bool EndConversation { get; set; }

        public bool ParametersIncomplete { get; set; }

        public string Response { get; set; }

        public string Action { get; set; }

        public Button[] Buttons { get; set; }

        public string ScopeKey { get; set; }

        public Payload Payload { get; set; }

        public IEnumerable<string> GetParameters(string key)
        {
            return Parameters?.Where(p => string.Equals(p.Key, key)).Select(p => p.Value);
        }
    }
}
