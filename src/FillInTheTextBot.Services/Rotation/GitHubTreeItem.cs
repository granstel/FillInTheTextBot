namespace FillInTheTextBot.Services.Rotation
{
    internal sealed class GitHubTreeItem
    {
        public string Path { get; set; }

        /// <summary>blob — файл, tree — каталог.</summary>
        public string Type { get; set; }
    }
}
