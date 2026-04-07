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
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShulkerTech.Web.dll"]
