#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# curl нужен только для HEALTHCHECK: по его состоянию скрипт раскатки понимает,
# что новый экземпляр готов принимать трафик
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
# Начиная с .NET 8 образы по умолчанию слушают 8080. Порт зафиксирован явно,
# чтобы контракт контейнера не поменялся вместе с версией рантайма.
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Версии пакетов управляются централизованно, без этого файла restore не проходит
COPY ["Directory.Packages.props", "./"]
COPY ["FillInTheTextBot.Api/FillInTheTextBot.Api.csproj", "FillInTheTextBot.Api/"]
COPY ["FillInTheTextBot.Services/FillInTheTextBot.Services.csproj", "FillInTheTextBot.Services/"]
COPY ["FillInTheTextBot.Models/FillInTheTextBot.Models.csproj", "FillInTheTextBot.Models/"]
COPY ["FillInTheTextBot.Messengers.Sber/FillInTheTextBot.Messengers.Sber.csproj", "FillInTheTextBot.Messengers.Sber/"]
COPY ["FillInTheTextBot.Messengers/FillInTheTextBot.Messengers.csproj", "FillInTheTextBot.Messengers/"]
COPY ["FillInTheTextBot.Messengers.Yandex/FillInTheTextBot.Messengers.Yandex.csproj", "FillInTheTextBot.Messengers.Yandex/"]
COPY ["FillInTheTextBot.Messengers.Marusia/FillInTheTextBot.Messengers.Marusia.csproj", "FillInTheTextBot.Messengers.Marusia/"]
RUN dotnet restore "FillInTheTextBot.Api/FillInTheTextBot.Api.csproj"
COPY . .
WORKDIR "/src/FillInTheTextBot.Api"
RUN dotnet build "FillInTheTextBot.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FillInTheTextBot.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=3 CMD curl -fsS http://localhost:80/health || exit 1
ENTRYPOINT ["dotnet", "FillInTheTextBot.Api.dll"]
