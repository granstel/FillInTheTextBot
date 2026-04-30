using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class SourcePayload
    {
        public ICollection<Button> Buttons { get; set; } = new List<Button>();

        public IDictionary<string, string> Replacements { get; set; } = new Dictionary<string, string>();
    }
}