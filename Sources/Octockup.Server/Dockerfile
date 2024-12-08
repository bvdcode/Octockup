FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS with-node
RUN apt-get update
RUN curl -sL https://deb.nodesource.com/setup_20.x | bash
RUN apt-get -y install nodejs curl git-lfs
RUN git lfs install

FROM with-node AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN git lfs pull
COPY ["Octockup.Server/Octockup.Server.csproj", "Octockup.Server/"]
COPY ["octockup.client/octockup.client.esproj", "octockup.client/"]
RUN dotnet restore "./Octockup.Server/Octockup.Server.csproj"
COPY . .
WORKDIR "/src/Octockup.Server"
RUN dotnet build "./Octockup.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Octockup.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Octockup.Server.dll"]
