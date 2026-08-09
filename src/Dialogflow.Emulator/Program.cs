using Dialogflow.Emulator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSingleton<IAgentStorage, AgentStorage>();
builder.Services.AddSingleton<SlotFillingStore>();

var app = builder.Build();

var agentPath = builder.Configuration.GetValue<string>("AGENT_PATH")
                ?? Path.Combine(Directory.GetCurrentDirectory(), "agent");

var agentStorage = app.Services.GetRequiredService<IAgentStorage>();
await agentStorage.InitializeAsync(agentPath);

app.MapGrpcService<SessionsEmulatorService>();
app.MapGrpcService<ContextsEmulatorService>();

app.MapGet("/", () => "Dialogflow emulator. Use a gRPC client.");

app.Run();
