namespace FillInTheTextBot.DialogflowImporter;

public class ImporterSettings
{
    public string? ZipPath { get; set; }
    public string Mode { get; set; } = "Restore";
    public bool Train { get; set; } = true;
    public string? JsonPath { get; set; }
    public List<TargetAgent> Targets { get; set; } = new();
}

public class TargetAgent
{
    public string? ProjectId { get; set; }
    public string? Region { get; set; }
    public string? JsonPath { get; set; }
}
