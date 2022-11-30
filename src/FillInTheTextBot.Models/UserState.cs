namespace FillInTheTextBot.Models
{
    public class UserState
    {
        public bool IsOldUser { get; set; }
        
        public int NextTextIndex { get; set; }

        public string ScopeKey { get; set; }
    }
}
