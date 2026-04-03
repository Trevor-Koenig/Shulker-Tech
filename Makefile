dev:
	docker compose down && docker compose -f docker-compose.yml -f docker-compose.dev.yml build --no-cache && docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
prod:
	docker compose up

test:
	vendor/bin/phpunit
