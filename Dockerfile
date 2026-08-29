# Build stage. Restores and publishes the API, which also bundles the Blazor client.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy manifests first so restore is cached when only source changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN dotnet restore src/MusicPlayer.Api/MusicPlayer.Api.csproj

RUN dotnet publish src/MusicPlayer.Api/MusicPlayer.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# Runtime stage. Adds yt-dlp and ffmpeg, which the extraction layer shells out to.
FROM mcr.microsoft.com/dotnet/aspnet:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip ffmpeg ca-certificates curl \
    && pip3 install --no-cache-dir --break-system-packages yt-dlp \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Runs unprivileged as the "app" user the .NET image already ships.
# The data directory is chowned so the app can write its database and cache.
WORKDIR /app
COPY --from=build /app ./
RUN mkdir -p /data && chown -R app:app /data /app
USER app

# Both live on the mounted volume so playlists and cached audio survive a rebuild.
ENV ConnectionStrings__Library="Data Source=/data/library.db" \
    MediaCache__Directory="/data/media" \
    ASPNETCORE_ENVIRONMENT="Production" \
    ASPNETCORE_HTTP_PORTS="5080"

VOLUME ["/data"]
EXPOSE 5080

HEALTHCHECK --interval=60s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:5080/health || exit 1

ENTRYPOINT ["./MusicPlayer.Api"]
