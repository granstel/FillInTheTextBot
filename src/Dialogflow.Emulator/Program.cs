using Dialogflow.Emulator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<IAgentStorage, AgentStorage>();
builder.Services.AddScoped<IIntentMatcher, IntentMatcher>();

var app = builder.Build();

// Initialize agent storage
var agentStorage = app.Services.GetRequiredService<IAgentStorage>();
var agentPath = builder.Configuration.GetValue<string>("AGENT_PATH") ?? Path.Combine(Directory.GetCurrentDirectory(), "agent");
await agentStorage.InitializeAsync(agentPath);

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<DialogflowEmulatorService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
