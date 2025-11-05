using Google.Api.Gax.Grpc;
using Google.Cloud.Dialogflow.V2;
using Grpc.Core;
using Environment = System.Environment;

// Minimal gRPC client for Dialogflow Emulator
// It sends two requests: WELCOME event and a simple text query.

var endpoint = Environment.GetEnvironmentVariable("EMULATOR_ENDPOINT") ?? "http://localhost:7195";
Console.WriteLine($"Using endpoint: {endpoint}");

// Enable HTTP/2 over plaintext for local emulator
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var sessionsClient = new SessionsClientBuilder
{
    Endpoint = endpoint,
    ChannelCredentials = ChannelCredentials.Insecure,
    GrpcAdapter = GrpcNetClientAdapter.Default
        .WithAdditionalOptions(o => o.HttpHandler = new SocketsHttpHandler { UseProxy = false })
}.Build();

var sessionId = Guid.NewGuid().ToString("N");
var session = new SessionName("test-project", sessionId);

// 1) WELCOME event
var welcomeRequest = new DetectIntentRequest
{
    SessionAsSessionName = session,
    QueryInput = new QueryInput
    {
        Event = new EventInput
        {
            Name = "WELCOME",
            LanguageCode = "ru"
        }
    }
};

Console.WriteLine("Sending WELCOME event...");
var welcomeResponse = await sessionsClient.DetectIntentAsync(welcomeRequest);
Console.WriteLine($"Intent: {welcomeResponse.QueryResult.Intent.DisplayName}");
Console.WriteLine($"Fulfillment: {welcomeResponse.QueryResult.FulfillmentText}");
Console.WriteLine($"Lang: {welcomeResponse.QueryResult.LanguageCode}");
Console.WriteLine();

// 2) Text query
var text = "да";
var textRequest = new DetectIntentRequest
{
    SessionAsSessionName = session,
    QueryInput = new QueryInput
    {
        Text = new TextInput
        {
            Text = text,
            LanguageCode = "ru"
        }
    }
};

Console.WriteLine($"Sending text: '{text}'...");
var textResponse = await sessionsClient.DetectIntentAsync(textRequest);
Console.WriteLine($"QueryText: {textResponse.QueryResult.QueryText}");
Console.WriteLine($"Intent: {textResponse.QueryResult.Intent.DisplayName}");
Console.WriteLine($"Fulfillment: {textResponse.QueryResult.FulfillmentText}");

// Gracefully shutdown default channels (prevents locked processes on Windows)
await SessionsClient.ShutdownDefaultChannelsAsync();

Console.WriteLine();
Console.WriteLine("Done.");
