.PHONY: dev css css-watch db migrate

# Start PostgreSQL via Docker (dev only)
db:
	docker compose up -d db

# Run the app
dev:
	cd ShulkerTech.Web && dotnet run

# Compile Tailwind once
css:
	./tailwindcss -i ShulkerTech.Web/wwwroot/css/app.css -o ShulkerTech.Web/wwwroot/css/app.out.css --minify

# Watch and recompile Tailwind on save
css-watch:
	./tailwindcss -i ShulkerTech.Web/wwwroot/css/app.css -o ShulkerTech.Web/wwwroot/css/app.out.css --watch

# Create and apply EF Core migrations
migrate:
	cd ShulkerTech.Web && dotnet ef database update
