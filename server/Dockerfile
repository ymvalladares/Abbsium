# Stage 1: Runtime base
FROM mcr.microsoft.com/dotnet/aspnet:6.0-jammy AS base
WORKDIR /app
EXPOSE 80

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS build
WORKDIR /src

COPY . .
RUN dotnet restore Server.csproj
RUN dotnet publish Server.csproj -c Release -o /app/publish

# Stage 3: Final
FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

# Install ffmpeg, yt-dlp and deno
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ffmpeg \
      ca-certificates \
      curl \
      unzip \
 && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp \
      -o /usr/local/bin/yt-dlp \
 && chmod +x /usr/local/bin/yt-dlp \
 && curl -fsSL https://deno.land/install.sh \
      | DENO_INSTALL=/usr/local sh \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "Server.dll"]