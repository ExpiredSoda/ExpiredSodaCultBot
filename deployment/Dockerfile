# Use the official .NET runtime as base image
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["../CultBot/CultBot.csproj", "CultBot/"]
RUN dotnet restore "CultBot/CultBot.csproj"
COPY ../ .
WORKDIR "/src/CultBot"
RUN dotnet build "CultBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CultBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - copy the published app
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CultBot.dll"]
