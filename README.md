# OpsPilot

OpsPilot is a web-based incident management platform for small engineering and IT teams. It gives a team one place to
record incidents, assign ownership, coordinate investigation, track response and resolution targets (SLA), and
document what was learned.

It is a portfolio project: a **modular monolith** (one ASP.NET Core API, one React frontend, PostgreSQL, Redis) built to
demonstrate sound engineering practice, not to be a production SaaS.

## Features

- **Incidents:** create, update, assign, and move through `Open → Investigating → Resolved → Closed` (illegal
  transitions are rejected). Paginated list with filters, search, and sorting.
- **Timeline and comments:** every important change is recorded in an append-only activity log; comments are immutable.
- **SLA tracking:** response and resolution targets per severity. A background worker records each breach exactly once,
  writes a system activity, and notifies the assignee (or the team admins when nobody is assigned).
- **In-app notifications:** on assignment and on SLA breach, with an unread counter.
- **Dashboard:** open / critical / breached counts, resolved count and average resolution time, breakdowns, and a daily
  trend, cached in Redis.
- **Postmortems:** one per resolved or closed incident (summary, root cause, impact, corrective actions).
- **Users and roles:** Admin, Responder, Viewer, all enforced by the API (not just hidden in the UI).

## Quick start (Docker)

Requirements: Docker Desktop (with Compose v2).

```bash
cp .env.example .env          # Windows: copy .env.example .env
# Edit .env and set JWT_SIGNING_KEY (see the comments in the file for how to generate one)
docker compose up --build
```

| What                        | Where                                                       |
| --------------------------- | ----------------------------------------------------------- |
| Web app                     | http://localhost:5173                                       |
| API                         | http://localhost:5130                                       |
| API documentation (Swagger) | http://localhost:5130/swagger (when `SWAGGER_ENABLED=true`) |
| Health check                | http://localhost:5130/health                                |

The first build takes a few minutes. Data is kept in a Docker volume, so it survives `docker compose down`
(use `docker compose down -v` to wipe it).

### Demo accounts (local only)

With `SEED_DEMO_DATA=true`, these accounts exist. The password for all of them is `DemoPassword123!`.

| Email                                                      | Role                                      |
| ---------------------------------------------------------- | ----------------------------------------- |
| admin@opspilot.local                                       | Admin                                     |
| responder@opspilot.local                                   | Responder                                 |
| viewer@opspilot.local                                      | Viewer                                    |
| inactive@opspilot.local                                    | Responder (disabled, cannot sign in)      |
| other.admin@opspilot.local, other.responder@opspilot.local | Second team (used to test team isolation) |

These credentials are public. **Never enable `SEED_DEMO_DATA` on a server others can reach.**

### Check that everything works

```powershell
.\scripts\smoke-test.ps1
```

## Local development

Run only the dependencies in Docker, and the apps from your tools:

```bash
docker compose up -d postgres redis
```

**API** (.NET 8):

```bash
cd api/Api
dotnet user-secrets set "Jwt:SigningKey" "<any random string of 32+ characters>"
dotnet run
```

In Development the API applies migrations and seeds the demo accounts automatically. The connection strings in
`appsettings.Development.json` must match the ports in `.env` (`POSTGRES_HOST_PORT`, `REDIS_HOST_PORT`).

**Frontend** (Node 20.19+ or 22.12+):

```bash
cd frontend
cp .env.example .env          # set VITE_API_URL to the API address, e.g. http://localhost:5130
npm install
npm run dev                   # http://localhost:5173
```

## Tests

```bash
# Backend. Integration tests start their own PostgreSQL container, so Docker must be running.
cd api && dotnet test

# Frontend
cd frontend && npm test
```

Backend: domain rules (status transitions, SLA evaluation), application services (authorization, team scoping),
the SLA worker (idempotency), and API integration tests against a real PostgreSQL.
Frontend: guards, forms, filters, and pages against a faked API.

## Architecture

```
React + TypeScript (Vite)  ──HTTP + JWT──►  ASP.NET Core API  ──►  PostgreSQL (source of truth)
                                                  │
                                                  ├──►  Redis (dashboard cache only)
                                                  └──►  BackgroundService (SLA breach detection)
```

The backend has four projects with the dependency rule pointing inwards:

- **Domain:** entities and business rules. No framework dependencies.
- **Application:** use cases, DTOs, validation, authorization checks, and the interfaces they need.
- **Infrastructure:** EF Core, Identity, JWT, Redis, the SLA worker, and seed data.
- **Api:** thin controllers, authentication, error handling, and configuration.

### Conventions

| Topic         | Decision                                                                                                                                                                                                               |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Errors        | RFC 7807 Problem Details. `400` validation (per-field errors), `401` unauthenticated, `403` role not allowed, `404` not found **or** belongs to another team, `409` state conflict (illegal status change, last admin) |
| Authorization | Role _and_ team scope, enforced in the API. The team always comes from the token, never from the request                                                                                                               |
| Time          | Stored and calculated in UTC. Dashboard days are UTC calendar days; the UI shows times in the browser's zone                                                                                                           |
| Pagination    | Mandatory on every list (default 20, maximum 100)                                                                                                                                                                      |
| SLA targets   | Configurable examples in `appsettings` (section `Sla`), **not** an industry standard                                                                                                                                   |

### Dashboard cache

|                  |                                                                                             |
| ---------------- | ------------------------------------------------------------------------------------------- |
| What is cached   | The three dashboard aggregates, per team and period                                         |
| Key              | `opspilot:dashboard:{teamId}:{kind}:{days}` (no tokens or personal data)                    |
| TTL              | 60 seconds                                                                                  |
| Invalidated when | An incident is created, its status or severity changes, or the worker records an SLA breach |
| If Redis is down | The API reads PostgreSQL directly and logs a warning. No data is lost                       |

## Configuration

Environment variables used by the API container (set through `.env` and `docker-compose.yml`):

| Variable                                               | Purpose                                                          |
| ------------------------------------------------------ | ---------------------------------------------------------------- |
| `JWT_SIGNING_KEY`                                      | **Required.** Secret used to sign tokens (32+ characters)        |
| `POSTGRES_*`                                           | Database name, user, and password                                |
| `SEED_DEMO_DATA`                                       | Create the demo accounts (local demos only)                      |
| `SWAGGER_ENABLED`                                      | Serve Swagger UI                                                 |
| `RateLimiting__Login__PermitLimit` / `__WindowSeconds` | Sign-in attempts allowed per IP per window (default 10 per 60 s) |
| `SlaWorker__IntervalSeconds`                           | How often the SLA worker checks (default 60)                     |
| `Sla__Critical__ResponseMinutes`, ...                  | SLA targets per severity                                         |

## Security notes

- Passwords are hashed by ASP.NET Core Identity. Accounts lock for 15 minutes after 5 failed sign-ins, and a sign-in
  failure never reveals whether an email exists.
- Sign-in is also rate limited per IP. _Behind Docker Desktop every request appears to come from one gateway address,
  so locally the limit is shared by everyone._ A real deployment behind a reverse proxy must configure forwarded headers.
- A deactivated user, or one whose role or team changed, is rejected on the very next request (the token is re-checked
  against the database each time).
- API responses carry `X-Content-Type-Options`, `Referrer-Policy`, and `Cache-Control: no-store`. The web server sends a
  Content-Security-Policy and related headers. Containers run as non-root, and database and cache ports are bound to
  `127.0.0.1`.
- Secrets live in environment variables or user secrets. Nothing secret is committed.

## Known limitations

These are deliberate MVP decisions, not oversights:

- **Token storage:** the JWT is kept in `sessionStorage`. That survives a refresh but not closing the tab, and it can be
  read by script if the page ever has an XSS hole (the app avoids raw HTML insertion and a CSP limits script sources).
  There are no refresh tokens: sessions last 60 minutes. A stronger design (HttpOnly cookie plus refresh tokens) needs
  backend changes.
- **No HTTPS** in the Docker setup; it is meant for local use. Put a TLS-terminating proxy in front for anything else.
- **Notifications are polled** every 30 seconds; there is no real-time push.
- **No reopening** of resolved incidents, and nothing can be deleted.
- **SLA breaches are recorded only while an incident is open.** If the worker is down while an incident is resolved late,
  the breach is not recorded afterwards (the UI still shows it as breached, computed from timestamps).
- **A notification can go to an assignee who was deactivated** after being assigned.
- **Concurrent edits:** the last save wins; there is no conflict detection.
- **Redis is tested with a fake in automated tests**, and with the real one only manually.
- Email, SMS, push, billing, multi-tenant provisioning, and external alert ingestion are out of scope.

## Project layout

```
api/                 ASP.NET Core solution (Domain, Application, Infrastructure, Api, tests)
frontend/            React + TypeScript app
docs/                Requirements and design documents
scripts/             Smoke test
docker-compose.yml   Whole stack
```
