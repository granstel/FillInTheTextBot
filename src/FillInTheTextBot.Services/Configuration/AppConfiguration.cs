namespace FillInTheTextBot.Services.Configuration
{
    public class AppConfiguration
    {
        public HttpLogConfiguration HttpLog { get; set; }

        public DialogflowConfiguration[] DialogflowScopes { get; set; }

        public RedisConfiguration Redis { get; set; }

        public TracingConfiguration Tracing { get; set; }

        public ConversationConfiguration ConversationConfiguration { get; set; }
    }
}
