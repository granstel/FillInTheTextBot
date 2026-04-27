FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы управления пакетами
COPY ["src/Directory.Packages.props", "src/"]
COPY ["nuget.config", "./"]

# Копируем файлы проектов
COPY ["src/Dialogflow.Emulator/Dialogflow.Emulator.csproj", "src/Dialogflow.Emulator/"]

# Восстанавливаем зависимости
RUN dotnet restore "src/Dialogflow.Emulator/Dialogflow.Emulator.csproj"

# Копируем весь исходный код
COPY ["src/Dialogflow.Emulator/", "src/Dialogflow.Emulator/"]

# Собираем проект
WORKDIR "/src/src/Dialogflow.Emulator"
RUN dotnet build "Dialogflow.Emulator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Dialogflow.Emulator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dialogflow.Emulator.dll"]
