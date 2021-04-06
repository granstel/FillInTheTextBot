using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Payload
    {
        public Payload()
        {
            Words = new Dictionary<Appeal, string[]>();
            Buttons = new List<Button>();
            ButtonsForSource = new Dictionary<Source, ICollection<Button>>();
        }

        public IDictionary<Appeal, string[]> Words { get; set; }

        public ICollection<Button> Buttons { get; set; }

        public IDictionary<Source, ICollection<Button>> ButtonsForSource { get; set; }
    }
}
