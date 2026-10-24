# OpsPilot — API and Entity Design

## 1. API conventions

- Base path: `/api/v1`
- Format: JSON over HTTPS in deployment; HTTP may be used locally.
- Authentication: bearer token for protected endpoints.
- Controllers: ASP.NET Core controller-based API, using `[ApiController]` and a shared `ApiControllerBase` where useful.
- Pagination: `page` (1-based) and `pageSize` (default 20, maximum 100).
- Timestamps: ISO 8601 UTC.
- Errors: `application/problem+json` / Problem Details, with field validation errors where applicable.
- Never return EF Core entities directly. Use request/response DTOs.
- All incident queries are scoped to the authenticated user's authorized team.
- API route names and DTOs should remain consistent; this document is the source of truth for the MVP.

Example paginated response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0
}
```

Example validation error:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "title": ["Title is required."]
  }
}
```

## 2. Authentication and current-user endpoints

### `POST /api/v1/auth/login`

Access: anonymous.

Request:
```json
{
  "email": "responder@opspilot.local",
  "password": "DemoPassword123!"
}
```

Response `200`:
```json
{
  "accessToken": "<token>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-10-24T10:00:00Z",
  "user": {
    "id": "uuid",
    "displayName": "Demo Responder",
    "email": "responder@opspilot.local",
    "role": "Responder",
    "teamId": "uuid"
  }
}
```

Use the actual expiry configured by the API. Demo credentials must only be seeded for Development.

### `GET /api/v1/auth/me`

Access: authenticated.

Returns the current user's profile, role, team ID, and active state.

### `POST /api/v1/auth/logout`

For a stateless JWT MVP, the API may not need a logout endpoint; the frontend clears its token. Include this endpoint only if the selected authentication implementation has server-side session/revocation behavior. Do not pretend that deleting a client token revokes an already-issued JWT.

## 3. Dashboard endpoints

### `GET /api/v1/dashboard/summary`

Access: Admin, Responder, Viewer.

Response fields:
- `openCount`
- `highOrCriticalOpenCount`
- `responseSlaBreachedCount`
- `resolutionSlaBreachedCount`
- `resolvedInPeriodCount`
- `averageResolutionMinutes`
- `generatedAtUtc`

Optional query:
- `fromUtc`
- `toUtc`

### `GET /api/v1/dashboard/incident-breakdown`

Access: Admin, Responder, Viewer.

Returns incident counts grouped by status and severity for the authorized team.

### `GET /api/v1/dashboard/trends`

Access: Admin, Responder, Viewer.

Query:
- `fromUtc`
- `toUtc`
- `bucket` (`day` or `week`; validate with an allowlist)

Returns time-bucketed created/resolved counts. Cache key must include the team and normalized query parameters.

## 4. Incident endpoints

### `GET /api/v1/incidents`

Access: Admin, Responder, Viewer.

Query parameters:
- `page`
- `pageSize`
- `search`
- `status`
- `severity`
- `assigneeId`
- `createdFromUtc`
- `createdToUtc`
- `sortBy` (allowlist: `createdAt`, `updatedAt`, `severity`, `responseDueAt`, `resolutionDueAt`)
- `sortDirection` (`asc` or `desc`)

Returns paginated `IncidentListItemDto` records. All filters are team-scoped.

### `POST /api/v1/incidents`

Access: Admin, Responder.

Request:
```json
{
  "title": "Payment API unavailable",
  "description": "Requests to the payment endpoint are returning 503.",
  "severity": "Critical",
  "assigneeId": null
}
```

The API derives `teamId` and `createdById` from the authenticated context. The client must not choose arbitrary values for these fields.

Response: `201 Created` with `IncidentDetailDto`.

### `GET /api/v1/incidents/{incidentId}`

Access: Admin, Responder, Viewer.

Returns incident detail, current assignee, creator, SLA fields, comments (or a separate comments call), activity timeline, and postmortem summary if present. To keep payloads manageable, comments and activities may instead use their own paginated endpoints.

### `PATCH /api/v1/incidents/{incidentId}`

Access: Admin, Responder.

Request fields are optional:
```json
{
  "title": "Payment API partially unavailable",
  "description": "Updated investigation details.",
  "severity": "High"
}
```

This endpoint updates editable descriptive fields and severity only. Use the dedicated status and assignment endpoints for lifecycle/assignment changes. Recalculate target timestamps according to the documented severity-change policy. Record activities for actual changes only.

### `POST /api/v1/incidents/{incidentId}/status`

Access: Admin, Responder.

Request:
```json
{
  "status": "Investigating"
}
```

Validates the state transition. Sets lifecycle timestamps and creates activity in the same transaction.

### `PUT /api/v1/incidents/{incidentId}/assignee`

Access: Admin, Responder.

Request:
```json
{
  "assigneeId": "uuid"
}
```

Set `assigneeId` to null to unassign if the product policy permits it. Validate that a non-null assignee is an active member of the same team. Record assignment activity and notify the new assignee.

### `GET /api/v1/incidents/{incidentId}/activities`

Access: Admin, Responder, Viewer.

Query: `page`, `pageSize`.

Returns append-only activity entries in chronological order (oldest first for a timeline; document if using newest-first pagination).

### `GET /api/v1/incidents/{incidentId}/comments`

Access: Admin, Responder, Viewer.

Query: `page`, `pageSize`.

Returns comments ordered by creation time.

### `POST /api/v1/incidents/{incidentId}/comments`

Access: Admin, Responder.

Request:
```json
{
  "body": "I am investigating the payment provider timeout."
}
```

Creates a comment and a timeline entry, then returns the created comment DTO.

## 5. Notification endpoints

### `GET /api/v1/notifications`

Access: authenticated.

Query:
- `page`
- `pageSize`
- `unreadOnly` (boolean)

Returns only notifications owned by the current user.

### `GET /api/v1/notifications/unread-count`

Access: authenticated.

Returns `{ "unreadCount": 3 }` for the current user.

### `PATCH /api/v1/notifications/{notificationId}/read`

Access: authenticated.

Marks the notification read only if it belongs to the current user. Repeating the request should be safe and should not create side effects.

## 6. Team and user endpoints

### `GET /api/v1/teams/current`

Access: authenticated.

Returns the caller's current team summary.

### `GET /api/v1/teams/current/users`

Access: Admin, Responder, Viewer.

Returns active team members for assignment selectors. Viewer access is read-only.

### `GET /api/v1/users`

Access: Admin.

Query: `page`, `pageSize`, `search`, `role`, `isActive`.

Returns users in the authorized workspace/team only.

### `POST /api/v1/users`

Access: Admin.

Creates a user or invitation-like local account according to the selected identity approach. For the time-boxed MVP, a simpler admin-created user with a generated/resettable password is acceptable. Do not implement email invitations unless delivery is actually configured.

### `PATCH /api/v1/users/{userId}`

Access: Admin.

Allows permitted updates such as display name, role, and active state. Do not allow arbitrary team reassignment if only one seeded team is supported. Prevent disabling the last active Admin if that rule is implemented.

If time is limited, user management may be reduced to listing seeded users and changing role/active state, rather than building a full invitation flow.

## 7. Postmortem endpoints (optional MVP)

### `GET /api/v1/incidents/{incidentId}/postmortem`

Access: Admin, Responder, Viewer.

Returns the postmortem if it exists; return `404` if not.

### `PUT /api/v1/incidents/{incidentId}/postmortem`

Access: Admin, Responder.

Creates or updates the one postmortem for an incident. Only permitted when the incident is Resolved or Closed.

Request:
```json
{
  "summary": "The payment API was unavailable for 18 minutes.",
  "rootCause": "Connection pool exhaustion during a traffic spike.",
  "impact": "Some customers could not complete payment.",
  "correctiveActions": "Add pool saturation alerts and review pool sizing."
}
```

Use a unique database constraint on `IncidentId` to enforce at most one postmortem per incident.

## 8. Health endpoint

### `GET /health`

May be anonymous for local diagnostics, or restricted in a public deployment. Returns application health and dependency status without exposing credentials or connection strings.

---

## 9. Entity model

Use UUIDs (`Guid`) for IDs. All persisted timestamps are UTC (`DateTimeOffset` is recommended). Use explicit EF Core configurations and migrations.

### 9.1 `Team`

Purpose: groups users and incidents.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Name` | `string` | Required, max 120 |
| `CreatedAtUtc` | `DateTimeOffset` | Required |

Relationships:
- One Team has many Users.
- One Team has many Incidents.

The MVP may seed one team; a public multi-tenant onboarding flow is out of scope.

### 9.2 `User`

Purpose: operational profile and authorization context. If using ASP.NET Core Identity, use an `ApplicationUser : IdentityUser<Guid>` and add the profile/team fields instead of creating a separate password table.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `DisplayName` | `string` | Required, max 120 |
| `Email` | `string` | Required, unique, normalized by identity system |
| `Role` | `UserRole` | Required: Admin, Responder, Viewer |
| `TeamId` | `Guid` | Required FK to Team for MVP |
| `IsActive` | `bool` | Required, default true |
| `CreatedAtUtc` | `DateTimeOffset` | Required |

Relationships:
- Many Users belong to one Team.
- One User can create many Incidents.
- One User can be assigned many Incidents.
- One User can create many Comments and Activities.
- One User can own many Notifications.

Do not store plaintext passwords on this entity. Delegate password hashing and credential validation to ASP.NET Core Identity or a well-established authentication implementation.

### 9.3 `Incident`

Purpose: central operational record.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `TeamId` | `Guid` | Required FK to Team |
| `Title` | `string` | Required, 5–160 chars |
| `Description` | `string` | Required, max 5,000 |
| `Severity` | `IncidentSeverity` | Required |
| `Status` | `IncidentStatus` | Required; default Open |
| `CreatedById` | `Guid` | Required FK to User |
| `AssigneeId` | `Guid?` | Optional FK to User |
| `CreatedAtUtc` | `DateTimeOffset` | Required |
| `UpdatedAtUtc` | `DateTimeOffset` | Required |
| `FirstRespondedAtUtc` | `DateTimeOffset?` | Set on first transition to Investigating |
| `ResolvedAtUtc` | `DateTimeOffset?` | Set on transition to Resolved |
| `ClosedAtUtc` | `DateTimeOffset?` | Set on transition to Closed |
| `ResponseDueAtUtc` | `DateTimeOffset` | Required |
| `ResolutionDueAtUtc` | `DateTimeOffset` | Required |

Relationships:
- Many Incidents belong to one Team.
- Each Incident has one creator User.
- Each Incident has zero or one assignee User.
- One Incident has many Comments, Activities, and SLA Breaches.
- One Incident has zero or one Postmortem.

Indexes:
- `(TeamId, Status, CreatedAtUtc)`
- `(TeamId, Severity, CreatedAtUtc)`
- `(TeamId, AssigneeId, Status)`
- `(TeamId, ResolutionDueAtUtc, Status)`

Avoid storing a duplicate `IsSlaBreached` boolean if using `IncidentSlaBreach` as the source of truth. Dashboard counts can be calculated from breach records.

### 9.4 `IncidentComment`

Purpose: immutable discussion entry attached to an incident.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `IncidentId` | `Guid` | Required FK to Incident |
| `AuthorId` | `Guid` | Required FK to User |
| `Body` | `string` | Required, max 2,000 |
| `CreatedAtUtc` | `DateTimeOffset` | Required |

Relationships:
- Many Comments belong to one Incident.
- Each Comment has one author User.

### 9.5 `IncidentActivity`

Purpose: append-only record of significant changes.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `IncidentId` | `Guid` | Required FK to Incident |
| `ActorId` | `Guid?` | Nullable for system-generated activity |
| `Type` | `ActivityType` | Required |
| `Message` | `string` | Required, max 500 |
| `MetadataJson` | `string?` | Optional structured details |
| `CreatedAtUtc` | `DateTimeOffset` | Required |

Relationships:
- Many Activities belong to one Incident.
- An Activity has zero or one actor User.

Use `MetadataJson` sparingly, for example to record previous/new severity or previous/new assignee ID. Do not serialize entire EF entities. Activity is append-only through normal application APIs.

### 9.6 `Notification`

Purpose: in-app notification for one recipient.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `RecipientId` | `Guid` | Required FK to User |
| `IncidentId` | `Guid?` | Optional FK to Incident |
| `Type` | `NotificationType` | Required |
| `Title` | `string` | Required, max 160 |
| `Message` | `string` | Required, max 500 |
| `IsRead` | `bool` | Required, default false |
| `CreatedAtUtc` | `DateTimeOffset` | Required |
| `ReadAtUtc` | `DateTimeOffset?` | Set when marked read |

Relationships:
- Many Notifications belong to one recipient User.
- A Notification may reference one Incident.

Indexes:
- `(RecipientId, IsRead, CreatedAtUtc)`

### 9.7 `IncidentSlaBreach`

Purpose: durable, idempotent record that a response or resolution SLA target was breached.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `IncidentId` | `Guid` | Required FK to Incident |
| `BreachType` | `SlaBreachType` | Required: Response, Resolution |
| `BreachedAtUtc` | `DateTimeOffset` | Required |
| `CreatedAtUtc` | `DateTimeOffset` | Required |

Relationships:
- Many breach records belong to one Incident, but only one per `BreachType` for the MVP.

Constraint:
- Unique index `(IncidentId, BreachType)`.

The worker inserts the breach record only if it does not already exist. Use a transaction and handle unique-constraint races safely. The MVP does not track repeated breach cycles after an incident is reopened.

### 9.8 `Postmortem` (optional MVP)

Purpose: record lessons learned after an incident.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `IncidentId` | `Guid` | Required FK, unique |
| `AuthorId` | `Guid` | Required FK to User |
| `Summary` | `string` | Required, max 2,000 |
| `RootCause` | `string` | Required, max 4,000 |
| `Impact` | `string` | Required, max 2,000 |
| `CorrectiveActions` | `string` | Required, max 4,000 |
| `CreatedAtUtc` | `DateTimeOffset` | Required |
| `UpdatedAtUtc` | `DateTimeOffset` | Required |

Relationships:
- One Incident has zero or one Postmortem.
- Each Postmortem has one author User.

### 9.9 Enums

`UserRole`
- `Admin`
- `Responder`
- `Viewer`

`IncidentSeverity`
- `Low`
- `Medium`
- `High`
- `Critical`

`IncidentStatus`
- `Open`
- `Investigating`
- `Resolved`
- `Closed`

`ActivityType`
- `Created`
- `Updated`
- `SeverityChanged`
- `StatusChanged`
- `AssigneeChanged`
- `CommentAdded`
- `Resolved`
- `Closed`
- `ResponseSlaBreached`
- `ResolutionSlaBreached`
- `PostmortemCreated`
- `PostmortemUpdated`

`NotificationType`
- `IncidentAssigned`
- `ResponseSlaBreached`
- `ResolutionSlaBreached`

`SlaBreachType`
- `Response`
- `Resolution`

## 10. Entity relationship summary

```text
Team 1 ───── * User
Team 1 ───── * Incident

User 1 ───── * Incident (CreatedBy)
User 0..1 ── * Incident (Assignee)
User 1 ───── * IncidentComment (Author)
User 0..1 ── * IncidentActivity (Actor)
User 1 ───── * Notification (Recipient)
User 1 ───── * Postmortem (Author)

Incident 1 ─ * IncidentComment
Incident 1 ─ * IncidentActivity
Incident 1 ─ * Notification (optional reference)
Incident 1 ─ * IncidentSlaBreach
Incident 1 ─ 0..1 Postmortem
```

Use explicit foreign-key relationships in EF Core configurations. Because `Incident` references `User` twice (creator and assignee), configure both relationships explicitly to avoid ambiguous navigation mapping. Decide delete behavior deliberately; prefer restrictive/no-action behavior for users/incidents with history rather than cascading deletion of audit records.

## 11. DTO design

Keep API contracts separate from persistence entities.

### `IncidentListItemDto`

- `id`
- `title`
- `severity`
- `status`
- `assignee` (small user summary or null)
- `createdAtUtc`
- `updatedAtUtc`
- `responseDueAtUtc`
- `resolutionDueAtUtc`
- `isResponseSlaBreached`
- `isResolutionSlaBreached`

### `IncidentDetailDto`

Includes list fields plus:
- `description`
- `createdBy` (small user summary)
- `firstRespondedAtUtc`
- `resolvedAtUtc`
- `closedAtUtc`
- optional `postmortemSummary`

Keep comments and activities as separate paginated requests if the detail response becomes too large.

### `IncidentCommentDto`

- `id`
- `incidentId`
- `author` (small user summary)
- `body`
- `createdAtUtc`

### `IncidentActivityDto`

- `id`
- `incidentId`
- `actor` (small user summary or `System`)
- `type`
- `message`
- `metadata` (only if safe and useful)
- `createdAtUtc`

### `NotificationDto`

- `id`
- `incidentId`
- `type`
- `title`
- `message`
- `isRead`
- `createdAtUtc`
- `readAtUtc`

### `PostmortemDto`

- `id`
- `incidentId`
- `author`
- `summary`
- `rootCause`
- `impact`
- `correctiveActions`
- `createdAtUtc`
- `updatedAtUtc`

## 12. EF Core relationship and consistency rules

- Use `Guid` primary keys and explicit foreign keys.
- Use enum string conversion for database readability if preferred; keep the conversion strategy consistent.
- Add unique index for normalized user email and `Postmortem.IncidentId`.
- Add unique index for `IncidentSlaBreach(IncidentId, BreachType)`.
- Use restrictive delete behavior for users and incidents with historical records.
- Update `Incident.UpdatedAtUtc` on successful modifications.
- Persist incident changes and corresponding activity records in one transaction.
- Persist assignment and notification in the same transaction where practical.
- Never let the frontend supply `CreatedById`, authoritative `TeamId`, or audit actor ID.
- Do not store the Redis cache as an entity or persist cache data to PostgreSQL.
- Store UTC timestamps consistently and test date-boundary filtering.
