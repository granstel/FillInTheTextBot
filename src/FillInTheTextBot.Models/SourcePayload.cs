using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class SourcePayload
    {
        public ICollection<Button> Buttons { get; set; }

        public IDictionary<string, string> Replacements { get; set; }

        public SourcePayload()
        {
            Buttons = new List<Button>();
            Replacements = new Dictionary<string, string>();
        }
    }
}
