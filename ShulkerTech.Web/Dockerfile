FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore — separate layer so dependencies are cached
COPY ["ShulkerTech.Web/ShulkerTech.Web.csproj", "ShulkerTech.Web/"]
RUN dotnet restore "ShulkerTech.Web/ShulkerTech.Web.csproj"

# Copy everything else and publish
COPY . .
RUN dotnet publish "ShulkerTech.Web/ShulkerTech.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install pg_dump for database backups
RUN apt-get update \
    && apt-get install -y --no-install-recommends gnupg curl \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
       | gpg --dearmor -o /usr/share/keyrings/pgdg.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/pgdg.gpg] https://apt.postgresql.org/pub/repos/apt bookworm-pgdg main" \
       > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client-18 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShulkerTech.Web.dll"]
