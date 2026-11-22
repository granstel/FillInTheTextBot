#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0 AS base
WORKDIR /app
# Начиная с .NET 8 образы по умолчанию слушают 8080. Порт зафиксирован явно,
# чтобы контракт контейнера не поменялся вместе с версией рантайма.
ENV ASPNETCORE_HTTP_PORTS=80
# После пустого пробуждения воркер раньше засыпает, пока другой занят.
ENV DOTNET_ThreadPool_SpuriousDispatchNoSpinThreshold=0
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS build
WORKDIR /src
# Central Package Management: без этого файла версии пакетов не заданы и restore падает
COPY ["Directory.Packages.props", "./"]
COPY ["FillInTheTextBot.Api/FillInTheTextBot.Api.csproj", "FillInTheTextBot.Api/"]
COPY ["FillInTheTextBot.Services/FillInTheTextBot.Services.csproj", "FillInTheTextBot.Services/"]
COPY ["FillInTheTextBot.Models/FillInTheTextBot.Models.csproj", "FillInTheTextBot.Models/"]
COPY ["FillInTheTextBot.Messengers/FillInTheTextBot.Messengers.csproj", "FillInTheTextBot.Messengers/"]
COPY ["FillInTheTextBot.Messengers.Yandex/FillInTheTextBot.Messengers.Yandex.csproj", "FillInTheTextBot.Messengers.Yandex/"]
RUN dotnet restore "FillInTheTextBot.Api/FillInTheTextBot.Api.csproj"
COPY . .
WORKDIR "/src/FillInTheTextBot.Api"
RUN dotnet build "FillInTheTextBot.Api.csproj" -c Release -o /app/build

FROM build AS publish
ARG RELEASE_VERSION
RUN dotnet publish "FillInTheTextBot.Api.csproj" -c Release -o /app/publish ${RELEASE_VERSION:+-p:Version="$RELEASE_VERSION"}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FillInTheTextBot.Api.dll"]
