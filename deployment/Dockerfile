# Build & runtime for .NET 8 (GA)

# ---- Base runtime image ----
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# ---- Build image ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore deps
COPY CultBot/CultBot.csproj CultBot/
RUN dotnet restore "CultBot/CultBot.csproj"

# Copy the rest of the source
COPY . .

# Build
WORKDIR "/src/CultBot"
RUN dotnet publish "CultBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- Final image ----
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CultBot.dll"]
