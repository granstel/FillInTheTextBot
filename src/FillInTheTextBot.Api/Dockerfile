#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
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
ENTRYPOINT ["dotnet", "FillInTheTextBot.Api.dll"]