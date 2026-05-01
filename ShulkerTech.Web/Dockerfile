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
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Install pg_dump for database backups
# Uses lsb_release to detect the OS codename so the right PGDG repo is used
# (dotnet/aspnet:10.0 is Ubuntu-based, not Debian bookworm)
RUN apt-get update \
    && apt-get install -y --no-install-recommends gnupg curl lsb-release \
    && curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
       | gpg --dearmor -o /usr/share/keyrings/pgdg.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/pgdg.gpg] https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" \
       > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client-18 postgresql-client-common \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShulkerTech.Web.dll"]
