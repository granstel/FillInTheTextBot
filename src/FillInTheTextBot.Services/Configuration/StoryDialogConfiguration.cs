namespace FillInTheTextBot.Services.Configuration;

public class StoryDialogConfiguration
{
    public string Endpoint { get; set; } = "http://storydialog:50051";

    public string ApiKey { get; set; }

    public string DefaultStoryId { get; set; } = "moon-picnic";

    public int TimeoutMilliseconds { get; set; } = 1500;
}
