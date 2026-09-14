# Trippy V2

Travel itinerary planner demo: **.NET 10** API (Controller → Service → Repository) + **React/Vite/shadcn** frontend + **Postgres**. Deployable to Railway via the root Dockerfile.

## What's in the box

- Itinerary → Section → Item → Place domain (Italy seed data)
- REST CRUD under `/api/*`
- AI chat with **OpenAI** function calling (no LangChain)
- HITL: agent proposes mutations → user approve/deny → deterministic executor
- SSE streaming for chat tokens / pending actions
- Orval generates TanStack Query hooks from OpenAPI

## Local development

### 1. Postgres

```bash
docker compose up db -d
```

### 2. API

```bash
# from repo root — set keys in .env or environment
export DATABASE_URL=postgresql://trippy:trippy@localhost:5433/trippy
export OPENAI_API_KEY=...

dotnet run --project backend
# http://localhost:8000  (Swagger at /swagger)
```

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
# http://localhost:5173  (proxies /api → :8000)
```

### Regenerate Orval clients

With the API running:

```bash
curl -o frontend/openapi.json http://localhost:8000/swagger/v1/swagger.json
cd frontend && npm run generate:api
```

DTO classes use `[Required]` so the swagger schema keeps required fields for TypeScript.
If Orval types regress, compare against the previous `openapi.json` before committing.

## Docker / Railway

```bash
docker compose up --build
# app on http://localhost:8000
```

Railway: create a project, add a Postgres plugin, deploy this repo. Set `OPENAI_API_KEY`. `DATABASE_URL` is injected automatically. Health check: `/api/health`.

## Extending the demo

| Goal | Where |
|------|--------|
| New seed places | `backend/Data/italy.json` |
| Sample itinerary | `DbSeeder.SeedSampleItineraryAsync` |
| New REST resource | Entity (`Data/Entities`) → `AppDbContext` → `Repositories/` → `Services/` → `Controllers/` |
| New agent tool | Implement `IAgentTool` under `Services/Tools`, register in `Program.cs` |
| New mutation type | Extend `ProposeMutationTool` + `PendingActionExecutor` |

## How to enhance this project (no AI assistance)

This section is the "manual mode" walkthrough: the same edits an AI assistant would make,
spelled out so you can do them yourself.

### 1. Adding or editing DB models

The data flow is: **Entity → `AppDbContext.OnModelCreating` → EF migration → Repository → Service
→ DTO → Controller → (frontend) Orval regen**.

1. **Add/edit the entity.** Create or edit a class under `backend/Data/Entities/` (plain POCO,
   e.g. `Place.cs`, `Itinerary.cs`). Add the new property/class.
2. **Register it with EF.** In `backend/Data/AppDbContext.cs`:
   - Add a `DbSet<T>` property if it's a new entity.
   - Add/adjust the `modelBuilder.Entity<T>(e => { ... })` block — table name (snake_case, e.g.
     `ToTable("places")`), keys, indexes, relationships (`HasOne`/`HasMany`), and any special
     column mapping (the codebase uses `jsonb` + a `HasConversion` for list/JSON columns — copy
     that pattern for new JSON columns).
3. **Generate an EF migration.** From the repo root:
   ```bash
   dotnet ef migrations add <DescriptiveName> --project backend
   ```
   This writes a new file into `backend/Migrations/` plus an updated
   `AppDbContextModelSnapshot.cs`. Always review the generated migration — EF sometimes drops/
   recreates a column when it should alter it, especially for renames.
   - `dotnet-ef` is already installed as a global tool (`dotnet tool list -g` confirms
     `dotnet-ef`). If it's ever missing: `dotnet tool install --global dotnet-ef`.
4. **Apply it locally.** You don't need to run `dotnet ef database update` yourself — migrations
   run automatically on startup via `DbSeeder` (`db.Database.MigrateAsync(ct)`, called from
   `Program.cs`). Just `dotnet run --project backend` (or restart it) and the new migration
   applies to your local/dev Postgres. Use `dotnet ef database update --project backend` manually
   only if you want to apply migrations without starting the app.
5. **Update the API surface.** Add/update DTOs in `backend/DTOs/` (DTO properties use
   `[Required]` deliberately so the Swagger schema keeps fields non-optional, which keeps Orval's
   generated TypeScript types non-optional too) and mapping in `DtoMapper.cs`, then the
   `Repositories/` → `Services/` → `Controllers/` layers for any new fields/endpoints.
6. **Regen the frontend client (Orval).** Whenever you change a controller route, DTO shape, or
   add/remove an endpoint, the frontend's generated API client is now stale. Run:
   ```bash
   dotnet run --project backend            # API must be running (:8000)
   curl -o frontend/openapi.json http://localhost:8000/swagger/v1/swagger.json
   cd frontend && npm run generate:api
   ```
   This regenerates `frontend/src/api/generated/**` (TanStack Query hooks + Zod-free TS models)
   from the live OpenAPI spec. **Run this any time you touch a Controller, DTO, or route** — not
   just for DB model changes. If the generated types look wrong (e.g. a field became optional
   that shouldn't be), check the DTO for a missing `[Required]` before touching Orval config.
7. **Confirm everything builds/compiles**, in this order (fastest feedback first):
   ```bash
   dotnet build backend                    # C# compiles + EF model is valid
   cd frontend && npm run typecheck        # tsc --noEmit, catches API/type mismatches
   npm run lint                            # eslint
   npm run build                           # full production build (tsc -b && vite build)
   ```
   Also just run the app locally end-to-end (`dotnet run --project backend` + `npm run dev`) and
   exercise the changed feature in the browser — migrations and seed data issues often only show
   up at runtime, not at compile time.

### 2. Adding a new tool for the AI agent

Agent tools are plain classes that implement `IAgentTool` (`backend/Interfaces/IAgentTool.cs`) and
get discovered via DI — the OpenAI function-calling loop in `ChatAgentService` builds the tool
list purely from whatever's registered.

1. Create a new class under `backend/Services/Tools/`, e.g. `MyNewTool.cs`, implementing
   `IAgentTool`:
   - `Name` — the function name the model calls (snake_case by convention, e.g. `query_db`).
   - `Description` — be explicit about *when* the model should call it; the model only has this
     text to decide. Look at `DistanceTool`/`QueryDbTool` for the style used.
   - `ParameterSchema` — a JSON-Schema-shaped anonymous object describing the function's
     arguments (see any existing tool for the pattern).
   - `ExecuteAsync(string argumentsJson, string conversationId, CancellationToken ct)` — parse
     `argumentsJson` with `JsonDocument`, do the work (usually query `AppDbContext`), return a
     JSON string. Keep tools read-only unless they go through the pending-action/HITL flow below.
2. **Register it in `Program.cs`**, next to the other tools:
   ```csharp
   builder.Services.AddScoped<IAgentTool, MyNewTool>();
   ```
   That's the only wiring needed — `ChatAgentService` takes `IEnumerable<IAgentTool>` and
   advertises every registered tool to OpenAI automatically.
3. **If the tool needs to mutate data** (create/update/delete an itinerary, section, or item), do
   **not** write directly to the DB from the tool. Instead extend `ProposeMutationTool` (adds a new
   `entity`/`op` combination to its schema) and `PendingActionExecutor` (adds the matching case
   that actually performs the write once a human approves it). This preserves the human-in-the-
   loop approval flow — the agent should never apply a mutation itself.
4. **Mention the new tool in the system prompt** in `ChatAgentService.cs` (`SystemPrompt` constant)
   if its usage isn't obvious from the description alone — e.g. explain *when* to call it relative
   to other tools, the same way the existing prompt explains `query_db` → `estimate_travel_time` →
   `propose_itinerary_mutation` ordering.
5. Rebuild (`dotnet build backend`) and test via the chat UI — ask something that should trigger
   the tool and check the SSE `tool` events in the browser network tab / chat transcript.

### 3. When would you need to touch the deployment scripts (`Dockerfile`, `docker-compose.yml`, `railway.toml`)?

You generally **don't** need to touch these for normal feature work (new models, tools,
endpoints, UI). You *would* need to when:

- **Adding a new runtime dependency/service** — e.g. Redis, a queue, blob storage. Add a new
  service block to `docker-compose.yml` (mirror the `db` block: image, env, healthcheck, ports)
  and, for Railway, provision the equivalent managed plugin/service and wire its connection info
  into env vars the same way `DATABASE_URL` is handled in `Program.cs`.
- **Changing the .NET or Node version** — bump `mcr.microsoft.com/dotnet/sdk:10.0` /
  `mcr.microsoft.com/dotnet/aspnet:10.0` and `node:22-alpine` in the `Dockerfile`, and
  `<TargetFramework>` in `backend.csproj` together, or things will build locally and fail in
  Docker (or vice versa).
- **Adding new required environment variables** — e.g. a new third-party API key. Add it to
  `.env.example`, read it in `Program.cs`/`Options` the same way `OPENAI_API_KEY` is handled, add
  it to the `web.environment` block in `docker-compose.yml`, and set it in the Railway project
  dashboard (Railway env vars aren't defined in `railway.toml` — that file only configures the
  build/health-check, not secrets).
- **Changing how the frontend is built or served** — e.g. adding a build step, changing the output
  dir, or serving assets differently. Update the `frontend-build` stage and the
  `COPY --from=frontend-build /frontend/dist ./wwwroot` line in the `Dockerfile` accordingly.
- **Changing the listen port or health check path** — the app reads `PORT` (Railway sets it,
  Docker Compose sets it to `8080`) and Railway pings `/api/health` (`railway.toml`). If you
  rename/move the health endpoint (`Controllers/HealthController.cs`), update
  `railway.toml`'s `healthcheckPath` too, or deploys will be marked unhealthy.
- **Multi-instance/production deployment** — note that chat history is in-memory
  (`ChatAgentService`'s `ConcurrentDictionary`) and won't survive a restart or work across
  multiple instances. Scaling beyond one instance requires moving that state to Redis/DB first —
  this isn't a docker-file change per se, but it's the first thing that breaks if you add
  horizontal scaling to the deploy config.
- Whenever you touch the Dockerfile, sanity-check it locally before deploying:
  ```bash
  docker compose up --build
  # http://localhost:8000 should serve both the API and the built SPA
  ```

### 4. Other things worth knowing before editing solo

- **Snake_case over the wire.** `Program.cs` configures `JsonNamingPolicy.SnakeCaseLower` globally
  for request/response JSON (and enums). C# property names stay PascalCase in code, but Orval and
  the frontend will always see/send snake_case — don't manually rename fields to snake_case in
  DTOs, the serializer handles it.
- **DTOs are the Orval contract.** Never change the shape of a DTO without regenerating the
  frontend client (step 6 above) and checking the resulting TS types/hooks compile
  (`npm run typecheck`). Committing an `openapi.json` that's out of sync with `generated/**` is a
  common source of drift — regenerate both together and commit both.
- **Seeding is idempotent-ish, not incremental.** `DbSeeder` loads `Data/italy.json` into
  `Places` only when the table is empty, and seeds one sample itinerary similarly. Editing
  `italy.json` after the DB already has data won't retroactively update rows — wipe the `db`
  volume (`docker compose down -v` or drop the local Postgres data) if you want to re-seed from
  scratch.
- **Migrations run on every startup**, not just once — `MigrateAsync` is a no-op if there's
  nothing pending, but it does mean a bad/partial migration can block the app from starting for
  every dev and in production. Test `dotnet ef database update` locally before committing a
  migration you're unsure about.
- **HITL mutation flow.** All writes the agent makes go through `propose_itinerary_mutation` →
  a `PendingAction` row → explicit user approve/deny in the UI → `PendingActionExecutor` performs
  the real write. If a new feature needs the agent to write data, plug into this flow rather than
  bypassing it, even for "obviously safe" writes — it's the app's core UX contract.
- **Chat state is in-memory and per-process** (see above) — fine for this demo/single instance,
  but don't be surprised if conversation history resets on every `dotnet run` restart.
- **CORS origins** are env-driven (`CORS_ORIGINS`), defaulting to the Vite dev server. If you
  deploy the frontend separately from the API (instead of the bundled SPA-in-wwwroot setup), you
  must set `CORS_ORIGINS` to that origin or requests will be blocked.
- **No test suite currently exists** in this repo — there's no `.Tests` project or frontend test
  runner wired up. If you add meaningful logic, consider adding one; until then, "does it build +
  does it work when I click through it manually" is the verification bar.

## Solution layout

Single ASP.NET Core project with a simple Controller → Service → Repository pattern:

```
backend/
  Program.cs                   DI + middleware setup (single entry point)
  Controllers/                 thin HTTP endpoints, map DTO ⇄ service calls
  Services/                    business logic (CRUD, pending-action HITL flow)
    Tools/                     OpenAI function-calling tools for the chat agent
  Repositories/                EF Core data access, one per aggregate
  Interfaces/                  repository, service, and agent contracts
  DTOs/                        API request/response contracts (Orval/OpenAPI source) + mapper
  Data/                        AppDbContext, entities, DbSeeder, italy.json seed data
  Options/                     strongly-typed config (OpenAI)
frontend/                      React app (Orval + shadcn)
```
