using System.Collections.Generic;

namespace FillInTheTextBot.Models
{
    public class Response
    {
        public Response()
        {
            Payload = new Dictionary<string, string>();
        }

        public string ChatHash { get; set; }

        public string UserHash { get; set; }

        public string Text { get; set; }

        public string AlternativeText { get; set; }

        public bool Finished { get; set; }

        public Button[] Buttons { get; set; }

        public IDictionary<string, string> Payload { get; set; }
    }
}