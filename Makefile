.PHONY: dev css css-watch db deploy setup migration

# ── First-time setup ──────────────────────────────────────────────────────────

# Install dev tools required on the local machine (not needed in Docker)
setup:
	dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
	@echo "Tailwind: download tailwindcss binary to repo root if not present"
	@test -f tailwindcss || (curl -sLo tailwindcss https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-linux-x64 && chmod +x tailwindcss)

# Create a new EF Core migration (usage: make migration NAME=AddArticleTable)
migration:
	cd ShulkerTech.Web && dotnet ef migrations add $(NAME)

# ── Development ──────────────────────────────────────────────────────────────

# Start PostgreSQL only (app runs natively via dotnet)
db:
	docker compose up -d db

# Run the app with hot-reload
dev:
	cd ShulkerTech.Web && dotnet watch

# Compile Tailwind once
css:
	./tailwindcss -i ShulkerTech.Web/wwwroot/css/app.css -o ShulkerTech.Web/wwwroot/css/app.out.css --minify

# Watch and recompile Tailwind on save
css-watch:
	./tailwindcss -i ShulkerTech.Web/wwwroot/css/app.css -o ShulkerTech.Web/wwwroot/css/app.out.css --watch

# ── Production ────────────────────────────────────────────────────────────────

# Build CSS then bring up the full stack (db + app) in Docker
deploy:
	$(MAKE) css
	docker compose up -d --build
