.PHONY: dev deploy setup migration clean

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

# ── Production ────────────────────────────────────────────────────────────────

# Deploy production stack — explicitly excludes the dev override
deploy:
	docker compose -f docker-compose.yml up -d --build
