using FillInTheTextBot.DialogflowImporter;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using Grpc.Auth;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "DFIMPORT_")
    .AddCommandLine(args)
    .Build();

var settings = config.Get<ImporterSettings>() ?? new ImporterSettings();

if (string.IsNullOrWhiteSpace(settings.ZipPath) || !File.Exists(settings.ZipPath))
{
    Console.Error.WriteLine($"Zip not found: '{settings.ZipPath}'. Pass --ZipPath=path/to/agent.zip or set it in appsettings.json.");
    return 1;
}

if (settings.Targets is null || settings.Targets.Count == 0)
{
    Console.Error.WriteLine("No targets configured. Add entries to 'Targets' in appsettings.json.");
    return 1;
}

var zipBytes = await File.ReadAllBytesAsync(settings.ZipPath);
var content = ByteString.CopyFrom(zipBytes);
Console.WriteLine($"Loaded {zipBytes.Length} bytes from {settings.ZipPath}");
Console.WriteLine($"Mode: {settings.Mode}, Train: {settings.Train}, Targets: {settings.Targets.Count}");

var failures = 0;
foreach (var target in settings.Targets)
{
    if (string.IsNullOrWhiteSpace(target.ProjectId))
    {
        Console.Error.WriteLine("Skipping target with empty ProjectId.");
        failures++;
        continue;
    }

    var region = string.IsNullOrWhiteSpace(target.Region) ? "global" : target.Region!;
    var endpoint = region == "global" ? "dialogflow.googleapis.com:443" : $"{region}-dialogflow.googleapis.com:443";
    var agentName = region == "global"
        ? $"projects/{target.ProjectId}/agent"
        : $"projects/{target.ProjectId}/locations/{region}/agent";

    Console.WriteLine();
    Console.WriteLine($"=== {target.ProjectId} ({region}) ===");

    try
    {
        var jsonPath = !string.IsNullOrWhiteSpace(target.JsonPath) ? target.JsonPath : settings.JsonPath;
        var builder = new AgentsClientBuilder { Endpoint = endpoint };
        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            var credential = GoogleCredential.FromFile(jsonPath).CreateScoped(AgentsClient.DefaultScopes);
            builder.ChannelCredentials = credential.ToChannelCredentials();
        }
        var client = await builder.BuildAsync();

        if (string.Equals(settings.Mode, "Import", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Importing (merge)...");
            var op = await client.ImportAgentAsync(new ImportAgentRequest
            {
                Parent = agentName[..agentName.LastIndexOf("/agent", StringComparison.Ordinal)],
                AgentContent = content
            });
            await op.PollUntilCompletedAsync();
        }
        else
        {
            Console.WriteLine("Restoring (replace)...");
            var op = await client.RestoreAgentAsync(new RestoreAgentRequest
            {
                Parent = agentName[..agentName.LastIndexOf("/agent", StringComparison.Ordinal)],
                AgentContent = content
            });
            await op.PollUntilCompletedAsync();
        }
        Console.WriteLine("Upload complete.");

        if (settings.Train)
        {
            Console.WriteLine("Training...");
            var trainOp = await client.TrainAgentAsync(new TrainAgentRequest
            {
                Parent = agentName[..agentName.LastIndexOf("/agent", StringComparison.Ordinal)]
            });
            await trainOp.PollUntilCompletedAsync();
            Console.WriteLine("Training complete.");
        }
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAILED for {target.ProjectId}: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All targets processed successfully." : $"Done with {failures} failure(s).");
return failures == 0 ? 0 : 2;
