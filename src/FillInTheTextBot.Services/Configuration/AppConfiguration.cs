namespace FillInTheTextBot.Services.Configuration
{
    public class AppConfiguration
    {
        /// <summary>Порт служебных эндпоинтов; прокси наружу его не публикует.</summary>
        public int ServicePort { get; set; }

        public HttpLogConfiguration HttpLog { get; set; }

        public DialogflowConfiguration[] Dialogflow { get; set; }

        public RedisConfiguration Redis { get; set; }

        public TracingConfiguration Tracing { get; set; }

        public ConversationConfiguration Conversation { get; set; }

        public ShutdownConfiguration Shutdown { get; set; }

        public RotationConfiguration Rotation { get; set; }
    }
}
