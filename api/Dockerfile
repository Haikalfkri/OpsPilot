# syntax=docker/dockerfile:1

# ============ Tahap 1: membangun (SDK besar, tidak ikut ke image akhir) ============
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

# Mencari file proyek API secara otomatis, supaya tidak bergantung pada nama folder.
# Cache NuGet disimpan di antara build (--mount), jadi build ulang jauh lebih cepat.
RUN --mount=type=cache,target=/root/.nuget/packages \
    set -e; \
    API_PROJECT="$(find . -name 'OpsPilot.Api.csproj' -not -path '*/obj/*' | head -n 1)"; \
    if [ -z "$API_PROJECT" ]; then \
        echo "OpsPilot.Api.csproj was not found. Projects present:"; \
        find . -name '*.csproj' -not -path '*/obj/*'; \
        exit 1; \
    fi; \
    dotnet publish "$API_PROJECT" -c Release -o /app/publish /p:UseAppHost=false


# ============ Tahap 2: menjalankan (hanya runtime, jauh lebih kecil) ============
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl dibutuhkan oleh healthcheck di docker-compose.yml
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# Jalan sebagai pengguna biasa (bukan root). APP_UID sudah disediakan image .NET 8.
USER $APP_UID

ENTRYPOINT ["dotnet", "OpsPilot.Api.dll"]