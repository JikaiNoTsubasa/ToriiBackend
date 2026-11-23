# ⛩️ ToriiBackend

API of Torii, a web-based project management tool. "Torii" as in the Japanese
gates: the entrance to your projects.

The Angular frontend lives in the separate **ToriiFrontend** repository.

## Stack

- **.NET 10** (ASP.NET Core Web API) — `Torii.Api`
- **EF Core** + **PostgreSQL** (Npgsql)
- **Auth**: JWT access token + refresh token (rotated on every use), BCrypt passwords
- **Errors**: normalized ProblemDetails responses (RFC 9457)
- **Logging**: Serilog — colored console + rolling files (`Torii.Api/logs/`)

## Logging

Set up in `Torii.Api/Logging/LoggingExtensions.cs` (sinks and templates are in code, so the
API logs even without configuration):

- **Console**: `[HH:mm:ss LVL] Source: message`;
- **Files**: `logs/torii-YYYYMMDD.log` under the content root, with full date and offset;
  a new file every day or at 10 MB, the last 14 are kept (ignored by git);
- **Requests**: one line per HTTP request (method, path, status, duration);
  4xx → warning, 5xx → error;
- **EF Core** is silenced (no SQL); database errors are still logged by `GlobalExceptionHandler`.

Levels are tuned in the `Serilog:MinimumLevel` section of `appsettings*.json`
(`Torii` is at `Debug` in Development).

## Prerequisites

- **.NET SDK 10** (`dotnet --version` → `10.0.x`)
- **PostgreSQL** (local or remote)
- EF Core tool: pinned locally in `dotnet-tools.json` → `dotnet tool restore`

## Configuration

Copy `Torii.Api/appsettings.example.json` to `Torii.Api/appsettings.json`
(ignored by git) and fill in:

- `ConnectionStrings:ToriiDb` — PostgreSQL connection string;
- `Jwt:Key` — secret key of **≥ 32 characters** (HMAC-SHA256).

> In dev, an empty key lets the API start but tokens will not be reliable.
> To keep secrets out of the repository, prefer `dotnet user-secrets` or an
> environment variable.

## Database

Migrations live in `Torii.Api/Migrations` (`InitialCreate`, `AddUserRole`,
`AddedAccountType`, `AddTicket`, `AddedCustomers`, `AddTicketChannels`, `AddedTicketTitle`,
`AddTicketEdges`, `AddTicketMessages`, `ReleaseDeletedUserEmails`, `AddTicketCreator`,
`AddChannelStatus`, `AddMessageForward`, `AddChannelFilesAndActivities`, `AddUserAvatar`,
`AddTicketTypeAndPriority`, `AddAccountApproval`, `AddSingpass`):

```bash
cd Torii.Api
dotnet ef database update
```

To evolve the schema: `dotnet ef migrations add <Name>` then
`dotnet ef database update`.

## First admin account (seed)

At startup, `Data/Seeders/AdminSeeder.cs` creates an `ADMIN` account **as long as
no admin exists** (idempotent), from the `Seed:Admin` section:

```bash
cd Torii.Api
dotnet user-secrets init
dotnet user-secrets set "Seed:Admin:Email" "admin@torii.dev"
dotnet user-secrets set "Seed:Admin:Password" "a-strong-password"
dotnet user-secrets set "Seed:Admin:DisplayName" "Administrator"   # optional
```

(or the `Seed__Admin__Email` / `Seed__Admin__Password` environment variables).
Without configuration, a warning is logged and nothing is created. If the email
already exists without the admin role, the account is not promoted (warning). The
role is carried in the JWT: `[Authorize(Roles = "ADMIN")]`.

## Running in development

```bash
cd Torii.Api
dotnet run --launch-profile http   # http://localhost:5080
```

- OpenAPI (dev): `http://localhost:5080/openapi/v1.json`
- CORS allowed for the frontend on `http://localhost:4200` (`Program.cs`).
- VS Code: **Run and Debug → "API (.NET debug)"**, EF tasks in `.vscode/tasks.json`.
- Sample requests: `Torii.Api/Torii.Api.http`.

## Authentication

Endpoints (`Controllers/AuthController.cs`, `/api/auth` prefix):

| Method | Route       | Description                              | Auth       |
|--------|-------------|------------------------------------------|------------|
| POST   | `/register` | Requests an account (202, no tokens)     | no         |
| POST   | `/login`    | Signs in, returns the tokens             | no         |
| POST   | `/refresh`  | Exchanges a refresh token for new tokens | no         |
| POST   | `/logout`   | Revokes a refresh token                  | no         |
| GET    | `/me`       | Current user                             | **Bearer** |

```bash
curl -X POST http://localhost:5080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"a@b.co","password":"password123","displayName":"Alice","position":"PM"}'
```

### Account requests (sign-up with approval)

`/register` creates a `PENDING`, disabled account (`User.Status`, `AccountStatus` enum) and
sends an `ACCOUNT_REQUESTED` in-app notification to every active administrator. No session
is issued. Signing in with valid credentials of a pending or declined account answers
**403** with a `code` extension (`ACCOUNT_PENDING` / `ACCOUNT_REJECTED`); the status is only
revealed once the password is verified.

Administrators review the requests (`Controllers/Admin/AdminAccountRequestController.cs`,
`/api/admin/account-requests` prefix, **ADMIN**):

| Method | Route           | Description |
|--------|-----------------|-------------|
| GET    | `/`             | Pending requests, oldest first |
| GET    | `/count`        | Number of pending requests (menu badge) |
| POST   | `/{id}/approve` | Activates the account (`ACTIVE`) |
| POST   | `/{id}/reject`  | Marks it `REJECTED` (stays disabled, keeps its email) |

Singpass account requests (below) go through the same review, flagged `accountType: SINGPASS`.
Either decision removes the request's notifications; deciding a request that is no longer
pending answers 404. Pending accounts are not listed in `/api/admin/users`; declined ones
are (`status: REJECTED`), and enabling one there approves it. Deleting it frees the email.

## Singpass (sign-in and account requests)

Singpass Login following the **FAPI 2.0** specification (mandatory for apps registered after
February 2026): Pushed Authorization Request + PKCE (S256), `private_key_jwt` client assertions
(ES256, `aud` = issuer), DPoP proofs bound to an ephemeral key per sign-in, and an ID token
encrypted with our key (JWE `ECDH-ES+A256KW` / `A256CBC-HS512`, opened with **jose-jwt**,
since Microsoft.IdentityModel 8 cannot unwrap ECDH-ES) and signed by Singpass (ES256, checked
against their JWKS).

Flow (`Services/Singpass/`):

1. `POST /api/auth/singpass/start` pushes the authorization request and returns the Singpass
   URL; the attempt (state, nonce, PKCE verifier, DPoP key) is kept **in memory** 10 minutes.
2. Singpass sends the browser back to the frontend's `/auth/singpass/callback`, which posts
   `{ code, state, iss, error }` to `POST /api/auth/singpass/callback`.
3. The `sub` (Singpass UUID) is looked up in `Users.SingpassSubject`: a linked, approved
   account is signed in (`SIGNED_IN` + tokens); pending / declined → the usual 403
   (`ACCOUNT_PENDING` / `ACCOUNT_REJECTED`); unknown → `REGISTRATION_REQUIRED` with a single-use
   token (15 min) and the name / email Singpass gave (scopes `name`, `email`).
4. `POST /api/auth/singpass/register` `{ token, email, displayName, position }` creates a
   PENDING `SINGPASS` account (no usable password) reviewed in the account requests.

Settings (`SingpassConfigs` table, single row) are edited by administrators
(`/api/admin/singpass`, **ADMIN**): `GET`, `PUT` (enabled, environment STAGING / PRODUCTION,
client id, redirect URI, scopes), `POST /keys` (new signing + encryption keys) and
`POST /test` (reads the discovery document). **Enabled** greys out / enables the Singpass
buttons; it can only be turned on once the client id, redirect URI and keys are set. The
private keys never leave the server; register the public **JWKS URL**
`/api/auth/singpass/jwks` (anonymous, must be publicly reachable) and the redirect URI in the
Singpass Developer Portal.

> In Development, `Singpass:IssuerOverride` points the client to a local mock issuer.
> Deleting a SINGPASS account clears its `SingpassSubject`, so the person can request again.

## Profile

Endpoints of the signed-in user (`Controllers/ProfileController.cs`, `/api/profile` prefix, **Bearer**):

| Method | Route                   | Description |
|--------|-------------------------|-------------|
| GET    | `/`                     | Own profile (identity, role, customers, avatar) |
| PUT    | `/`                     | Update `{ displayName }` |
| PUT    | `/email`                | Change `{ newEmail, currentPassword }` (400 wrong password, 409 taken) |
| PUT    | `/password`             | Change `{ currentPassword, newPassword }`: every session is revoked, a fresh `AuthResponse` is returned |
| POST   | `/avatar`               | Upload an avatar (multipart `file`; PNG, JPEG, WebP or GIF checked from the file signature, 5 MB max) |
| DELETE | `/avatar`               | Back to the default avatar |
| POST   | `/sessions/revoke-all`  | Sign out of all devices |

A wrong current password answers **400** (not 401, which the client treats as an expired
session). Uploaded avatars are stored with the file storage and served **anonymously** by
`GET /api/users/{id}/avatar` (an `<img>` cannot send the Bearer token); `User.AvatarUrl`
becomes `/api/users/{id}/avatar?v={version}`, the version changing on every upload so the
response can be cached for 30 days.

## Tickets

- `Number` primary key in the `TI000001` format, generated by PostgreSQL (`TicketNumbers`
  sequence + `next_ticket_number()` function).
- Model: `Ticket` 1─N `TicketChannel` (bidirectional) 1─N `ChannelParticipant` N─1 `Customer`
  N─N `User` (`CustomerMembers` table); `TicketChannel` 1─N `TicketMessage` (author = `User`).
- Visibility (`TicketManager.QueryVisibleTo`): a ticket is visible if the user is a member
  of a customer taking part in one of its channels (soft-deleted channels and customers
  excluded, no ADMIN exception). Reuse it for every ticket endpoint.

- Creation (`TicketManager.CreateAsync`): the user picks one of their customers as the
  source, a target allowed by an active ticket edge, a title and a content. In one
  transaction it creates the ticket, its channel (source + target as participants) and the
  first `TicketMessage` (author = user, HTML content). 403 if the user is not a member of
  the source customer or no edge allows source → target; 400 on an empty title / content.

| Method | Route                       | Description                                         | Auth       |
|--------|-----------------------------|-----------------------------------------------------|------------|
| GET    | `/api/tickets?search=&status=&type=&priority=&sort=&dir=&page=&pageSize=` | One page of the visible tickets, newest first by default (see below) | **Bearer** |
| GET    | `/api/tickets/send-options` | User's customers that may send, with their targets  | **Bearer** |
| POST   | `/api/tickets`              | Create `{ sourceCustomerId, targetCustomerId, title, content }` | **Bearer** |
| PUT    | `/api/tickets/{number}/channels/{channelId}/status` | Change a channel's status `{ status }` (404 if not a participant) | **Bearer** |

| GET    | `/api/tickets/{number}`     | Details: the user's channels with their messages (404 if none) | **Bearer** |
| POST   | `/api/tickets/{number}/channels/{channelId}/messages` | Reply in a channel `{ content }` (participants only) | **Bearer** |
| POST   | `/api/tickets/{number}/channels/{channelId}/messages/{messageId}/forward` | Forward a message `{ targetChannelId }` or `{ sourceCustomerId, targetCustomerId }` (+ optional `comment`) | **Bearer** |
| POST   | `/api/tickets/{number}/channels` | Open a channel `{ sourceCustomerId, targetCustomerId, content }` (edge required; 409 if these customers are already linked) | **Bearer** |

Each ticket has a **type** (`BUG`, `INCIDENT`, `FEATURE_REQUEST`, `IMPROVEMENT`, `REQUEST`,
`QUESTION`; default `REQUEST`) and a **priority** (`CRITICAL`, `HIGH`, `MEDIUM`, `LOW`; default
`MEDIUM`), shared by all its channels: chosen on creation, changed by any participant with
`PUT /api/tickets/{number}/classification` `{ type, priority }`.

The list is filtered and paginated server-side and returns a `PagedResult`
(`{ items, page, pageSize, totalCount, totalPages }`): `search` matches the number or the
title (case-insensitive `ILIKE`, wildcards escaped); `status` (repeatable,
`?status=NEW&status=IN_PROGRESS`) keeps tickets where at least one of the user's channels has
one of them; `type` / `priority` (repeatable too) keep tickets of one of them — an absent
filter means "any"; `page` is 1-based, `pageSize` defaults to 25 (max 100).
`sort` = `CREATED_AT` (default) | `NUMBER` | `TITLE` | `TYPE` | `PRIORITY` (by urgency, not
alphabetically) | `CREATED_BY` (creator's name), `dir` = `DESC` (default) | `ASC`; ties are broken
newest first so that paging stays stable.

The list and the details only return the channels the user takes part in: the same ticket
looks different depending on who reads it (e.g. a channel opened between Globex and a
subcontractor is invisible to the original requester). Each message tells its author's
customer in the channel, `isMine` and `isOwnSide` (written by the user's side).

### Activity feed & files

Each channel has an activity feed (`TicketActivities`): `CHANNEL_OPENED`, `STATUS_CHANGED`
(from → to), `FILE_UPLOADED`, `FILE_FORWARDED`, `TYPE_CHANGED` / `PRIORITY_CHANGED` (from → to;
ticket-wide, so logged in every channel of the ticket) — with the actor and the date. The details
return it next to the messages; the UI merges both into one chronological feed. The
`AddChannelFilesAndActivities` migration backfills the opening of existing channels and their
last known status change.

Files are stored **on disk** under `Storage:Path` (default `storage/files`, relative to the
content root; git-ignored), as `yyyy/MM/{guid}` keys generated by the app; the metadata lives in
`StoredFiles`. A file is attached to channels through `ChannelFiles` links: forwarding a file
only adds a link with the same file id (the content is never copied). Downloads go through a
channel the user sees, and are always served as attachments with `nosniff`.
`Storage:MaxFileSizeMb` (default 25) limits each file; one upload request is limited to 100 MB.

| Method | Route | Description |
|--------|-------|-------------|
| POST   | `/api/tickets/{number}/channels/{channelId}/files` | Upload (multipart field `files`, several allowed) |
| GET    | `/api/tickets/{number}/channels/{channelId}/files/{fileId}` | Download |
| POST   | `/api/tickets/{number}/channels/{channelId}/files/{fileId}/forward` | Forward `{ targetChannelId }` or `{ sourceCustomerId, targetCustomerId }` (409 if already there) |

Forwarding (`TicketManager.ForwardMessageAsync`) copies a message the user sees into another
channel of the same ticket: an existing channel of the user, or the channel between one of
their customers and a target customer — reused when it exists (no edge needed, like a reply),
created otherwise (active edge required). The forward is a new message by the user whose
`Content` is the optional comment and `ForwardedFromId` links the root original (forwarding
a forward without comment points to the original, never a chain). Its DTO carries the
original in `forwarded` (author, author's customer, date, content).

### Statuses (per channel)

Each `TicketChannel` has its own `Status` (`TicketStatus` enum, `NEW` on creation), plus
`StatusChangedAt` / `StatusChangedById`. Any member of a customer of the channel can change
it; transitions are free (a closed channel can be reopened).

| Status                    | Meaning                                               |
|---------------------------|-------------------------------------------------------|
| `NEW`                     | Created, not yet looked at                            |
| `ACKNOWLEDGED`            | Seen and accepted, work not started yet               |
| `IN_PROGRESS`             | Being worked on                                       |
| `WAITING_FOR_REQUESTER`   | Waiting for an answer from the requester              |
| `WAITING_FOR_THIRD_PARTY` | Waiting for an external party (supplier, other team…) |
| `ON_HOLD`                 | Deliberately paused                                   |
| `RESOLVED`                | A solution was provided, waiting for confirmation     |
| `CLOSED`                  | Done                                                  |
| `CANCELLED`               | Dropped without resolution                            |

Message content is HTML from the rich text editor: Angular sanitizes it when rendered
with `[innerHTML]`, but sanitize it on the backend before any use outside the app
(emails, exports).

## Dashboard

`GET /api/dashboard` (`TicketManager.GetDashboardAsync`) computes the home dashboard over the
channels the user can see: KPIs (open conversations, conversations awaiting the user's side —
latest message from the other side —, tickets created in 7 days, channels resolved / closed in
30 days, messages in 14 days), channels by status, open tickets by priority, messages per UTC
day (14 days, zeros included), the conversations waiting for an answer (most urgent, then
longest waiting first) and the latest tickets.

## Notifications

In-app notifications (`Notifications` table, `NotificationManager`). A message posted or
forwarded into a channel notifies every active member of the channel's customers except its
author. Unread notifications are **coalesced per channel** (`Count`, latest actor / message /
date) instead of piling up; replying in a channel marks the author's own notification of it
read. A notification is only listed while its channel is still visible to the recipient.

| Method | Route | Description |
|---|---|---|
| GET | `/api/notifications?unreadOnly=&take=` | Latest notifications (newest first, 20 by default, 50 max) + unread total |
| GET | `/api/notifications/unread` | `{ count, latestAt }` — cheap, polled by the UI |
| POST | `/api/notifications/{id}/read` | Marks one read |
| POST | `/api/notifications/channels/{channelId}/read` | Marks a channel's notifications read (sent when it is viewed) |
| POST | `/api/notifications/read-all` | Marks everything read |

## Administration (ADMIN role)

Controllers in `Controllers/Admin/*`, protected by `[Authorize(Roles = nameof(UserRole.ADMIN))]`.
Deleting a customer is a soft delete (`IsDeleted`, `DeletedAt`, `DeletedById`); the
`CreatedById` / `UpdatedById` audit fields are set to the signed-in admin.

| Method | Route                                        | Description                          |
|--------|----------------------------------------------|--------------------------------------|
| GET    | `/api/admin/customers`                       | List (name, member count)            |
| POST   | `/api/admin/customers`                       | Create `{ name }`                    |
| GET    | `/api/admin/customers/{id}`                  | Details + members                    |
| PUT    | `/api/admin/customers/{id}`                  | Rename `{ name }`                    |
| DELETE | `/api/admin/customers/{id}`                  | Delete (soft delete)                 |
| POST   | `/api/admin/customers/{id}/members`          | Add a member `{ userId }` (409 if already a member) |
| DELETE | `/api/admin/customers/{id}/members/{userId}` | Remove a member                      |
| GET    | `/api/admin/users?search=&activeOnly=&take=` | Users (disabled included unless `activeOnly`; `take` default 100, max 500) |
| GET    | `/api/admin/users/{id}`                      | User details                         |
| POST   | `/api/admin/users`                           | Create `{ email, displayName, password, role }` (409 if the email exists) |
| PUT    | `/api/admin/users/{id}`                      | Update `{ email, displayName, role, isActive, newPassword? }` |
| DELETE | `/api/admin/users/{id}`                      | Delete (soft delete)                 |

### User administration safeguards

An admin cannot delete, disable or demote their own account, and the last active
administrator cannot lose that status (409). Disabling or deleting a user revokes their
refresh tokens: their session ends when the current access token expires (≤ 15 min), and
sign-in is refused. Deleting a user releases their email: it becomes
`deleted-{id}-{unix seconds}-{original email}` (original kept for audit, truncated to 256
characters), so the address can be used by a new account right away. The
`ReleaseDeletedUserEmails` data migration applies the same format to accounts deleted
before. Role changes take effect on the user's next token refresh.
| GET    | `/api/admin/ticket-edges`                    | Ticket edges (routing configuration) |
| POST   | `/api/admin/ticket-edges`                    | Create `{ sourceCustomerId, targetCustomerId }` (409 if it exists) |
| PUT    | `/api/admin/ticket-edges/{id}`               | Update `{ sourceCustomerId, targetCustomerId, isActive }` |
| DELETE | `/api/admin/ticket-edges/{id}`               | Delete (soft delete)                 |

### Ticket configuration: edges

A `TicketEdge` states that a **source** customer may send tickets to a **target**
customer. Edges are **directed** (A → B does not allow B → A) and can be disabled
without being deleted (`IsActive`). Database rules: one live edge per (source, target)
pair (unique index filtered on `IsDeleted = false`), no self-edge (check constraint),
and customers referenced by an edge cannot be hard-deleted. Edges whose customer was
soft-deleted are hidden. `TicketEdgeManager.CanSendAsync(source, target)` is the check to
use when creating tickets.

## Structure

```
ToriiBackend/
├── Torii.slnx
├── dotnet-tools.json            # dotnet-ef 10 (local tool)
└── Torii.Api/
    ├── Program.cs               # DI, EF, JWT, CORS, ProblemDetails, JSON, seed, pipeline
    ├── Controllers/             # AuthController, TicketController, ToriController (base)
    │   └── Admin/               # AdminCustomerController, AdminUserController
    ├── Data/                    # ToriiDbContext, UpperCaseEnumConverter, Seeders/AdminSeeder
    ├── Dtos/                    # Auth, Ticket, Customer DTOs
    ├── Exceptions/              # ToriiException + Validation/NotFound/Conflict/Authentication
    ├── Handlers/                # GlobalExceptionHandler → ProblemDetails
    ├── Json/                    # UPPERCASE enum JSON serialization
    ├── Managers/                # ToriManager (base), Ticket/Customer/User managers
    ├── Models/                  # Entity (base), User, Customer, Ticket, TicketChannel…
    ├── Services/                # TokenService, AuthService
    └── Migrations/
```

## Adding to the business model

1. Create the entities in `Models/` (inherit from `Entity`). Enums are automatically
   stored as UPPERCASE strings, in the database (`Data/UpperCaseEnumConverter.cs`) as well
   as in JSON (`Json/UpperCaseEnumJson.cs`, integers rejected on input, case ignored); name
   the values in `UPPER_SNAKE` (e.g. `ON_GOING`) to keep word boundaries.
2. Declare them with a `DbSet<>` in `Data/ToriiDbContext.cs` (+ configuration in `OnModelCreating`).
3. `dotnet ef migrations add <Name>` then `dotnet ef database update`.
4. Add a manager (inherits from `ToriManager`) and register it in `Program.cs`.
5. Expose `[Authorize]` controllers under `/api/...`; throw `ToriiException`s for business errors.

All user-facing text (API error messages, ProblemDetails titles) and code comments
are written in English.

## Production build

```bash
cd Torii.Api && dotnet publish -c Release
```

## To do

- [ ] Rest of the business data model
- [ ] Fine-grained roles / permissions if needed
- [ ] Unit tests and CI
- [ ] Harden token storage (httpOnly cookie) before production
