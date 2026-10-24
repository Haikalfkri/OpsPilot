# OpsPilot — Use Cases and System Flows

## 1. Actors and permissions

The MVP has three human roles plus a system background worker.

### Admin

Can:
- Sign in.
- View dashboard and incidents in the configured team.
- Create and update incidents.
- Assign incidents to active responders in the same team.
- Change incident severity and valid status.
- Comment on incidents.
- View activity history.
- View and mark their own notifications as read.
- Create/update postmortems if the feature is enabled.
- View team users and manage user role/active state within the MVP policy.

Cannot:
- Access another team's records unless explicitly granted through a future policy.
- Edit/delete append-only activity records.
- Read another user's private notification list.

### Responder

Can:
- Sign in.
- View dashboard and incidents in their team.
- Create incidents.
- Investigate incidents and perform allowed status transitions.
- Assign incidents if the project policy permits responders to assign; for the MVP, allow assignment to same-team responders.
- Update incident title, description, and severity according to policy.
- Add comments.
- View activity history.
- Read/mark their own notifications.
- Create/update a postmortem if enabled and the incident is resolved/closed.

Cannot:
- Manage users or roles.
- Access another team's records.
- Edit/delete activity records.
- Read another user's notifications.

### Viewer

Can:
- Sign in.
- View dashboard.
- List and view incident details in their team.
- Read comments and activity history.
- Read their own notifications.

Cannot:
- Create or update incidents.
- Change severity/status/assignment.
- Add comments.
- Create/update postmortems.
- Manage users.

### System worker

Can:
- Query unresolved incidents for SLA targets.
- Persist a breach record/activity once per target.
- Create in-app notifications for relevant users.
- Write system-generated activity records with a null actor or explicit system actor.

Cannot:
- Perform arbitrary user administration.
- Bypass data consistency rules.
- Create duplicate breach notifications on every polling cycle.

## 2. Use-case catalogue

| ID | Use case | Primary actor | Outcome |
|---|---|---|---|
| UC-01 | Sign in | All users | Authenticated API access |
| UC-02 | View dashboard | All users | Team-scoped operational metrics |
| UC-03 | Browse/filter incidents | All users | Paginated incident list |
| UC-04 | Create incident | Admin, Responder | Incident is persisted with initial SLA targets |
| UC-05 | View incident details | All users | Incident, comments, timeline, and SLA details |
| UC-06 | Update incident | Admin, Responder | Valid changes are saved and audited |
| UC-07 | Assign incident | Admin, Responder (policy-controlled) | Assignee updated and notified |
| UC-08 | Change status | Admin, Responder | Valid lifecycle transition is recorded |
| UC-09 | Comment on incident | Admin, Responder | Comment and timeline entry are persisted |
| UC-10 | Resolve/close incident | Admin, Responder | Lifecycle timestamps are set |
| UC-11 | View notifications | All users | User sees only their own notifications |
| UC-12 | Mark notification read | Notification owner | Notification read state changes |
| UC-13 | Detect SLA breach | System worker | Breach and notification are recorded once |
| UC-14 | Manage users | Admin | Team membership/role/active state updated |
| UC-15 | Manage postmortem | Admin, Responder (if enabled) | Root cause and corrective actions documented |

## 3. Main system flow: create an incident

1. User opens the Create Incident form.
2. React validates required fields for immediate feedback.
3. React sends `POST /api/v1/incidents` with title, description, severity, and optional assignee ID.
4. API authenticates the caller and passes the request to the application service.
5. Application checks role, team scope, field rules, and assignee eligibility.
6. Application calculates `ResponseDueAt` and `ResolutionDueAt` from the severity configuration.
7. Domain rules create the incident with status `Open`.
8. Application records an `IncidentActivity` of type `Created`.
9. If an assignee was supplied, application records assignment activity and creates a notification.
10. Infrastructure saves the related records in a transaction.
11. Application invalidates dashboard cache keys.
12. API returns `201 Created` with the incident response DTO and location/header if used.
13. React navigates to the incident detail page and shows a success state.

Failure paths:
- Invalid input: `400 Bad Request` with field errors.
- Unauthenticated: `401 Unauthorized`.
- Insufficient permission: `403 Forbidden`.
- Invalid/inactive/out-of-team assignee: validation error.
- Unexpected error: standard Problem Details response; log details server-side.

## 4. Incident lifecycle flow

```text
                 ┌──────────────────┐
                 │       Open       │
                 └────────┬─────────┘
                          │ Start investigation
                          ▼
                 ┌──────────────────┐
                 │  Investigating   │
                 └────────┬─────────┘
                          │ Mitigation / resolution
                          ▼
                 ┌──────────────────┐
                 │     Resolved     │
                 └────────┬─────────┘
                          │ Confirm completion
                          ▼
                 ┌──────────────────┐
                 │      Closed      │
                 └──────────────────┘
```

Additional allowed transition:
- `Open` → `Resolved`, when the issue is immediately mitigated.

For the MVP:
- `Closed` is terminal.
- Reopening is excluded unless implemented as an explicit operation with its own audit activity.
- Any transition not explicitly allowed must be rejected by application/domain rules.
- Status changes, timestamps, and activity records must be persisted together.

### Status transition details

- `Open` → `Investigating`: set `FirstRespondedAt` if not already set.
- `Open` → `Resolved`: set `ResolvedAt`.
- `Investigating` → `Resolved`: set `ResolvedAt`.
- `Resolved` → `Closed`: set `ClosedAt`.
- Invalid transition: return a consistent `409 Conflict` or `400 Bad Request` policy; choose one and document it. Recommended: `409 Conflict` for a state transition that conflicts with the current state.

## 5. Assignment flow

1. Authorized user selects an active responder.
2. Frontend submits the new `assigneeId`.
3. Application verifies the incident is in the caller's team.
4. Application verifies the assignee exists, is active, and belongs to the same team.
5. Application updates `AssigneeId`.
6. Application appends `IncidentActivity` with old/new assignee identifiers or display names.
7. Application creates an in-app notification for the new assignee.
8. The transaction commits and dashboard cache is invalidated.
9. API returns the updated incident DTO.

If the assignee did not change, the API should either return the unchanged resource without creating duplicate activity/notifications or return a validation response. Recommended: no-op with no duplicate activity.

## 6. Comment and activity flow

1. Authorized user opens an incident detail page.
2. User submits a non-empty comment.
3. API verifies incident access and comment length.
4. Application creates an `IncidentComment`.
5. Application creates a corresponding activity entry (or the UI combines comments and activity into one timeline using separate queries; choose one approach and keep it consistent).
6. Both records are saved in one transaction.
7. API returns the created comment DTO.
8. React updates the comments/timeline query.

Comments are immutable in the MVP. Activity records are append-only and are not directly editable through API endpoints.

## 7. SLA calculation and background-worker flow

### At incident creation

1. Application reads the configured response/resolution target durations for the selected severity.
2. `ResponseDueAt = CreatedAt + ResponseTargetDuration`.
3. `ResolutionDueAt = CreatedAt + ResolutionTargetDuration`.
4. Persist the calculated timestamps on the incident.

When severity changes before resolution, recalculate target timestamps using the documented MVP policy. Recommended policy: recalculate from the original `CreatedAt`, and record the severity change in activity history. This keeps behavior deterministic for a portfolio demo.

### Background worker

1. A `BackgroundService` runs at a configurable interval (e.g. every 60 seconds in normal development).
2. It queries incidents not resolved/closed whose response or resolution deadline has passed.
3. It checks whether the corresponding breach has already been recorded.
4. If not, it records the breach and an activity entry, and creates a notification for the assignee; if unassigned, notify the team Admin or use a documented fallback.
5. It commits the changes.
6. On the next cycle, the same breach is not recorded again.

Recommended implementation: add an `IncidentSlaBreach` entity with a unique constraint on `(IncidentId, BreachType)` to guarantee one breach record per type. If scope must be reduced, use dedicated breach timestamps/flags on Incident, but avoid both approaches simultaneously.

The worker must create a dependency-injection scope for scoped services such as `DbContext`. It should log cycle start, counts processed, and failures. It must not crash permanently because one cycle fails.

## 8. Dashboard flow and cache behavior

1. React requests `GET /api/v1/dashboard/summary` and related chart endpoints.
2. Application checks Redis for a team-scoped cache key.
3. Cache hit: return the cached dashboard DTO.
4. Cache miss: query PostgreSQL with aggregate projections, save the DTO to Redis with a short TTL, then return it.
5. On relevant incident changes, application invalidates the affected team's dashboard keys.
6. If Redis is temporarily unavailable, fall back to database queries and log the cache failure.

Cache key examples:
- `opspilot:dashboard:{teamId}:summary`
- `opspilot:dashboard:{teamId}:trends:{fromDate}:{toDate}`

Do not place access tokens or private user data in cache keys. Any cached response must be scoped to the correct team and filters.

## 9. Authentication flow

1. User submits email/password.
2. API validates credentials using the configured identity/authentication service.
3. Inactive users are rejected.
4. API issues a bearer token with stable user ID and role claims; team identity should be resolved safely by the application and not blindly trusted from arbitrary client input.
5. React stores the token according to the chosen MVP approach and sends `Authorization: Bearer <token>` for protected API requests.
6. On `401`, React clears invalid auth state and returns to sign-in.

Security note: for a public production deployment, revisit token storage, refresh tokens, expiration, revocation, and CSRF/XSS tradeoffs. For this portfolio MVP, document the chosen approach and avoid claiming production-grade identity lifecycle features that are not implemented.

## 10. Notification flow

Notifications are in-app only.

Creation triggers:
- Incident assignment.
- Response SLA breach.
- Resolution SLA breach.

1. Application determines recipient.
2. Notification is saved in PostgreSQL with `IsRead = false`.
3. User calls `GET /api/v1/notifications`.
4. API filters by the authenticated user ID.
5. User marks a notification read with `PATCH /api/v1/notifications/{id}/read`.
6. API verifies ownership before updating it.

No email, SMS, or browser push delivery is included.

## 11. Postmortem flow (optional MVP)

1. Incident is Resolved or Closed.
2. Authorized user opens the postmortem form.
3. User records summary, root cause, impact, and corrective actions.
4. Application checks that no postmortem already exists for the incident.
5. Postmortem is saved and a timeline activity is appended.
6. Users with read access can view it from the incident detail page.

## 12. Error and response conventions

Use consistent HTTP semantics:
- `200 OK`: successful read/update.
- `201 Created`: resource created.
- `204 No Content`: successful operation with no response body, if needed.
- `400 Bad Request`: malformed request or field validation failure.
- `401 Unauthorized`: missing/invalid authentication.
- `403 Forbidden`: authenticated caller lacks permission.
- `404 Not Found`: resource does not exist or is intentionally concealed by access policy.
- `409 Conflict`: invalid state transition or conflicting resource state.
- `500 Internal Server Error`: unexpected failure with safe Problem Details response.

All timestamps are stored in UTC. API responses should use ISO 8601 timestamps.
