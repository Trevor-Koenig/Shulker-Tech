# Shulker Tech

The web platform for the Shulker Tech Minecraft server — a community-driven survival/tech server.

## Features

- **Homepage** — Live server status cards (HTMX polling), world map iframe via BlueMap, and a customizable hero section
- **Wiki** — Markdown articles (rendered via Markdig) with per-article permissions, category grouping, TOC sidebar, image uploads, and optional embedded BlueMap location panels
- **Invite-only registration** — Accounts require a valid invite code and a real Minecraft username (verified via the Mojang API)
- **Two-factor authentication** — TOTP-based 2FA via ASP.NET Core Identity
- **Admin panel** — Manage users, roles, invite codes, map servers, site settings, and wiki permissions

## Tech Stack

- **Backend** — ASP.NET Core 10 Razor Pages, Entity Framework Core 10, PostgreSQL
- **Frontend** — Tailwind CSS v4, HTMX, EasyMDE (markdown editor)
- **Auth** — ASP.NET Core Identity with lockout-based account deactivation
- **Maps** — BlueMap integration for live world map embeds

## Development

Requires Docker and Docker Compose.

```bash
# Start the dev environment (app + database, with hot reload)
make dev

# Create a new database migration
make migration NAME=YourMigrationName

# Compile Tailwind CSS
make tailwind
```

Copy `.env.example` to `.env` and fill in the required values before running.
