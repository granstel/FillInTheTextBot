namespace FillInTheTextBot.Services.Rotation
{
    /// <summary>Ответ GitHub Trees API.</summary>
    internal sealed class GitHubTree
    {
        public GitHubTreeItem[] Tree { get; set; }

        public bool Truncated { get; set; }
    }
}
