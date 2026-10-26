# OpsPilot — Project Overview

## 1. Project summary

**OpsPilot** is a web-based operations and incident management platform for small-to-medium engineering or IT teams. It gives teams one place to record incidents, assign ownership, coordinate investigation, track service-level targets, and document resolution.

The portfolio implementation is a **modular monolith**: one ASP.NET Core Web API, one React + TypeScript frontend, one PostgreSQL database, and Redis for selected cache workloads. The architecture should demonstrate sound engineering practices without introducing microservices or infrastructure that cannot reasonably be completed in the planned 1–1.5 weeks.

### Problem statement

Teams often track operational issues across chat messages, spreadsheets, and separate task tools. This makes ownership, status history, urgency, and follow-up actions difficult to track consistently.

OpsPilot provides:
- A shared incident register with severity, status, assignee, and timestamps.
- A documented activity timeline for important changes.
- Comments and notifications for collaboration.
- SLA target tracking and automatic detection of overdue incidents.
- A dashboard summarizing incident volume and resolution performance.
- A postmortem record for documenting root cause and follow-up actions.

### Target users

- **Administrators** configure and manage a team workspace and its users.
- **Responders** investigate and resolve incidents assigned to their team.
- **Viewers** read incidents and dashboards but cannot modify operational records.

This is a single-workspace/team-oriented MVP. Full multi-tenant SaaS billing, external monitoring integrations, and customer-facing status pages are out of scope.

## 2. Goals and non-goals

### Goals

1. Deliver a complete incident lifecycle from creation to closure.
2. Enforce authorization and business rules on the server, not only in the UI.
3. Keep a reliable history of important incident changes.
4. Demonstrate maintainable ASP.NET Core design, EF Core, async I/O, validation, tests, logging, caching, and background processing.
5. Provide a polished React interface that can be demonstrated with seeded data.
6. Run the application and its dependencies locally through Docker Compose.

### Non-goals for this version

- Microservices or event sourcing.
- Kubernetes or cloud-specific deployment.
- Subscription billing and tenant provisioning.
- External alert ingestion from tools such as PagerDuty, Datadog, or email.
- AI incident analysis.
- Full real-time collaboration via SignalR (optional future enhancement).
- A general-purpose project/task management system.
- Guaranteed exactly-once delivery of background jobs.

## 3. Technology stack

### Backend

- C# with ASP.NET Core Web API.
- Controller-based API using **base controllers** (not Minimal APIs).
- Entity Framework Core for persistence.
- SQL Server as the relational database.
- Redis for distributed caching of selected dashboard/query results.
- JWT bearer authentication for the API.
- FluentValidation or equivalent explicit request validation.
- Serilog or structured logging through Microsoft.Extensions.Logging.
- xUnit for automated tests.

Use the current supported .NET version available in the development environment. If starting fresh and the installed SDK supports it, use .NET 10; otherwise use the supported SDK already installed and document the version.

### Frontend

- React + TypeScript + Vite.
- Tailwind CSS and shadcn/ui (or a consistent component library).
- TanStack Query for API/server state.
- React Hook Form and Zod for form validation.
- Recharts for dashboard charts.

### Infrastructure

- Docker and Docker Compose.
- PostgreSQL and Redis containers for local development.
- GitHub Actions for build and test checks (if time permits).

## 4. Backend architecture

Use four projects/layers:

```text
src/
├── OpsPilot.Domain/
├── OpsPilot.Application/
├── OpsPilot.Infrastructure/
└── OpsPilot.Api/
```

Tests:

```text
tests/
├── OpsPilot.Domain.Tests/
├── OpsPilot.Application.Tests/
└── OpsPilot.Api.IntegrationTests/
```

### Dependency direction

```text
OpsPilot.Api ───────────────► OpsPilot.Application
      │                              ▲
      │                              │ interfaces/ports
      ▼                              │
OpsPilot.Infrastructure ────────────┘
      │
      ▼
OpsPilot.Domain

OpsPilot.Application ───────────────► OpsPilot.Domain
OpsPilot.Domain ────────────────────► (no project dependency)
```

More precisely:
- **Domain** has no dependency on the other application projects.
- **Application** references Domain and defines use cases, DTOs, validation, and interfaces required by use cases.
- **Infrastructure** references Application and Domain; it implements persistence, caching, and infrastructure services.
- **API** references Application and Infrastructure to register dependencies and expose HTTP endpoints.
- API controllers call Application services/use cases. Controllers must not contain business rules or directly implement EF Core queries.

### Domain layer — `OpsPilot.Domain`

Contains the core business concepts and invariants:
- Entities: `Incident`, `IncidentComment`, `IncidentActivity`, `Notification`, `Postmortem`, `Team`, and `User` (or an application user abstraction if using ASP.NET Core Identity).
- Enums: `IncidentStatus`, `IncidentSeverity`, `UserRole`, `ActivityType`, and `NotificationType`.
- Domain-level methods for valid status transitions and recording incident lifecycle changes where appropriate.
- No EF Core, HTTP, Redis, or framework-specific controller logic.

Keep domain methods small and explicit. Do not build a generic domain-event framework for this time-boxed project.

### Application layer — `OpsPilot.Application`

Contains application use cases and contracts:
- Use-case services, for example `IncidentService`, `DashboardService`, `TeamService`, `NotificationService`, and `PostmortemService`.
- Request/response DTOs.
- Interfaces such as `IApplicationDbContext` (or repository interfaces), `ICurrentUser`, `IDashboardCache`, and `INotificationDispatcher` only where they provide a useful boundary.
- Validation rules and authorization checks that are part of use-case execution.
- Pagination/filtering contracts.
- Mapping from entities to DTOs.

Prefer straightforward services over creating an interface for every class. For this MVP, using EF Core through an `IApplicationDbContext` abstraction is acceptable; a repository-per-entity pattern is not required.

### Infrastructure layer — `OpsPilot.Infrastructure`

Implements technical details:
- EF Core `OpsPilotDbContext`, entity configurations, migrations, and query implementations.
- ASP.NET Core Identity user persistence if selected.
- Redis cache implementation.
- Background worker for checking SLA breaches.
- Notification persistence and dispatching.
- Seed data for local demo environments.
- Database and Redis health-check registration.

### API layer — `OpsPilot.Api`

Contains:
- Controller-based REST endpoints.
- Base controller, e.g. `ApiControllerBase`, for shared access to current user identity and consistent response helpers if needed.
- Authentication and authorization configuration.
- Dependency injection registration.
- Middleware for exception handling and Problem Details.
- OpenAPI/Swagger configuration.
- Rate limiting on authentication endpoints if time permits.
- Health endpoints and application startup configuration.

Suggested controller names:
- `AuthController`
- `DashboardController`
- `IncidentsController`
- `IncidentCommentsController` (or comments nested in `IncidentsController`)
- `NotificationsController`
- `TeamsController`
- `UsersController`
- `PostmortemsController`

Use `[ApiController]` and a shared base controller if desired, but avoid putting business logic in the base class. Keep HTTP concerns in controllers and business rules in Application/Domain.

## 5. Key architectural decisions

### Modular monolith

One deployable backend keeps local setup and debugging manageable. The modules are separated by responsibility without the operational burden of distributed services.

### PostgreSQL as source of truth

Incident state, comments, activities, notifications, users, teams, and postmortems are persisted in PostgreSQL. Redis is a cache, not the authoritative data store.

### Redis cache

Cache only read-heavy dashboard aggregates initially. Use a short TTL (for example, 30–60 seconds) and explicit invalidation after incident create/update/resolve/close operations. If Redis is unavailable, the application should be able to fall back to querying PostgreSQL where practical; do not allow cache failure to corrupt incident writes.

### Background SLA worker

A `BackgroundService` periodically queries incidents that are not resolved/closed and whose `ResponseDueAt` or `ResolutionDueAt` has passed. It records an SLA breach or updates a durable field only once per breach. The worker must be idempotent to avoid duplicate notifications every polling cycle.

For the MVP, SLA targets can be configured by severity in application configuration. Do not build a dynamic SLA policy editor.

### Activity history

Record important business actions such as incident creation, status changes, assignment changes, severity changes, and resolution. Store a concise description or structured metadata. The activity log is append-only from normal application workflows; users cannot edit or delete activity records through the API.

### Authentication and authorization

The API uses authenticated requests and server-side authorization. Roles are `Admin`, `Responder`, and `Viewer`. Team scoping must be enforced when reading or modifying incidents. A role check alone is not enough if a user should only access their own team’s data.

For a small demo, use one team seeded with users. Keep the schema team-aware so access rules are explicit, but do not claim full multi-tenant isolation.

## 6. High-level request flow

```text
React + TypeScript
       │ HTTP + bearer token
       ▼
ASP.NET Core Controller
       │ validate HTTP contract / derive current identity
       ▼
Application use-case service
       │ enforce permissions and business rules
       ▼
Domain entities + application interfaces
       │
       ├── Infrastructure / EF Core ──► PostgreSQL
       ├── Infrastructure / cache ───► Redis
       └── BackgroundService ─────────► SLA checks / notifications
```

Example: when an incident is assigned:
1. React sends an authenticated request.
2. `IncidentsController` validates the request shape and calls `IncidentService`.
3. The application verifies that the caller can manage incidents in the relevant team and that the assignee belongs to that team.
4. The service updates the incident and adds an `IncidentActivity` record.
5. EF Core saves both changes in one database transaction.
6. Dashboard cache keys are invalidated.
7. A notification is persisted for the newly assigned responder.
8. The API returns the updated incident DTO.

## 7. Definition of done

The project is ready to demo when:
- The application starts using documented Docker Compose commands.
- A seeded Admin, Responder, and Viewer can sign in.
- Role/team authorization is enforced by the API.
- The incident lifecycle works end-to-end.
- Comments and activity history persist.
- Dashboard data comes from the database and uses Redis caching.
- SLA background processing is observable and does not create repeated duplicate notifications.
- Key business rules have automated tests.
- README explains setup, architecture, demo credentials, API docs, and known limitations.
