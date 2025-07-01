# FillInTheTextBot Information

## Summary
FillInTheTextBot is a .NET-based conversational bot platform that integrates with multiple voice assistant platforms including Yandex, Sber, and Marusia. The application uses Dialogflow for natural language processing and provides a unified API for handling conversations across different messenger platforms.

## Structure
- **FillInTheTextBot.Api**: Main API entry point and web application host
- **FillInTheTextBot.Models**: Shared data models used across the application
- **FillInTheTextBot.Services**: Core business logic and services
- **FillInTheTextBot.Messengers**: Base messenger integration framework
- **FillInTheTextBot.Messengers.***: Platform-specific implementations (Yandex, Sber, Marusia)
- **Tests**: Multiple test projects for different components

## Language & Runtime
**Language**: C#
**Framework**: ASP.NET Core
**Version**: .NET 6.0
**Build System**: MSBuild (Visual Studio)
**Package Manager**: NuGet

## Dependencies
**Main Dependencies**:
- Google.Cloud.Dialogflow.V2: Natural language processing integration
- NLog/NLog.Web.AspNetCore: Logging framework
- Newtonsoft.Json: JSON serialization/deserialization
- OpenTracing/Jaeger: Distributed tracing
- prometheus-net: Metrics collection and monitoring

**Development Dependencies**:
- NUnit: Testing framework
- Moq: Mocking library for unit tests
- AutoFixture: Test data generation

## Build & Installation
```bash
dotnet restore
dotnet build
dotnet run --project FillInTheTextBot.Api/FillInTheTextBot.Api.csproj
```

## Docker
**Dockerfile**: FillInTheTextBot.Api/Dockerfile
**Base Image**: mcr.microsoft.com/dotnet/aspnet:6.0
**Build Image**: mcr.microsoft.com/dotnet/sdk:6.0
**Exposed Port**: 80
**Build Command**:
```bash
docker build -t fillinthetext-bot -f FillInTheTextBot.Api/Dockerfile .
```

## Application Structure
**Entry Point**: Program.cs in FillInTheTextBot.Api
**Configuration**: Startup.cs handles service registration and middleware configuration
**Main Components**:
- **DialogflowService**: Handles NLP processing through Google's Dialogflow
- **ConversationService**: Manages conversation state and flow
- **Messenger Services**: Platform-specific implementations for different voice assistants
  - YandexService: Integration with Yandex Alice
  - SberService: Integration with Sber Salut
  - MarusiaService: Integration with Marusia

## Testing
**Framework**: NUnit
**Test Locations**:
- FillInTheTextBot.Services.Tests
- FillInTheTextBot.Messengers.Tests
- FillInTheTextBot.Messengers.Yandex.Tests
**Tools**: Moq for mocking, AutoFixture for test data generation
**Run Command**:
```bash
dotnet test
```