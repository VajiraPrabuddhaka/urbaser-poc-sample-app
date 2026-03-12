# Smart Waste Management Dashboard

A .NET 10 sample application demonstrating [OpenChoreo](https://choreo.dev) observability capabilities through a simulated Smart Waste Management system. The application generates rich telemetry — structured logs, distributed traces, and custom metrics — making it an ideal showcase for connecting to an observability backend.

## Overview

The system simulates a city's waste management operations: IoT sensors on waste bins reporting fill levels, collection trucks on routes, and an operations dashboard for monitoring and scheduling. Background workers continuously generate realistic telemetry data without any manual interaction.

```
┌──────────────────────────┐         ┌──────────────────────────────────┐
│  Frontend                │  HTTP   │  Backend                         │
│  Blazor WebAssembly      │ ──────► │  .NET 10 Web API                 │
│                          │         │                                  │
│  • Dashboard             │         │  Controllers/                    │
│  • Bins (fill levels)    │         │   ├─ BinsController              │
│  • Trucks (fleet status) │         │   ├─ TrucksController            │
│  • Collections           │         │   ├─ CollectionsController       │
│  • Alerts                │         │   ├─ AlertsController            │
│                          │         │   └─ DashboardController         │
│  localhost:5231          │         │                                  │
│                          │         │  Background Workers/             │
│                          │         │   ├─ SensorSimulatorService      │
│                          │         │   └─ AlertMonitorService         │
│                          │         │                                  │
│                          │         │  Observability/                  │
│                          │         │   ├─ Serilog (structured logs)   │
│                          │         │   ├─ OpenTelemetry (traces)      │
│                          │         │   └─ Custom Metrics (OTLP)       │
│                          │         │                                  │
│                          │         │  SQLite + EF Core                │
│                          │         │  localhost:5230                  │
└──────────────────────────┘         └──────────────────────────────────┘
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Verify your installation:

```bash
dotnet --version
# Should print 10.x.x
```

## Running the Application

Both the backend and frontend are separate processes. Open two terminals:

**Terminal 1 — Backend API**
```bash
dotnet run --project backend/UrbaserApi/
```
The API starts at `http://localhost:5230`. On first run it creates `urbaser.db` and seeds sample data (12 bins, 4 trucks, collections, alerts, 288 fill-level readings).

**Terminal 2 — Frontend**
```bash
dotnet run --project frontend/UrbaserDashboard/
```
The dashboard opens at `http://localhost:5231`.

**Build only (no run)**
```bash
dotnet build
```

## API Endpoints

### Bins — `/api/bins`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/bins` | List all bins. Optional: `?type=General\|Recycling\|Organic\|Glass\|Paper`, `?status=Active\|Inactive\|Maintenance` |
| GET | `/api/bins/{id}` | Bin detail with last 24h fill-level readings |
| GET | `/api/bins/{id}/readings` | Fill-level history. Optional: `?hours=24` |

### Trucks — `/api/trucks`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/trucks` | List all trucks. Optional: `?status=Available\|OnRoute\|Collecting\|Returning\|OutOfService` |
| GET | `/api/trucks/{id}` | Truck detail with recent collections |
| PUT | `/api/trucks/{id}/location` | Update GPS location `{ "latitude": 0.0, "longitude": 0.0 }` |

### Collections — `/api/collections`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/collections` | List collections. Optional: `?status=Scheduled\|InProgress\|Completed\|Cancelled` |
| GET | `/api/collections/{id}` | Collection detail |
| POST | `/api/collections` | Schedule: `{ "binId": 1, "truckId": 1, "scheduledAt": "...", "notes": "..." }` |
| PUT | `/api/collections/{id}/start` | Transition Scheduled → InProgress |
| PUT | `/api/collections/{id}/complete` | Transition InProgress → Completed. Body: `{ "fillLevelAtCollection": 87.5 }` |
| PUT | `/api/collections/{id}/cancel` | Cancel a scheduled or in-progress collection |

### Alerts — `/api/alerts`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/alerts` | List alerts. Optional: `?acknowledged=true\|false`, `?severity=Info\|Warning\|Critical` |
| GET | `/api/alerts/{id}` | Alert detail |
| PUT | `/api/alerts/{id}/acknowledge` | Acknowledge: `{ "acknowledgedBy": "operator" }` |
| PUT | `/api/alerts/acknowledge-all` | Acknowledge all open alerts |

### Dashboard — `/api/dashboard`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard` | Aggregate statistics (bin counts, alert counts, fill levels, collection rates) |

### Simulation — `/api/simulation`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/simulation/status` | Current chaos/accelerated flags |
| PUT | `/api/simulation/chaos` | Toggle chaos mode |
| PUT | `/api/simulation/accelerate` | Toggle accelerated mode |
| POST | `/api/simulation/reset` | Reset bins to 20–60% fill, clear all open alerts, disable both modes |

### OpenAPI / Swagger

The API schema is available at `http://localhost:5230/openapi` when the backend is running.

## Observability Features

### Structured Logging (Serilog)

All backend logs are emitted as structured JSON to the console. Every log entry includes machine name, thread ID, and trace/span ID for correlation with distributed traces.

Log levels by component:
- Application logic: `Information`
- EF Core / ASP.NET Core framework: `Warning` (to reduce noise)
- Sensor timeouts, alert generation, slow requests: `Warning`
- 5xx errors: `Error`

Each HTTP request is logged by `RequestLoggingMiddleware` with method, path, status code, duration (ms), and a correlation ID that is also echoed back in the `X-Correlation-Id` response header.

### Distributed Tracing (OpenTelemetry)

Traces are exported via OTLP (gRPC) to `http://localhost:4317` and also printed to the console. The service name is `urbaser-api`.

**Custom spans** (activity source: `UrbaserApi`):

| Span name | Tags |
|-----------|------|
| `sensor.simulate-cycle` | `bin.count` |
| `sensor.read` | `bin.id`, `fill_level`, `battery_level` |
| `bin.get-detail` | `bin.id`, `bin.fill_level` |
| `collection.schedule` | `bin.id`, `truck.id`, `scheduled_at` |
| `collection.complete` | `collection.id`, `duration_minutes` |
| `alert.generate` | `alert.type`, `alert.severity`, `bin.id` |

In addition, ASP.NET Core, HTTP client, and EF Core are auto-instrumented.

### Custom Metrics (OpenTelemetry)

Metrics are exported via OTLP to `http://localhost:4317` and printed to the console. The meter name is `UrbaserApi`.

| Instrument | Type | Description |
|------------|------|-------------|
| `urbaser.collections.scheduled` | Counter | Collections scheduled via API |
| `urbaser.collections.completed` | Counter | Collections completed via API |
| `urbaser.collection.duration_minutes` | Histogram | Time from start to completion |
| `urbaser.bin.fill_level_at_collection` | Histogram | Fill level recorded at completion |
| `urbaser.alerts.generated` | Counter | Alerts created by monitor service (tagged by type + severity) |
| `urbaser.alerts.acknowledged` | Counter | Alerts acknowledged via API |
| `urbaser.bins.average_fill_level` | Observable Gauge | Live average fill across active bins |
| `urbaser.trucks.active` | Observable Gauge | Live count of non-out-of-service trucks |

Runtime metrics (GC, thread pool, memory) are also exported via `OpenTelemetry.Instrumentation.Runtime`.

## Simulation Controls

The simulation controls let you generate interesting telemetry without manual data entry.

### Chaos Mode
Activates degraded conditions:
- **50% sensor timeout rate** — half of all bin sensor readings are skipped, generating `SensorTimeout` alerts
- **Random delays** — 20% of API requests receive a 100–2000ms artificial delay

Toggle via the Dashboard page or `PUT /api/simulation/chaos`.

### Accelerated Mode
Speeds up the sensor simulation cycle from 10 seconds to 2 seconds, producing fill-level data 5× faster and triggering alerts sooner.

Toggle via the Dashboard page or `PUT /api/simulation/accelerate`.

### Reset
Resets all bin fill levels to 20–60%, acknowledges all open alerts, and disables both modes. Useful for restarting a demo scenario.

## Connecting to OpenChoreo

To send traces and metrics to an OpenChoreo-connected observability backend (e.g. Jaeger, Prometheus, Grafana), point the OTLP exporter at your collector:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://<your-collector>:4317
dotnet run --project backend/UrbaserApi/
```

Or set it in `appsettings.json` / `appsettings.Development.json`:

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://<your-collector>:4317"
}
```

The backend exports both **traces** and **metrics** over gRPC OTLP. If no collector is reachable the app still runs — export failures are silent, and the console exporter continues to work locally.

## Project Structure

```
UrbaserWasteManagement.slnx         Solution file
backend/
  UrbaserApi/
    Controllers/                    REST API controllers
    Data/                           EF Core DbContext + seed data
    DTOs/                           API request/response records
    Middleware/                     RequestLoggingMiddleware
    Models/                         EF Core entities + enums
    Services/                       Background workers + simulation state
    Telemetry/                      Custom metrics + activity source
    Program.cs                      Application entry point
    appsettings.json                Serilog config, connection string
frontend/
  UrbaserDashboard/
    Models/                         Client-side DTO records
    Services/                       HttpClient wrappers for each API area
    Pages/                          Blazor pages (Home, Bins, BinDetail, Trucks, Collections, Alerts)
    Layout/                         MainLayout, NavMenu
    wwwroot/css/app.css             All application styles
    Program.cs                      HttpClient config + DI registrations
```

## Database

SQLite is used for simplicity — no setup required. The database file (`backend/UrbaserApi/urbaser.db`) is created automatically on first run using `EnsureCreated()`. There are no EF Core migrations.

To reset to a clean state with fresh seed data, delete the database file and restart the backend:

```bash
rm backend/UrbaserApi/urbaser.db
dotnet run --project backend/UrbaserApi/
```
