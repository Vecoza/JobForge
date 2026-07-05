# JobForge

A background job processor built to demonstrate asynchronous processing,
retry strategies, and system reliability patterns — not just request/response
HTTP.

**Use case**: a REST API accepts email notification requests and queues them
in PostgreSQL. A separate Worker Service polls the queue, sends the emails,
and handles failures with automatic retries, exponential backoff, and
dead-lettering — the concepts that come up constantly in real backend systems
but rarely get exercised in typical CRUD-API portfolio projects.

## Why this project

Most junior/mid-level portfolio projects only exercise request/response HTTP.
This one is built around a second, independent process — a Worker Service —
that has to coordinate safely with the API over shared state in Postgres,
survive failures without losing or duplicating work, and degrade gracefully
under retry. Specifically, it demonstrates:

- **Idempotent API design** — duplicate requests (same `RequestId`) don't
  create duplicate jobs.
- **Safe concurrent job claiming** — the worker claims jobs using
  `UPDATE ... FOR UPDATE SKIP LOCKED` in a single atomic round trip, so
  multiple worker instances (or overlapping polling ticks) can never process
  the same job twice.
- **Retry with exponential-style backoff** — failed sends retry up to 3
  times (1 min → 5 min → 15 min) before giving up.
- **Dead-lettering** — jobs that exhaust retries move to a `FailedJobs` table
  with the final error recorded, while the original job history is preserved.
- **Graceful shutdown** — in-flight jobs run to completion rather than being
  aborted mid-send when the worker is asked to stop.

## Architecture

```
┌─────────────┐        writes         ┌──────────────┐
│  JobForge   │  ───────────────────► │              │
│    .Api     │                       │  PostgreSQL  │
│ (REST API)  │                       │  Jobs table  │
└─────────────┘                       │ FailedJobs   │
                                       │    table     │
┌─────────────┐        polls          │              │
│  JobForge   │  ◄──────────────────► │              │
│  .Worker    │   claims (SKIP LOCKED)└──────────────┘
│(BackgroundS)│
└──────┬──────┘
       │ sends
       ▼
   MailKit → Mailtrap (dev SMTP sandbox)
```

- **JobForge.Api** — ASP.NET Core Web API. Accepts notification requests,
  exposes queue stats.
- **JobForge.Core** — shared class library: entities, `AppDbContext`, EF Core
  migrations. Referenced by both Api and Worker.
- **JobForge.Worker** — .NET Worker Service (`BackgroundService`). Polls the
  queue every 5 seconds, sends emails via MailKit, handles retry/backoff/
  dead-letter.

## Job lifecycle

```
Pending ──(claimed)──► Processing ──(send succeeds)──► Completed
                             │
                             └──(send fails, attempts remaining)──► Pending
                                        (NextRunAt pushed forward by backoff)
                             │
                             └──(send fails, attempts exhausted)──► Failed
                                        (+ row inserted into FailedJobs)
```

Backoff schedule: attempt 1 fail → retry in 1 min, attempt 2 fail → retry in
5 min, attempt 3 fail → retry in 15 min, attempt 4 fail → dead-letter.

## The atomic claim query

The core correctness guarantee of this project: no two worker processes (or
two overlapping polling ticks) can ever claim the same job. This is done with
a single SQL statement, not a check-then-update:

```sql
UPDATE "Jobs"
SET "Status" = 'Processing', "ClaimedAt" = now(), "UpdatedAt" = now()
WHERE "Id" IN (
    SELECT "Id" FROM "Jobs"
    WHERE "Status" = 'Pending' AND "NextRunAt" <= now()
    ORDER BY "NextRunAt"
    LIMIT 10
    FOR UPDATE SKIP LOCKED
)
RETURNING *;
```

`FOR UPDATE SKIP LOCKED` means a concurrent poller trying to claim the same
rows simply skips past ones already locked by another transaction, rather
than blocking or double-claiming.

## Running locally

### Prerequisites
- .NET 10 SDK
- PostgreSQL (local instance or Docker)
- A [Mailtrap](https://mailtrap.io) sandbox account (free tier) for dev email
  testing — or use placeholder credentials to exercise the retry/dead-letter
  path without actually sending mail

### Setup

```bash
git clone https://github.com/Vecoza/JobForge.git
cd JobForge
```

Set your local connection string and Mailtrap credentials via user secrets
(never committed to the repo):

```bash
cd JobForge.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Username=<user>;Password=<pass>;Database=JobForge;"
cd ../JobForge.Worker
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Username=<user>;Password=<pass>;Database=JobForge;"
dotnet user-secrets set "Mailtrap:Host" "sandbox.smtp.mailtrap.io"
dotnet user-secrets set "Mailtrap:Port" "2525"
dotnet user-secrets set "Mailtrap:Username" "<your-mailtrap-username>"
dotnet user-secrets set "Mailtrap:Password" "<your-mailtrap-password>"
cd ..
```

Apply the database migration:

```bash
dotnet ef database update --project JobForge.Core --startup-project JobForge.Api
```

Run both processes (in separate terminals):

```bash
dotnet run --project JobForge.Api
dotnet run --project JobForge.Worker
```

### Try it out

Submit a notification:

```bash
curl -X POST http://localhost:5000/api/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "requestId": "11111111-1111-1111-1111-111111111111",
    "recipientEmail": "test@example.com",
    "subject": "Hello from JobForge",
    "body": "This is a test notification."
  }'
```

Submitting the same `requestId` again returns the existing job (`200 OK`)
instead of creating a duplicate (`202 Accepted` on first submission).

Check queue stats:

```bash
curl http://localhost:5000/api/dashboard/stats
```

## Tech stack

- ASP.NET Core 10 (REST API)
- .NET Worker Service / `BackgroundService` (async job processing)
- Entity Framework Core + Npgsql (PostgreSQL)
- MailKit (SMTP email sending)
- Mailtrap (dev SMTP sandbox)

## What's intentionally out of scope

This is a portfolio project sized to demonstrate specific reliability
concepts clearly, not to be a production-grade job queue. Deliberately not
included: a message broker (RabbitMQ/Kafka — Postgres polling is the point),
a live-updating dashboard (SignalR), multi-tenancy, and horizontal-scaling
deployment (though the atomic claim query is what *would* make that safe,
if it were added).
