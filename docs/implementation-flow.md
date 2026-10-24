# OpsPilot — Implementation Flow

> **Purpose:** implement OpsPilot in dependency order so that each feature builds on working foundations. Follow the phases from top to bottom; avoid building UI screens against APIs, permissions, or domain rules that do not exist yet.

## How to use this roadmap

- Work in small vertical slices: **database/domain → application logic → API → authorization/validation → tests → UI → end-to-end verification**.
- Finish the phase's exit criteria before starting the next phase.
- Keep the backend authoritative for identity, permissions, team access, status transitions, SLA calculations, and audit history. The frontend may hide unavailable actions, but that is not security enforcement.
- Build only the MVP path first. Add polish and optional enhancements after the end-to-end incident lifecycle works.
- Use migrations and seed data from the beginning; do not manually change the database schema without a migration.

## Dependency overview

```text
Project foundation
  └─ Identity + roles + teams + authorization
       └─ Incident creation and retrieval
            └─ Incident lifecycle + assignment + activity history
                 ├─ Comments
                 ├─ Dashboard and filtering
                 ├─ SLA tracking + background worker
                 │    └─ Notifications
                 └─ Postmortems
  └─ Cross-cutting hardening: tests, logging, Docker, documentation
```

Dashboard, notifications, and postmortems depend on real incident data and stable incident rules. Do not implement them before the core incident lifecycle.

---

## Phase 0 — Confirm scope and establish the project foundation

### 0.1 Lock the MVP scope
- [ ] Confirm roles: **Admin**, **Responder**, and **Viewer**.
- [ ] Confirm incident statuses: **Open → Investigating → Resolved → Closed**.
- [ ] Confirm severity levels and required incident fields.
- [ ] Confirm team-based visibility and which roles can create, assign, update, resolve, and close incidents.
- [ ] Confirm SLA policy inputs (for example, severity-based response/resolution targets) before implementing SLA calculations.
- [ ] Treat AI, billing, CRM, inventory, and unrelated modules as out of scope.

### 0.2 Create the repository and application skeleton
- [ ] Create the solution and projects: `OpsPilot.Api`, `OpsPilot.Application`, `OpsPilot.Domain`, and `OpsPilot.Infrastructure`.
- [ ] Create the React + TypeScript frontend using Vite.
- [ ] Add formatting, linting, environment configuration, and a consistent folder structure.
- [ ] Add a basic health endpoint and a minimal frontend shell.
- [ ] Add Docker Compose for the app dependencies, initially PostgreSQL; add Redis when its first real use case is implemented.
- [ ] Add environment-variable examples without committing secrets.

**Exit criteria:** backend and frontend both run locally; the API health endpoint responds; configuration is environment-based; the solution builds cleanly.

## Phase 1 — Data model, persistence, and API conventions

### 1.1 Set up persistence
- [ ] Configure PostgreSQL and Entity Framework Core.
- [ ] Add the initial `DbContext`, entity configuration, and migration workflow.
- [ ] Establish naming, timestamps (UTC), ID strategy, and database constraints/index conventions.
- [ ] Add a development seed-data strategy that can create predictable local test data.

### 1.2 Establish API foundations
- [ ] Configure dependency injection and application services.
- [ ] Add centralized exception handling and consistent error responses (including validation errors).
- [ ] Add request validation, pagination conventions, and structured logging.
- [ ] Configure Swagger/OpenAPI for development.
- [ ] Add a test project and a first smoke test.

**Exit criteria:** a clean database can be created from migrations; the API starts against it; errors have a consistent shape; basic tests run in CI or locally.

## Phase 2 — Authentication and authorization

> Implement identity and access rules before building protected incident features. Use a supported ASP.NET Core authentication approach (for example, ASP.NET Core Identity with JWT bearer tokens if the frontend is a separate SPA). Do not invent a custom password/token system.

### 2.1 User identity and account lifecycle
- [ ] Define the user model and identity configuration.
- [ ] Implement login and secure password handling through the chosen identity framework.
- [ ] Implement registration only if self-registration is part of the product scope. For an internal operations tool, prefer admin-created/invited accounts unless public registration is explicitly required.
- [ ] Implement logout/token expiry handling appropriate to the chosen token strategy.
- [ ] Add current-user endpoint (`/api/me`) for profile and authorization context.
- [ ] Return generic authentication errors; never expose whether a password or account detail was correct.

### 2.2 Roles and permissions
- [ ] Define the Admin, Responder, and Viewer roles/permissions.
- [ ] Seed roles and a development admin account safely (development only; no hard-coded production credentials).
- [ ] Add policy-based authorization for API endpoints and sensitive operations.
- [ ] Test unauthenticated requests, authenticated requests, and forbidden role actions.

### 2.3 Team membership and team-scoped access
- [ ] Implement Team and user-to-team membership relationships.
- [ ] Decide whether users may belong to one or multiple teams and enforce that model consistently.
- [ ] Implement server-side checks for access to a team and its incidents.
- [ ] Prevent users from assigning incidents to unauthorized teams or users outside the permitted team scope.
- [ ] Add authorization tests for cross-team access and object-ID guessing (IDOR).

### 2.4 Frontend authentication shell
- [ ] Build login page and API client configuration.
- [ ] Add authenticated route handling and current-user loading state.
- [ ] Add role-aware navigation/action visibility.
- [ ] Handle expired sessions and unauthorized responses gracefully.

**Exit criteria:** users can authenticate; `/api/me` returns the correct user context; role and team restrictions are enforced by the API and covered by tests; the frontend can distinguish loading, unauthenticated, and forbidden states.

## Phase 3 — Teams and user administration

This phase comes before incident assignment because incidents need valid teams and assignees.

### 3.1 Team management
- [ ] Create, list, view, and update teams (Admin only for management actions).
- [ ] Validate team names and prevent accidental duplicates where appropriate.
- [ ] Add team membership management.

### 3.2 User administration
- [ ] List and view users for authorized admins.
- [ ] Assign roles and team membership with authorization checks.
- [ ] Support account activation/deactivation if included in the agreed MVP.
- [ ] Ensure a user cannot grant themselves a higher role through request payloads.

### 3.3 Frontend
- [ ] Build Team Management and basic User Management views.
- [ ] Add forms, validation, loading/error/empty states, and success feedback.

**Exit criteria:** an Admin can prepare teams and users; team membership is stored and enforced; later incident forms can load legitimate team/assignee choices.

## Phase 4 — Core incident management (first end-to-end vertical slice)

> This is the first major product milestone. Get a simple incident from creation through retrieval working before adding dashboards, notifications, or SLA automation.

### 4.1 Domain rules and persistence
- [ ] Implement the Incident entity and required fields: title, description, severity, status, creator, team, assignee (optional initially), timestamps, and SLA policy reference/values as agreed.
- [ ] Define allowed severity values and validation rules.
- [ ] Define status transitions centrally in domain/application logic; do not let the frontend choose arbitrary status values.
- [ ] Enforce team and user references with database constraints where applicable.

### 4.2 Incident API
- [ ] Create incident (creator and server-owned fields come from authenticated context, not client-supplied IDs).
- [ ] Get incident by ID with authorization checks.
- [ ] List incidents with pagination and basic sorting.
- [ ] Filter by status, severity, team, and assignee.
- [ ] Update editable incident fields with validation.
- [ ] Assign/reassign incident to a valid team and/or responder.
- [ ] Implement status transitions: Open → Investigating → Resolved → Closed, with invalid transitions rejected.
- [ ] Add unit/integration tests for permissions, validation, filters, and transitions.

### 4.3 Incident frontend
- [ ] Build Incident List with filters, sorting, pagination, and empty/loading/error states.
- [ ] Build Create Incident form.
- [ ] Build Incident Detail page.
- [ ] Build Edit/Assign actions and status-transition controls based on permissions.
- [ ] Verify the full flow against the real API, not mock data.

**Exit criteria:** a permitted user can create an incident, see it in the list, open its detail page, update allowed fields, assign it, and move it through valid lifecycle transitions. Unauthorized and invalid actions are rejected by the backend.

## Phase 5 — Audit trail, activity timeline, and comments

These features depend on a stable incident lifecycle and provide context for collaboration and later postmortems.

### 5.1 Activity history
- [ ] Implement IncidentActivity records for meaningful events: creation, field changes, assignment, and status transitions.
- [ ] Write activity records from application services in the same transaction as the corresponding incident change when practical.
- [ ] Do not rely on the frontend to submit authoritative audit events.
- [ ] Return a chronological activity timeline from the incident API.

### 5.2 Comments
- [ ] Implement add/list comments for an incident.
- [ ] Enforce incident/team visibility on every comment endpoint.
- [ ] Decide whether editing/deleting comments is in scope; if included, define who can do it and whether edited comments are marked.
- [ ] Add tests for comments, permissions, and activity behavior.

### 5.3 Frontend
- [ ] Display the activity timeline on Incident Detail.
- [ ] Add the comments panel and submit flow.
- [ ] Clearly distinguish comments from system-generated activity.

**Exit criteria:** changes and collaboration are visible in the incident timeline; audit records are generated by trusted backend code; cross-team users cannot read or add comments to incidents they cannot access.

## Phase 6 — SLA policy and tracking

> Define SLA behavior before implementing the background worker or notifications. Otherwise, automation may send incorrect alerts or duplicate them.

### 6.1 SLA rules
- [ ] Confirm response and resolution targets and how they vary by severity/team.
- [ ] Define when each clock starts, pauses, resumes, and stops. Keep the MVP simple and document any exclusions (for example, business hours versus calendar hours).
- [ ] Calculate and persist or deterministically derive response/resolution deadlines on the server.
- [ ] Define what counts as a response (for example, first transition to Investigating or an explicitly recorded acknowledgement).
- [ ] Define breach semantics and how resolved/closed incidents affect pending breach checks.
- [ ] Add tests for deadlines, boundary times, status changes, and timezone handling (store timestamps in UTC).

### 6.2 SLA display
- [ ] Expose SLA deadlines, remaining time, and breach state in incident API responses.
- [ ] Display SLA indicators on Incident List and Incident Detail.
- [ ] Keep SLA calculations server-authoritative; the frontend only formats and displays returned values.

**Exit criteria:** SLA behavior is documented and testable; the UI shows the same SLA state as the API; edge cases are covered before automated breach alerts are enabled.

## Phase 7 — Background processing and notifications

### 7.1 Background worker
- [ ] Choose a reliable worker/scheduler approach suitable for the project (for example, a hosted .NET BackgroundService; use a persistent job system if stronger scheduling/retry guarantees are needed).
- [ ] Add a periodic SLA-breach scan with bounded batches and cancellation support.
- [ ] Make processing idempotent: repeated scans must not create duplicate breach records or notifications for the same incident/event.
- [ ] Add logging and retry/error handling; avoid depending on in-memory state for durable processing.

### 7.2 Notifications
- [ ] Implement notification records and recipient rules (incident creator, assignee, or team members as explicitly agreed).
- [ ] Create notifications for relevant events such as assignment and SLA breach; avoid sending a notification for every low-value field change.
- [ ] Implement list, unread count, mark-as-read, and mark-all-as-read endpoints.
- [ ] Add authorization so users can only view or update their own notifications.
- [ ] Test duplicate prevention, retry behavior, and notification ownership.

### 7.3 Frontend
- [ ] Add notification list/panel and unread indicator.
- [ ] Add read/unread interactions and useful links back to the relevant incident.
- [ ] Add visible SLA-breach indicators and notification feedback.

**Exit criteria:** background processing can be run repeatedly without duplicate alerts; notifications are persisted and user-scoped; SLA breach and assignment flows work end to end.

## Phase 8 — Operations dashboard and workload views

Build dashboards after the incident data, status rules, team membership, and SLA definitions are stable.

- [ ] Implement authorized aggregate endpoints for incident counts by status/severity and incident trends over a defined time range.
- [ ] Implement team workload/assignee summaries with the same team-scope rules as incident lists.
- [ ] Ensure queries are paginated or bounded where appropriate and avoid loading all incidents into application memory.
- [ ] Add indexes based on observed query patterns; validate aggregation performance with realistic seed data.
- [ ] Build KPI cards, incident trend charts, severity/status breakdowns, recent incidents, and team workload views.
- [ ] Make dashboard filters/time ranges explicit and ensure each metric has a clear definition.
- [ ] Test that users cannot infer counts for teams/data they are not allowed to access.

**Exit criteria:** dashboard metrics reconcile with underlying incident queries, respect authorization, and remain responsive with realistic data.

## Phase 9 — Postmortems

Postmortems depend on incident details, timeline, comments, and outcomes.

- [ ] Define the postmortem lifecycle and who can create/edit/publish it.
- [ ] Implement create/read/update operations linked to an incident.
- [ ] Include agreed fields such as summary, impact, root cause, timeline, resolution, and action items.
- [ ] Validate that postmortem access follows the parent incident's authorization scope.
- [ ] Build the Postmortem view/form and link it from Incident Detail.
- [ ] Add tests for permissions, required fields, and incident linkage.

**Exit criteria:** authorized users can document and review a postmortem for an incident without bypassing incident access rules.

## Phase 10 — Frontend completion and UX consistency

- [ ] Complete Profile and Settings only for settings that are actually supported by the backend.
- [ ] Standardize form validation, toast messages, confirmation dialogs, tables, badges, and loading/empty/error states.
- [ ] Ensure keyboard accessibility, clear focus states, responsive layouts, and readable severity/status indicators.
- [ ] Add route-level error handling and a not-found page.
- [ ] Check role-specific UI states, but keep all security checks on the backend.
- [ ] Remove accidental mock data and dead controls before the demo/release.

**Exit criteria:** every visible control has a real supported behavior or is clearly disabled/not yet available; critical user flows work on desktop and narrower screens.

## Phase 11 — Production-minded hardening and delivery

### Quality and security
- [ ] Add unit tests for domain rules and application services.
- [ ] Add API integration tests for authentication, authorization, validation, and incident workflows.
- [ ] Add frontend tests for high-value forms and key flows.
- [ ] Validate input sizes and file/text limits; protect against over-posting and insecure direct object references.
- [ ] Review secrets, CORS, HTTPS, token storage/expiry, rate limiting where appropriate, and dependency vulnerabilities.
- [ ] Avoid logging passwords, tokens, sensitive request bodies, or secrets.

### Reliability and performance
- [ ] Add structured logs and correlation/request IDs.
- [ ] Check database indexes and query plans for key list/dashboard endpoints.
- [ ] Configure health checks for the API and required dependencies.
- [ ] Verify Redis is used only where it provides a clear benefit (for example, selected read caching or distributed coordination); the app must remain correct if cache entries expire.
- [ ] Verify background worker restart/retry behavior and duplicate prevention.

### Delivery
- [ ] Finalize Docker Compose for local demo with API, frontend, PostgreSQL, and Redis if used.
- [ ] Document setup, environment variables, migrations, seed data, test commands, and demo accounts (development only).
- [ ] Add a concise README with architecture, feature list, screenshots, API docs, and known limitations.
- [ ] Run a clean-clone test from setup through the primary incident flow.

**Exit criteria:** another developer can clone and run the app from documentation; automated tests pass; the primary flows work in a clean environment; known limitations are explicit.

---

## Recommended implementation order at a glance

| Order | Feature group | Why it comes here |
|---:|---|---|
| 0 | Scope and project skeleton | Prevents rework and establishes a runnable base |
| 1 | Database, API conventions, test setup | Needed by every backend feature |
| 2 | Authentication, roles, team authorization | Protects every subsequent endpoint and defines ownership |
| 3 | Team and user administration | Provides valid teams and assignees for incidents |
| 4 | Incident CRUD, filters, assignment, lifecycle | Core product workflow and source of operational data |
| 5 | Activity timeline and comments | Adds traceability and collaboration on stable incidents |
| 6 | SLA rules and display | Establishes correct deadline/breach semantics before automation |
| 7 | Background worker and notifications | Depends on stable SLA rules and notification ownership |
| 8 | Dashboard and workload | Depends on reliable incident data and metric definitions |
| 9 | Postmortems | Depends on incident outcomes and history |
| 10 | UX completion | Consolidates the full feature set consistently |
| 11 | Hardening and delivery | Validates the integrated application end to end |

## Practical rules to prevent implementation rework

1. **Do not build dashboard charts first.** They depend on stable incident states, permissions, and metric definitions.
2. **Do not build notifications before events and recipients are defined.** Otherwise notification rules will be rewritten as features change.
3. **Do not start SLA automation before the SLA policy is written and tested.** A worker can reliably automate the wrong rules just as easily as the right ones.
4. **Do not trust client-supplied creator IDs, role IDs, team permissions, deadlines, or activity records.** Derive or validate them on the server.
5. **Do not implement every screen as a disconnected mock.** After the core API is ready, complete one vertical slice at a time against real endpoints.
6. **Do not add Redis just to claim usage.** Add it when a concrete caching or coordination need exists, and keep PostgreSQL as the source of truth.
7. **Do not build every edge case before the main path works.** First make the happy path correct, then add invalid-input, permission, retry, and concurrency tests.
8. **Keep a Definition of Done for every feature:** migrations (if needed), domain/application logic, API contract, authorization, tests, UI states, and documentation are complete.

## MVP demo path

For a convincing first demo, prioritize this complete sequence before optional polish:

1. Admin signs in and creates teams/users or uses seeded demo accounts.
2. Responder signs in and submits an incident.
3. Incident appears in the list and can be filtered by status/severity/team.
4. Authorized responder assigns the incident and transitions it to Investigating.
5. A comment and system activity appear in the incident timeline.
6. Incident is resolved and then closed using valid transitions.
7. SLA information is visible; if breach automation is complete, demonstrate a persisted breach and notification.
8. Dashboard reflects the same incident data; an authorized user can create/view its postmortem if that phase is complete.

If time is limited, finish steps 1–6 robustly before attempting dashboard charts or advanced notification delivery.
