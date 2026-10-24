# OpsPilot — Features and Requirements

## 1. Purpose

This document defines the MVP features, functional requirements, non-functional requirements, and scope boundaries for OpsPilot. Treat these requirements as the implementation contract. If a feature is not listed as part of the MVP, do not add it before the core workflows are complete.

## 2. Roles

- **Admin**: manages team members and has full operational access within the configured workspace/team.
- **Responder**: views incidents in their team, investigates incidents, updates status, comments, and resolves incidents according to the workflow.
- **Viewer**: read-only access to permitted incident and dashboard data.

All access checks must be enforced by the backend. Hiding a button in React is not authorization.

## 3. Functional requirements

### F-01 — Authentication

- Users can sign in using email and password.
- Successful sign-in returns the authentication mechanism configured by the backend (JWT bearer token for this API).
- Invalid credentials return a generic authentication error and must not reveal whether an email exists.
- Protected endpoints require authentication.
- The frontend supports sign-out by clearing the client-side token/session state.
- Demo users are seeded only in Development/demo environments; credentials must not be hard-coded for production.

**Acceptance criteria**
- Anonymous requests to protected endpoints receive `401 Unauthorized`.
- A valid user can sign in and call an authorized endpoint.
- Invalid credentials do not return a token.

### F-02 — Role and team authorization

- Every user has one role: Admin, Responder, or Viewer.
- Every operational user belongs to a team for the MVP.
- Users may only read incidents belonging to their team.
- Admins can manage users and team membership within the configured workspace.
- Responders can create incidents and update permitted incidents in their team.
- Viewers cannot create, update, assign, comment, resolve, or close incidents.
- The API returns `403 Forbidden` when an authenticated user lacks permission, or `404 Not Found` when using not-found semantics to avoid leaking resource existence. Choose and apply a consistent policy.

**Acceptance criteria**
- A Viewer cannot modify an incident even by calling the API directly.
- A user from another team cannot read or modify an incident outside their team.
- The frontend's controls reflect permissions but do not replace backend checks.

### F-03 — Operations dashboard

The dashboard displays:
- Total open incidents.
- Critical/high open incidents.
- Incidents breached against a response or resolution target.
- Resolved incidents in a selected recent period.
- Average resolution time for resolved incidents in the selected period.
- Incident counts grouped by severity and status.
- A recent incident list.

Requirements:
- Dashboard values are derived from persisted incident data.
- Date ranges use a documented timezone convention; store timestamps in UTC and display in the user's/local demo timezone.
- Read-heavy dashboard results may be cached in Redis.
- Cache TTL and invalidation behavior must be documented.

**Acceptance criteria**
- Creating/resolving an incident eventually updates the dashboard within the configured cache TTL or through explicit invalidation.
- An empty database returns valid zero/empty values rather than errors.
- Dashboard queries are team-scoped.

### F-04 — Incident management

Users with write permission can create an incident with:
- Title (required, 5–160 characters).
- Description (required, sensible maximum such as 5,000 characters).
- Severity: Low, Medium, High, Critical.
- Optional initial assignee who must belong to the same team.

The system sets:
- Unique incident ID.
- Team ID from the authenticated user's permitted context, not from an untrusted arbitrary client value.
- Initial status `Open`.
- Created and updated timestamps.
- Response and resolution target timestamps based on severity configuration.
- Creator identity.

Authorized users can view incident details, update title/description/severity, assign a responder, and transition status according to the lifecycle rules.

List requirements:
- Pagination is mandatory.
- Filtering by status, severity, assignee, and date range.
- Search by title and optionally description.
- Sorting by created date, updated date, severity, and SLA due date, using an allowlist of sort fields.

**Acceptance criteria**
- Invalid fields produce a structured `400 Bad Request` response.
- List endpoints never return incidents outside the caller's permitted team.
- A create/update action adds an activity record.
- Writes are persisted to PostgreSQL.

### F-05 — Incident lifecycle and SLA

Statuses:
- `Open`
- `Investigating`
- `Resolved`
- `Closed`

Allowed transitions:
- `Open` → `Investigating`
- `Open` → `Resolved` (for an incident that is immediately mitigated)
- `Investigating` → `Resolved`
- `Resolved` → `Closed`
- `Resolved` → `Investigating` only through an explicit reopen action (optional for MVP; if implemented, record it as reopening)
- `Closed` is terminal for the MVP.

Disallow all other transitions.

Severity-based target times are configuration values, not user-editable in this version. Suggested demo defaults:

| Severity | Response target | Resolution target |
|---|---:|---:|
| Critical | 15 minutes | 4 hours |
| High | 1 hour | 8 hours |
| Medium | 4 hours | 24 hours |
| Low | 8 hours | 72 hours |

These are demo rules, not a claim of an industry standard. Document that the targets are configurable examples.

Requirements:
- Store response and resolution target timestamps on the incident when created or when severity changes.
- Store `FirstRespondedAt` when the incident first moves to `Investigating`.
- Store `ResolvedAt` when it moves to `Resolved`.
- A background worker checks unresolved incidents for breached targets.
- A breach is recorded at most once per target per incident.
- The system can create an in-app notification for the responsible responder/admin when a breach occurs.

**Acceptance criteria**
- Illegal status transitions are rejected by the application/domain rules.
- Target timestamps are calculated consistently.
- SLA worker can be run repeatedly without generating duplicate breach records/notifications.

### F-06 — Assignment

- An incident may be unassigned or assigned to one responder.
- An assignee must be an active member of the same team.
- Admins and Responders with appropriate permission can change assignment.
- Assignment changes are written to activity history.
- Assigning an incident creates an in-app notification for the new assignee.

**Acceptance criteria**
- The API rejects assignment to a disabled user or user outside the team.
- Assignment history records the previous and new assignee where applicable.

### F-07 — Comments

- Authorized team members can add comments to an incident.
- Comment body is required and has a reasonable maximum length (for example, 2,000 characters).
- Comments show author and creation timestamp.
- Users cannot edit or delete comments in the MVP; this avoids additional audit complexity.
- Creating a comment adds an activity entry or appears in the incident timeline through a consistent timeline query.

**Acceptance criteria**
- Comment is visible after reload.
- A user cannot comment on an incident outside their team.
- Empty or oversized comments are rejected.

### F-08 — Activity timeline

Append-only activity types include:
- Incident created.
- Status changed.
- Severity changed.
- Assignee changed.
- Incident resolved.
- Incident closed.
- Comment added.
- SLA response target breached.
- SLA resolution target breached.
- Postmortem created/updated (if implemented).

Each activity contains incident ID, actor (nullable for system-generated actions), action type, timestamp, and concise details or structured metadata.

**Acceptance criteria**
- Important changes generate one corresponding activity record in the same transaction as the related write where possible.
- Activity records are returned in chronological order.
- There is no API for arbitrary activity edits/deletes.

### F-09 — In-app notifications

- Users can list their own notifications.
- Users can mark a notification as read.
- Notifications are created for incident assignment and SLA breach.
- A user cannot read or mark another user's notification as read.
- Notification delivery is in-app only; email, SMS, and push notifications are out of scope.

**Acceptance criteria**
- Notifications persist in PostgreSQL.
- Unread count is available to the frontend.
- Access is restricted to the notification owner.

### F-10 — Postmortem

A postmortem is an optional but recommended MVP feature if time permits:
- At most one postmortem per incident.
- Available for resolved or closed incidents.
- Contains summary, root cause, impact, and corrective/preventive action items.
- Created/updated by an authorized team member.
- Postmortem changes appear in the incident activity timeline.

**Acceptance criteria**
- Postmortem cannot be created for an incident outside the user's team.
- Duplicate postmortems for the same incident are prevented by application validation and a database unique constraint.

### F-11 — Team and user administration

- Admin can list team users and their roles.
- Admin can activate/deactivate a user or change their role within the supported policy.
- Admin can assign a user to the configured team.
- The MVP may seed one team; team CRUD is not required.
- Prevent deactivating the last active Admin, if that workflow is included.

**Acceptance criteria**
- Only Admin can perform user administration.
- Inactive users cannot sign in or perform protected actions.

### F-12 — Health and diagnostics

- Expose a health endpoint for the API.
- Include database health check.
- Include Redis health check if configured as a required dependency for the running environment.
- Use structured logs for application errors and important background-worker events.
- Do not log passwords, bearer tokens, or sensitive authentication data.

**Acceptance criteria**
- Health endpoint returns a clear healthy/unhealthy status.
- Errors are logged with enough context to debug without leaking secrets.

## 4. Non-functional requirements

### Security
- All write endpoints require authentication.
- Enforce role and team scope in backend use cases.
- Validate and normalize input.
- Use parameterized EF Core queries; do not concatenate SQL from user input.
- Keep secrets in environment variables/user secrets, not committed configuration.
- Configure CORS for the known frontend origin in development.
- Do not expose stack traces in production responses.

### Reliability and data integrity
- PostgreSQL is the source of truth.
- Related incident/activity/notification writes should use a database transaction where consistency requires it.
- Background work must be idempotent.
- Redis failures must not lose incident records.
- Use database constraints for unique and required relationships where appropriate.

### Performance
- Paginate all potentially large list endpoints.
- Use `AsNoTracking` for read-only EF Core queries where appropriate.
- Project to DTOs instead of returning full EF entities.
- Add indexes for common filters, including team/status/severity/created time and notification owner/read state.
- Cache only selected read-heavy dashboard responses; do not cache sensitive user-specific data under a shared key.

### Maintainability
- Follow the four-project backend architecture: Domain, Application, Infrastructure, API.
- Controllers are thin and based on a shared API controller base where useful.
- Keep business rules out of React components and controllers.
- Document public endpoints with OpenAPI.
- Add tests for important business rules and authorization.

### Usability
- Provide loading, empty, success, and error states in React.
- Confirm destructive or irreversible actions.
- Use consistent status/severity labels.
- Make incident creation and assignment understandable without documentation.

### Deployment and developer experience
- `docker compose up --build` should start the local dependencies and application, subject to documented environment setup.
- Provide migrations and deterministic seed data.
- Document demo credentials for local development only.
- Keep setup steps short and reproducible.

## 5. MVP priority

### Must-have
- Authentication and role/team authorization.
- Incident CRUD and lifecycle validation.
- Assignment.
- Comments and activity timeline.
- Dashboard and filters/pagination.
- PostgreSQL migrations.
- Redis dashboard cache.
- SLA background worker and in-app notifications.
- Docker Compose.
- Unit tests for core business rules.

### If time permits
- Postmortem UI and report export.
- API integration tests with a real test database.
- GitHub Actions CI.
- SignalR live updates.
- More elaborate audit metadata.

## 6. Explicitly out of scope

Do not implement these in the initial project:
- Billing/subscriptions.
- Customer/tenant signup and tenant provisioning.
- External monitoring/alert integrations.
- Email/SMS/push notification delivery.
- AI features.
- Kubernetes.
- Microservices.
- Complex configurable workflow designer.
- Arbitrary custom roles and permissions.
- Full incident analytics/BI platform.

## 7. Demo acceptance checklist

- [ ] Sign in as Admin, Responder, and Viewer.
- [ ] Show that Viewer cannot write even through direct API calls.
- [ ] Create a Critical incident.
- [ ] Assign it to a responder and show the notification.
- [ ] Move it to Investigating and verify activity history.
- [ ] Demonstrate SLA due timestamps and a breach using shortened Development-only demo thresholds or seeded test data.
- [ ] Resolve and close the incident.
- [ ] Show dashboard metrics and Redis cache behavior in logs or a short technical explanation.
- [ ] Restart containers and verify persisted incident data remains in PostgreSQL.
- [ ] Run automated tests.
