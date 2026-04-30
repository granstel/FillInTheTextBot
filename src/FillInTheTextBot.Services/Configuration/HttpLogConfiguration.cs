namespace FillInTheTextBot.Services.Configuration;

public class HttpLogConfiguration
{
    public bool Enabled { get; set; }

    public bool AddRequestIdHeader { get; set; }

    public string[] ExcludeBodiesWithWords { get; set; } = new string[0];

    public string[] IncludeEndpoints { get; set; } = new string[0];
}