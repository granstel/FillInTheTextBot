using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class SourcePayload
    {
        public SourcePayload()
        {
            Buttons = new List<Button>();
            Replacements = new Dictionary<string, string>();
        }

        public ICollection<Button> Buttons { get; set; }

        public IDictionary<string, string> Replacements { get; set; }
    }
}