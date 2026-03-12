# Running PostgreSQL with Docker

A quick way to get a local PostgreSQL instance for development without a system-level install.

## Start the container

```bash
docker run -d \
  --name urbaser-postgres \
  -e POSTGRES_USER=urbaser \
  -e POSTGRES_PASSWORD=urbaser123 \
  -e POSTGRES_DB=urbaser \
  -p 5432:5432 \
  postgres:17
```

## Verify it's running

```bash
docker ps
```

Or connect directly via psql:

```bash
docker exec -it urbaser-postgres psql -U urbaser -d urbaser
```

## Configure the backend

**Option A — `appsettings.json`**

Update `backend/UrbaserApi/appsettings.json`:

```json
"DatabaseProvider": "postgres",
"ConnectionStrings": {
  "PostgresConnection": "Host=localhost;Port=5432;Database=urbaser;Username=urbaser;Password=urbaser123"
}
```

**Option B — environment variables (no file changes needed)**

```bash
DatabaseProvider=postgres \
ConnectionStrings__PostgresConnection="Host=localhost;Port=5432;Database=urbaser;Username=urbaser;Password=urbaser123" \
dotnet run --project backend/UrbaserApi/
```

The schema and seed data are created automatically on first run.

## Container management

```bash
# Stop (data is preserved)
docker stop urbaser-postgres

# Start again
docker start urbaser-postgres

# Destroy completely (wipes all data)
docker rm -f urbaser-postgres
```

## Re-seed from scratch

Destroy the container, recreate it, then restart the backend:

```bash
docker rm -f urbaser-postgres
docker run -d \
  --name urbaser-postgres \
  -e POSTGRES_USER=urbaser \
  -e POSTGRES_PASSWORD=urbaser123 \
  -e POSTGRES_DB=urbaser \
  -p 5432:5432 \
  postgres:17
dotnet run --project backend/UrbaserApi/
```
