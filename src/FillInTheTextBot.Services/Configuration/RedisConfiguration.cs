namespace FillInTheTextBot.Services.Configuration
{
    public class RedisConfiguration : Configuration
    {
        private string _connectionString;

        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = ExpandVariable(value);
        }

        public string KeyPrefix { get; set; }
    }
}
