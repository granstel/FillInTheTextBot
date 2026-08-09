namespace FillInTheTextBot.Services.Configuration
{
    public class TracingConfiguration : Configuration
    {
        public bool Enabled { get; set; }

        public string Host { get; set; }

        public int? Port { get; set; }
    }
}