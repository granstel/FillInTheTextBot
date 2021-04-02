using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Payload
    {
        public IDictionary<Appeal, string[]> Words = new Dictionary<Appeal, string[]>();
    }
}
