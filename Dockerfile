# Use official .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["CultBot/CultBot.csproj", "CultBot/"]
RUN dotnet restore "CultBot/CultBot.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/CultBot"
RUN dotnet publish "CultBot.csproj" -c Release -o /app/publish

# Use official .NET 8 runtime for running
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "CultBot.dll"]
