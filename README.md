# Shulker Tech

The web platform for the Shulker Tech Minecraft server — a community-driven survival/tech server.

## Features

- **Homepage** — Live server status cards (HTMX polling), world map iframe via BlueMap, and a customizable hero section
- **Wiki** — Markdown articles (rendered via Markdig) with per-article permissions, tag filtering, revision history, image uploads, and optional embedded BlueMap location panels
- **Article templates** — Admin-managed article templates with a default that auto-loads in the editor; users can switch templates from the Create page
- **Article ratings** — Minecraft-themed Usefulness (redstone) and Coolness (diamond) ratings; averages visible to all, picker shown to authenticated users
- **Favorites** — Users can star any article; favorites appear in a personal MY FAVORITES section on the wiki index
- **Random article** — One-click navigation to a random published article
- **Inline BlueMap embeds** — Fenced ` ```map ` blocks in article Markdown render as collapsible interactive map panels
- **Invite-only registration** — Accounts require a valid invite code and a real Minecraft username (verified via the Mojang API)
- **Two-factor authentication** — TOTP-based 2FA via ASP.NET Core Identity
- **Admin panel** — Manage users, roles, invite codes, map servers, site settings, wiki permissions, tags, and article templates
- **First-run setup** — Setup wizard creates the initial admin account; if database backups exist they can be restored directly from the setup page

## Tech Stack

- **Backend** — ASP.NET Core 10 Razor Pages, Entity Framework Core 10, PostgreSQL
- **Frontend** — Tailwind CSS v4, HTMX, EasyMDE (Markdown editor, vendored)
- **Auth** — ASP.NET Core Identity with lockout-based account deactivation
- **Maps** — BlueMap integration for live world map embeds
- **Markdown** — Markdig with custom extensions (external link renderer, BlueMap fenced block)

## Development

Requires Docker and Docker Compose.

```bash
# Start the dev environment (app + database, with hot reload)
make dev

# Run the test suite
make test

# Create a new database migration
make migration NAME=YourMigrationName

# Compile Tailwind CSS
make tailwind
```

Copy `.env.example` to `.env` and fill in the required values before running.

## Environment Variables

| Variable | Description |
|---|---|
| `SETUP_CODE` | Secret code required during first-run setup and backup restore |
| `BackupDir` | Path to the directory containing `.sql.gz` backup files (default: `/backups`) |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
