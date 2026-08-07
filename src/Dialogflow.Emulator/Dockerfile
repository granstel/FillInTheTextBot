FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["Dialogflow.Emulator/Dialogflow.Emulator.csproj", "Dialogflow.Emulator/"]
RUN dotnet restore "Dialogflow.Emulator/Dialogflow.Emulator.csproj"
COPY . .
RUN dotnet publish "Dialogflow.Emulator/Dialogflow.Emulator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Выгрузка агента монтируется снаружи, см. docker-compose
ENV AGENT_PATH=/app/agent
ENTRYPOINT ["dotnet", "Dialogflow.Emulator.dll"]
