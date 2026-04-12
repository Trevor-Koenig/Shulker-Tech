.PHONY: dev deploy setup migration clean test test-unit test-integration test-coverage

# ── First-time setup ──────────────────────────────────────────────────────────

# Install dev tools required on the local machine
setup:
	dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
	dotnet tool install --global dotnet-aspnet-codegenerator || dotnet tool update --global dotnet-aspnet-codegenerator
	@test -f tailwindcss || (curl -sLo tailwindcss https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-x64 && chmod +x tailwindcss)

# Create a new EF Core migration (usage: make migration NAME=AddArticleTable)
migration:
	cd ShulkerTech.Web && dotnet ef migrations add $(NAME)

# ── Development ───────────────────────────────────────────────────────────────

# Start dev environment in Docker (db + app with hot reload + Tailwind watch)
# docker-compose.override.yml is merged automatically by Docker Compose
dev:
	docker compose up --build

# Same but detached
dev-bg:
	docker compose up --build -d

# Stop all dev containers
stop:
	docker compose down

# ── Testing ───────────────────────────────────────────────────────────────────

# Run all tests (requires Docker for Testcontainers integration tests)
test:
	dotnet test tests/ShulkerTech.Tests/ShulkerTech.Tests.csproj

# Run only unit tests — no Docker required
test-unit:
	dotnet test tests/ShulkerTech.Tests/ShulkerTech.Tests.csproj --filter "Category=Unit"

# Run only integration tests — requires Docker
test-integration:
	dotnet test tests/ShulkerTech.Tests/ShulkerTech.Tests.csproj --filter "Category=Integration"

# Run tests with coverage output in ./coverage/
test-coverage:
	dotnet test tests/ShulkerTech.Tests/ShulkerTech.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./coverage

# ── Production ────────────────────────────────────────────────────────────────

# Deploy production stack — explicitly excludes the dev override
deploy:
	docker compose -f docker-compose.yml up -d --build
