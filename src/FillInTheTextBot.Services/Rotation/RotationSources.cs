namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>Исходные данные ротации в том виде, в каком их отдал GitHub.</summary>
    public sealed class RotationSources
    {
        public string StoriesETag { get; set; }

        public string[] StoryFiles { get; set; } = [];

        public string RulesETag { get; set; }

        /// <summary>null — файла правил нет.</summary>
        public string RulesJson { get; set; }
    }
}
