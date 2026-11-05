namespace FillInTheTextBot.Services.Configuration
{
    public class RotationConfiguration
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Каталог интентов агента в GitHub Trees API.</summary>
        public string StoriesUrl { get; set; }

        public string RulesUrl { get; set; }

        /// <summary>Токен GitHub только на чтение: поднимает лимит запросов к API.</summary>
        public string GitHubToken { get; set; }

        public int RefreshIntervalMinutes { get; set; } = 10;

        /// <summary>Сколько не выдавать историю, которую агент Dialogflow не знает.</summary>
        public int UnavailableMinutes { get; set; } = 10;
    }
}
