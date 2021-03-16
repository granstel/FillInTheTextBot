namespace FillInTheTextBot.Services.Configuration
{
    public class HttpLogConfiguration
    {
        public HttpLogConfiguration()
        {
            ExcludeBodiesWithWords = new string[0];
            ExcludeEndpoints = new string[0];
        }

        public bool Enabled { get; set; }

        public bool AddRequestIdHeader { get; set; }

        public string[] ExcludeBodiesWithWords { get; set; }

        public string[] ExcludeEndpoints { get; set; }
    }
}
