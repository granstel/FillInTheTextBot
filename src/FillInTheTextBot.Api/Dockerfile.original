#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["FillInTheTextBot.Api/FillInTheTextBot.Api.csproj", "FillInTheTextBot.Api/"]
COPY ["FillInTheTextBot.Services/FillInTheTextBot.Services.csproj", "FillInTheTextBot.Services/"]
COPY ["FillInTheTextBot.Models/FillInTheTextBot.Models.csproj", "FillInTheTextBot.Models/"]
COPY ["FillInTheTextBot.Messengers.Telegram/FillInTheTextBot.Messengers.Telegram.csproj", "FillInTheTextBot.Messengers.Telegram/"]
COPY ["FillInTheTextBot.Messengers/FillInTheTextBot.Messengers.csproj", "FillInTheTextBot.Messengers/"]
COPY ["FillInTheTextBot.Messengers.Yandex/FillInTheTextBot.Messengers.Yandex.csproj", "FillInTheTextBot.Messengers.Yandex/"]
RUN dotnet restore "FillInTheTextBot.Api/FillInTheTextBot.Api.csproj"
COPY . .
WORKDIR "/src/FillInTheTextBot.Api"
RUN dotnet build "FillInTheTextBot.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FillInTheTextBot.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FillInTheTextBot.Api.dll"]