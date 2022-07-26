using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class SourcePayload
    {
        public ICollection<Button> Buttons { get; set; }

        public IDictionary<string, string> Sounds { get; set; }

        public SourcePayload()
        {
            Buttons = new List<Button>();
            Sounds = new Dictionary<string, string>();
        }
    }
}
