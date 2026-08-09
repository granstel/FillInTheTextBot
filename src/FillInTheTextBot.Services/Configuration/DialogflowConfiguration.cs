namespace FillInTheTextBot.Services.Configuration
{
    public class DialogflowConfiguration
    {
        public virtual string ScopeId { get; set; }

        public virtual string ProjectId { get; set; }

        public virtual string JsonPath { get; set; }

        public virtual string Region { get; set; }

        public virtual string LanguageCode => "ru";

        public bool LogQuery { get; set; }

        public bool DoNotUseForNewSessions { get; set; }

        /// <summary>
        /// Адрес локального эмулятора Dialogflow. Если задан, клиент идёт в него
        /// по незашифрованному каналу и без учётных данных Google.
        /// Используется только в интеграционных тестах и при локальном запуске.
        /// </summary>
        public virtual string EmulatorEndpoint { get; set; }
    }
}
