dev:
	docker compose down && docker compose -f docker-compose.yml -f docker-compose.dev.yml build --no-cache && docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
devl:
	docker compose down && docker compose -f docker-compose.yml -f docker-compose.dev.yml build --no-cache && docker compose -f docker-compose.yml -f docker-compose.dev.yml up
devq:
	docker compose down && docker compose -f docker-compose.yml -f docker-compose.dev.yml build && docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
prod:
	docker compose up
restart:
	docker compose -f docker-compose.yml -f docker-compose.dev.yml restart app
test:
	vendor/bin/phpunit
artisan:
	docker compose exec app php artisan $(cmd)
tinker:
	docker compose exec app php artisan tinker
