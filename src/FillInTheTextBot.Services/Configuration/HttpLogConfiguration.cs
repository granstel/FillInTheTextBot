namespace FillInTheTextBot.Services.Configuration
{
    public class HttpLogConfiguration
    {
        public HttpLogConfiguration()
        {
            IncludeEndpoints = new string[0];
        }

        public bool Enabled { get; set; }


        public string[] IncludeEndpoints { get; set; }
    }
}
