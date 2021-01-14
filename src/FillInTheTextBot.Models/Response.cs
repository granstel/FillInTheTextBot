namespace FillInTheTextBot.Models
{
    public class Response
    {
        public static string NextTextIndexKey => nameof(NextTextIndex).ToUpper();

        public string ChatHash { get; set; }

        public string UserHash { get; set; }

        public string Text { get; set; }

        public string AlternativeText { get; set; }

        public bool Finished { get; set; }

        public Button[] Buttons { get; set; }

        public int NextTextIndex { get; set; }
    }
}