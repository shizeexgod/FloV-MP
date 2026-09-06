# FloV:MP SaaS Ecosystem & Platform Architecture Blueprint
**Version:** 2.0.0 (Production Roadmap)  
**Author:** FloV:MP Platform & Multiplayer Architecture Team  
**Scope:** Complete technical ecosystem for FloV:MP as an enterprise multiplayer platform for GTA V.

---

## Executive Summary & Philosophy

FloV:MP is designed not merely as an alternative GTA V multiplayer runtime, but as a complete developer-first cloud ecosystem — marrying the developer experience of **Railway/Vercel**, the telemetry/edge routing of **Cloudflare**, the server orchestration of **txAdmin**, and the high-performance raw networking of **alt:V / RAGE:MP**.

### The Golden Rule of Platform Boundaries
```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            FLOV:MP ECOSYSTEM                                │
├─────────────────────────────────────────────────────────────────────────────┤
│  1. CLOUD CONTROL PLANE (SaaS Portal, Projects, Billing, Telemetry, AI)     │
│  2. PROJECT GATEWAY & EDGE (Manifest CDN, Dynamic Server Router, API)       │
│  3. SERVER AGENT & RUNTIME (CoreCLR .NET 8, FloV:Shield AntiCheat, Sync)    │
│  4. CLIENT LAUNCHER & RUNTIME (Native b3307 Loader, Direct Connect, NUI)   │
└─────────────────────────────────────────────────────────────────────────────┘
```

The game servers and game modes (whether "Держава RP", "Florida V", or any independent community) are **tenants** inside this ecosystem. The engine is 100% agnostic to lore, geography, or gamemode rules.

---

## Part I: Strategic Answers to the 15 Core Questions

### Q1: What backend architecture should be implemented first?
**Answer:** A **Modular Monolith with an Out-of-Band Telemetry Pipeline**.
Starting with distributed microservices for day one is an anti-pattern that slows feature velocity. Instead, use a single, hardened Next.js 14 / Node.js or .NET 8 API backend with clean module boundaries:
1. `Auth & Accounts` (JWT + TOTP 2FA)
2. `Project Registry` (Multi-tenant Account -> Project -> Servers hierarchy)
3. `Licensing & Cryptography` (Ed25519/HMAC-SHA256 asymmetric ticket signing)
4. `Server Agent Gateway` (Lightweight HTTP/WebSocket ingest for heartbeats and remote management)
5. `Asset Packager & CDN Distribution` (Chunked SHA-256 manifests)

### Q2: What services should exist from day one?
**Answer:** Five essential services:
1. **Project & License Master Service**: Validates Project Lifetime/Subscription keys and signs offline lease tokens.
2. **Server Heartbeat & Status Collector**: Ingests 15s ticks from servers without dropping game frames.
3. **Dynamic Launcher Gateway**: Resolves `project-slug` to active endpoints, eliminating hardcoded server IPs in client builds.
4. **FastDL Asset Distribution Service**: Generates differential CDN manifests and serves zipped resource chunks.
5. **Project Owner Web Dashboard**: Unified pane for managing projects, environments, team members, and telemetry.

### Q3: What should be inside Multiplayer Core (`FloVMP.Core`)?
**Answer:** Only low-level engine abstractions and security invariants:
- Network packet encoding/decoding & compression.
- High-precision entity streaming and delta interpolation.
- AntiCheat security invariants (**FloV:Shield**): Teleport bounds, Speedhack verification, Weapon inventory whitelisting.
- Asynchronous Server Agent Worker (`TelemetryReporter` / `RemoteManagementAgent`) running in a background thread without touching the Alt:V tick loop.
- Abstract database connector interfaces (`IAccountStore`, `IAuditStore`) with MariaDB/JSON fallback.
- **ZERO** hardcoded map names, Russian/English lore, or gamemode rules.

### Q4: What should be inside Dashboard Backend?
**Answer:**
- Multi-tenancy RBAC (Owner, Developer, Moderator, QA).
- Project management (Production, Staging, Dev server slots).
- Remote Server Management (txAdmin-like: Start, Stop, Restart, Live Console stdout stream, Resource reload).
- Realtime telemetry charts (Chart.js / Tailwind UI) showing Tickrate, FPS, Memory, and Player curves.
- Billing & Invoicing engine with automated license generation and webhook dispatch.
- FastDL build packer triggers and launcher customizer.
- AI Assistant context engine (feeding documentation and crash dumps into LLM diagnostics).

### Q5: What APIs should be implemented first?
**Answer:**
1. `POST /api/v1/agent/heartbeat`: Game server reporting health & players.
2. `POST /api/v1/license/verify`: Cryptographic lease exchange (Server -> Cloud).
3. `GET /api/v1/projects/:slug/launcher/config`: Launcher fetching dynamic server list and colors.
4. `GET /api/v1/projects/:slug/servers/:env/manifest`: FastDL delta sync manifest.
5. `POST /api/v1/agent/commands/poll` & `POST /api/v1/agent/commands/result`: txAdmin-style cloud control commands.
6. `GET /api/v1/projects/:slug/stats/public`: Public widget API for Discord bots and project landing pages.

### Q6: What data should every server report?
**Answer:** Every server reports a compact 15-second JSON/MessagePack heartbeat payload:
```json
{
  "projectKey": "FLV-PROJ-XXXX-XXXX",
  "serverUid": "srv_prod_01",
  "environment": "production",
  "version": "1.0.4",
  "gitCommit": "0522c74",
  "playersCount": 142,
  "maxPlayers": 1500,
  "tickRate": 60.0,
  "fps": 60.0,
  "memoryMb": 384,
  "cpuPercent": 14.2,
  "uptimeSeconds": 86400,
  "activeResources": ["flovmp-client", "gamemode", "vehicles_pack"],
  "crashCount": 0
}
```

### Q7: How should project licensing work?
**Answer:** **License belongs to the Project, NEVER to an individual Server.**
- A single project license grants an entitlement pool: e.g. *Enterprise Tier = 1500 concurrent players, 1 Production Server, up to 5 Development/Test instances*.
- Dev/Test instances have player caps (e.g. 32-64 slots) and local IP allowance (`127.0.0.1`, LAN, or staging IPs) without consuming production slots.
- Lifetime licenses give perpetual engine access with optional recurring support/cloud-tier add-ons.

### Q8: Should licensing be local or cloud-based?
**Answer:** **Hybrid Asymmetric Lease with Offline Grace Period.**
- True cloud-only licensing is catastrophic: if the cloud portal or Cloudflare drops for 10 minutes, hundreds of active game servers with thousands of players would crash.
- Solution: When the game server boots, it contacts the FloV:MP Cloud API and obtains a **signed Ed25519 / HMAC cryptographic lease** valid for **72 hours**.
- The server caches this lease locally (`flovmp-data/license.lease`).
- The server re-validates the lease every 12 hours in the background. If the cloud is unreachable, the server continues running uninterrupted during the 72-hour grace period while logging a warning.

### Q9: How should updates be distributed?
**Answer:** **Differential Content Addressing via FastDL CDN.**
1. Assets are divided into chunks and indexed in `manifest.json` by SHA-256 hash.
2. The launcher downloads `manifest.json` from the CDN.
3. The launcher compares local files with the remote hashes.
4. Only missing or altered chunks are downloaded (with Gzip / Brotli compression and parallel multi-part HTTP/2 streaming).
5. The launcher verifies hashes before unlocking the "Play" button.

### Q10: How should launcher integration work?
**Answer:** **Zero-Hardcoding Project Gateway.**
- The launcher binary is identical across all projects. It reads a dynamic metadata configuration:
  - Either compiled with a 1 KB embedded configuration payload: `{ "projectSlug": "florida-v", "gatewayUrl": "https://api.flovmp.ru" }`.
  - Or passed via CLI parameter: `FloVMP.exe --project=florida-v`.
- The launcher fetches the latest UI colors, logo, news feed, active servers, and CDN manifest from the FloV:MP Cloud Gateway at startup.

### Q11: How should analytics be collected?
**Answer:** **Dual-Speed Analytics Pipeline.**
1. **Fast-path (Operational / Realtime)**: Ingested via `/api/v1/agent/heartbeat` into Redis / memory ring buffer (stores last 60 minutes in 15-second granularity for live graphs).
2. **Slow-path (Historical / Aggregated)**: Background worker rolls up the 15-second data into hourly and daily summary records (`portal_analytics_hourly`, `portal_analytics_daily`) in MariaDB / TimescaleDB.

### Q12: What technical mistakes should be avoided now?
**Answer:**
1. **Never block the game thread with network I/O**: Synchronous HTTP calls inside Alt:V tick loop cause game stutters and desync for all players.
2. **Never bind licenses to dynamic server IPs**: Cloud servers and home developers frequently change public IPs.
3. **Never couple gamemode code to engine binaries**: Game logic must remain isolated in plugins/resources.
4. **Never rely on centralized authentication for player gameplay**: If the FloV:MP master auth drops, players should still be able to play on their favorite servers via server-managed auth.

### Q13: What architecture decisions today will save massive rewrites later?
**Answer:**
1. Establishing the `Account -> Project -> Servers` database hierarchy from Day 1.
2. Using signed lease tokens instead of continuous heartbeats for DRM verification.
3. Standardizing all agent payloads on strict JSON Schema / TypeScript interfaces.
4. Implementing the remote management protocol as a queue of idempotent commands.

### Q14: How would you design the ecosystem for 100+ projects?
**Answer:**
- Primary MariaDB/PostgreSQL with read replicas for public stats.
- Redis Cluster for pub/sub heartbeat aggregation and cache.
- Cloudflare R2 / AWS S3 with CDN caching for all FastDL assets.
- Dockerized Next.js web application running behind Traefik/Nginx reverse proxy.

### Q15: What architecture would you choose for 1000+ projects?
**Answer:**
- Distributed Anycast Edge Gateways for launcher requests.
- Apache Kafka or RabbitMQ event queue for ingesting millions of server telemetry events per minute into ClickHouse.
- Regional edge clusters for CDN file distribution.
- Kubernetes / Nomad cluster for automated deployment of managed dedicated server instances.

---

## Part II: Complete 13-Section Architecture Specification

```
                          ┌───────────────────────────┐
                          │   Account (Project Owner) │
                          └─────────────┬─────────────┘
                                        │ 1..N
                          ┌─────────────▼─────────────┐
                          │          PROJECT          │
                          │ - License Key (Lifetime)  │
                          │ - API Key (flv_live_...)  │
                          │ - Slot Entitlement (1500) │
                          └─────────────┬─────────────┘
                                        │ 1..N
            ┌───────────────────────────┼───────────────────────────┐
            │                           │                           │
  ┌─────────▼─────────┐       ┌─────────▼─────────┐       ┌─────────▼─────────┐
  │ Production Server │       │ Development Server│       │   Staging Server  │
  │ IP: 188.127.x.x   │       │ IP: 127.0.0.1     │       │ IP: 192.168.x.x   │
  │ Slots: 1500       │       │ Slots: 64         │       │ Slots: 64         │
  └───────────────────┘       └───────────────────┘       └───────────────────┘
```

### 1. Product Architecture
- **Multi-Tenant Hierarchy**: Account owns Projects; Projects own Licenses and Servers.
- **Role-Based Access Control (RBAC)**:
  - `Owner`: Billing, License management, Transfer project.
  - `Admin`: Full server control, FastDL publish, Team invites.
  - `Developer`: Server restart, Live console, Test server deployment.
  - `Analyst`: View telemetry, player retention, audit logs.

### 2. Service Architecture
```
[Client Launcher] ───────┐
                         ▼
[Game Servers] ───> [FloV:MP Edge Gateway] ───> [Backend API] ───> [MariaDB / Redis]
                         ▲                            │
[Project Owners] ────────┴─── [Web Dashboard] ────────┘
```
- **Edge Gateway**: TLS termination, rate limiting, request routing.
- **Core SaaS Backend**: Project management, billing, team access, API key verification.
- **Server Agent (txAdmin Style)**: In-process C# daemon in `FloVMP.Core` communicating with SaaS over HTTPS/WebSocket.

### 3. Backend Architecture
- **Framework**: Next.js 14 App Router + Node.js/C# Micro-agents.
- **Data Persistence**: Dual-layer architecture:
  - Primary: MariaDB 10.6+ relational storage with connection pooling.
  - Resilient Fallback: Local ACID JSON file storage for zero-dependency local development and network fault tolerance.

### 4. Multiplayer Architecture
- **Runtime**: alt:V b3307 unhooked binary engine.
- **Gamemode Host**: CoreCLR .NET 8 (`FloVMP.Core.dll` + `FloVMP.Gamemode.dll`).
- **Security**: In-engine speed, teleport, and inventory anti-cheat running at 60 Hz tickrate.

### 5. Dashboard Architecture
- **Overview**: Active player counts across all environments, server uptime, license status.
- **Server Hub**: Live console stream, start/stop/restart triggers, resource inspector.
- **Analytics View**: Peak CCU, 24h retention curves, CPU/RAM histograms.
- **Launcher Builder**: Interactive color theme picker, logo upload, single-click `.exe` installer generator.
- **AI Troubleshooter**: Integrated diagnostics assistant analyzing crash logs.

### 6. Database Architecture (`sql/portal_schema.sql` Evolution)
- `portal_users`: User credentials, 2FA secrets, roles.
- `portal_projects`: Project metadata, slug, owner ID, project API key.
- `portal_licenses`: Plan type (Indie/Business/Enterprise/Lifetime), slot entitlement, expiration.
- `portal_servers`: Specific instances (environment, IP, port, current status, agent token).
- `portal_telemetry`: Raw time-series health metrics.
- `portal_analytics_daily`: Compact rolled-up daily metrics.
- `portal_agent_commands`: Remote control command queue (restart, stop, kick, eval).

### 7. API Architecture
- `/api/v1/projects`: Project CRUD.
- `/api/v1/projects/:id/servers`: Server management under project.
- `/api/v1/agent/heartbeat`: Server health reporting.
- `/api/v1/agent/command`: Dispatch and poll remote management commands.
- `/api/v1/launcher/:slug/bootstrap`: Dynamic launcher initialization.
- `/api/v1/ai/troubleshoot`: Crash dump & error diagnostics.

### 8. Analytics Architecture
- **Metrics Collected**:
  - `CCU` (Concurrent Users): Realtime, 1h avg, 24h peak.
  - `Performance`: Tickrate (Hz), Server FPS, CoreCLR RAM (MB), CPU (%).
  - `Stability`: Uptime seconds, unhandled exceptions count.
- **Storage Strategy**: Raw data retained for 7 days; aggregated daily metrics retained indefinitely.

### 9. Licensing Architecture
- **Key Format**: `FLV-PROJ-[XXXX]-[XXXX]-[XXXX]`
- **Lease Mechanism**:
  ```
  Server Startup -> POST /api/v1/license/verify
  Response: Signed Lease Token { ProjectId, Plan, MaxSlots, ValidUntil, Signature }
  Local Cache: flovmp-data/license.lease (72h validity)
  ```

### 10. Scaling Strategy
- **10 Projects**: Single server, MariaDB + Next.js.
- **100 Projects**: Managed MariaDB with read replicas + Redis cache + Cloudflare CDN.
- **1000 Projects**: Micro-gateways on AWS/Hetzner Anycast, Kafka ingest pipeline, ClickHouse analytics.

### 11. Recommended Tech Stack
- **Game Engine**: Unhooked alt:V b3307 + CoreCLR .NET 8 (C#).
- **Launcher**: Electron + C# Native Bridge (`FloVMP.Launcher.Native`).
- **SaaS Portal & API**: Next.js 14, TypeScript, Tailwind CSS, Prisma/mysql2.
- **Database**: MariaDB 10.6+ / Redis.
- **CDN**: Nginx FastDL + Cloudflare Edge.

### 12. MVP Roadmap (Current Focus)
- [x] Unhooked autonomous alt:V engine verified.
- [x] Decoupled lore-agnostic `FloVMP.Core` with 137 passing tests.
- [x] Basic Next.js SaaS portal with billing & telemetry.
- [ ] Implement `portal_projects` & `portal_servers` schema expansion.
- [ ] Implement txAdmin-style cloud control commands (Start, Stop, Restart).
- [ ] Build AI Troubleshooter assistant endpoint for crash logs.

### 13. Long-Term Roadmap
- Self-healing server agent with automatic crash restart.
- Web-based in-game resource manager and live entity inspector.
- Turnkey 1-click cloud server deployment (deploy GTA V RP server on Hetzner/AWS in 60 seconds).
- Official FloV:MP Marketplace for scripts, maps, and vehicles.
