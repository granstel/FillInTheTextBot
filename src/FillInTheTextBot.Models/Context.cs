using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Context
    {
        public string Name { get; set; }

        public int LifeSpan { get; set; }

        public IDictionary<string, string> Parameters { get; set; }
    }
}
