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
- **Admin panel** — Manage users, roles, invite codes, map servers, site settings, wiki permissions, tags, and article templates; permission-gated cards are grayed out for roles that lack access; includes a one-click database export (gzip-compressed SQL dump)
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

## Software Practices

### Architecture & Code Quality
- **Separation of concerns** — Domain models and data access live in `ShulkerTech.Core`; all web concerns (pages, middleware, services) stay in `ShulkerTech.Web`. Neither project bleeds into the other.
- **Object-oriented design** — Business logic is encapsulated in focused classes (middleware, scoped services, page models). Page models are thin coordinators; heavy logic belongs in services.
- **Minimal, purposeful code** — No speculative abstractions, no half-finished features, no backwards-compatibility shims. Three similar lines are preferred over a premature helper.
- **No comments on obvious code** — Comments are reserved for non-obvious constraints, subtle invariants, or deliberate workarounds. Well-named identifiers document the *what*; comments explain the *why*.

### Testing
- **Integration tests over mocks** — The test suite runs against a real PostgreSQL instance via Testcontainers. Database mocks are avoided because mock/real divergence has historically masked broken migrations.
- **Full coverage of critical paths** — Every feature ships with integration tests covering the happy path, edge cases, and access-control boundaries (authenticated, unauthenticated, wrong role).
- **Tests clean up after themselves** — Shared fixture state is always restored in `finally` blocks so test ordering never affects results.
- **CI parity** — `make test` runs the full suite locally in the same Docker environment used in production; no "works on my machine" gaps.

### Security (OWASP Top 10 and beyond)
- **SQL injection (A03)** — All database access goes through EF Core with parameterised queries. Raw SQL is never used.
- **Broken access control (A01)** — Every page and admin route is guarded by `PageGuardMiddleware` and `AdminGuardMiddleware` using a fully dynamic RBAC system. There are no hardcoded role bypasses anywhere in the codebase.
- **XSS (A03/A07)** — Razor auto-encodes all output. The Markdown pipeline runs with `DisableHtml()` so raw HTML in article content is stripped before rendering.
- **CSRF (A01)** — Antiforgery tokens are validated on every state-changing POST. The test suite replaces antiforgery with a no-op only in the test environment.
- **Broken authentication (A07)** — ASP.NET Core Identity handles password hashing and account lockout. TOTP-based 2FA can be enforced per role. Invite codes gate registration.
- **Rate limiting (A04/A07)** — Login, registration, and password-reset endpoints are rate-limited by IP using ASP.NET Core's built-in rate limiter. The upload API has its own named policy.
- **Security headers** — Every response includes `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, and `Referrer-Policy: strict-origin-when-cross-origin` via `SecurityHeadersMiddleware`.
- **Path traversal** — Backup restore validates filenames with `Path.GetFileName` and a `Path.GetFullPath` prefix check before touching the filesystem.
- **Secrets management** — All sensitive values (`SETUP_CODE`, database credentials, email config) are supplied via environment variables and never committed to source control.



### Completed

- **Markdown pipeline fixes** — External links open in new tab, GitHub-style heading anchors, uploaded images served correctly in production
- **Inline BlueMap embeds** — Fenced ` ```map ` blocks render as collapsible interactive map panels; multiple independent embeds per article
- **Tag system** — Many-to-many tags replace the old category field; tag pills on index with client-side filtering, full admin CRUD with live preview
- **Favorites** — Per-user article favouriting with a personal MY FAVORITES section on the wiki index
- **Random article** — One-click navigation to a random published article
- **Article ratings** — Usefulness (redstone) and Coolness (diamond) Minecraft-themed ratings; averages visible to all, picker for authenticated users
- **Article templates** — Admin-managed DB-driven templates; default auto-loads in the editor, users can switch from the create page
- **Revision history** — Full edit history per article with side-by-side diff viewer and one-click restore
- **Backup restore on setup** — Existing `.sql.gz` backups can be restored directly from the first-run setup page; migrations auto-apply after restore and the first user is guaranteed an Admin role
- **Automated database backups** — Daily scheduled backups compressed with gzip, 14-day retention, final backup on shutdown
- **RBAC permission system** — Fully dynamic role-based access control covering every page (home, wiki, admin); permissions managed through the admin panel with no hardcoded bypasses
- **Per-role 2FA enforcement** — Admin-configurable list of roles that must complete TOTP setup before accessing the site
- **Guest role** — Any role can be designated as the "guest" role; unauthenticated visitors are treated as holding that role for all permission checks
- **Database export** — Admin page (`/Admin/Site/DbExport`) streams a gzip-compressed `pg_dump` to the browser; protected by the `admin.db_export` RBAC resource; error handling redirects back to the page on failure
- **Permission-aware admin dashboard** — Each card on the admin dashboard checks the current user's RBAC grants; inaccessible cards are grayed out with `pointer-events: none` and no `href`, so they are entirely non-interactive without changing the element type
- **Incremental permission seeding** — On each startup the app compares `SiteResource.All` against the Admin role's existing grants and inserts only the missing entries, so newly-added resources are granted automatically on the next deploy without touching existing permissions

### Planned

#### Wiki
- **Article search** — Full-text search using PostgreSQL `tsvector` on article title and content; search bar on the wiki index
- **Table of contents** — Auto-generated TOC sidebar for long articles using Markdig's built-in extension
- **Article comments** — Per-article discussion thread so users can ask questions and give feedback
- **Last edited by** — Surface the most recent editor and timestamp on the article view page
- **Meilisearch** — Drop-in search upgrade once PostgreSQL FTS becomes insufficient

#### Admin
- **Audit log** — Persistent record of permission changes, article deletions, role assignments, and other admin actions
- **Site announcements** — Admin-posted banners or notices displayed site-wide; useful for downtime, events, and rule changes
- **Invite code usage tracking** — Show which user redeemed each invite code and when

#### Minecraft & Community
- **Player profile pages** — Public `/players/{username}` pages showing a user's articles, playtime from session data, and Minecraft skin via the Mojang API
- **Discord OAuth** — Sign in with Discord alongside local Identity accounts (natural fit for Minecraft communities)
- **Live server status push** — `ServerStatusHub` and SignalR are wired up; remaining work is the server-side push logic and replacing the HTMX polling on the homepage with a SignalR client connection

#### Long-term
- **Collaborative editing** — Real-time co-editing via Yjs + SignalR transport (requires editor upgrade from EasyMDE)
