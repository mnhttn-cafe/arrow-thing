# Local Server Setup

How to run the Arrow Thing server locally for development and testing.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- `dotnet-ef` CLI tool (for migrations):
  ```
  dotnet tool install --global dotnet-ef
  ```

## Quick Start

From the `server/` directory:

```bash
cd server
dotnet run --project ArrowThing.Server
```

The server starts at **http://localhost:5000** by default.

When no `ConnectionStrings:Default` is configured (the default for local dev), the server uses **SQLite** and creates `arrowthing.db` in the project directory. Migrations are applied automatically on startup — no manual migration step is needed.

## Configuration

### JWT Secret

A dev-only JWT secret is pre-configured in `appsettings.Development.json`. No changes needed for local testing.

### Database

| Environment | Provider | Config |
|---|---|---|
| Local dev | SQLite | Automatic — no config needed |
| Production | PostgreSQL | Set `ConnectionStrings:Default` |

To reset the local database, delete `server/ArrowThing.Server/arrowthing.db` and restart.

## Running Tests

```bash
cd server
dotnet test
```

Tests use an **in-memory SQLite** database via `TestFactory` — they don't touch your local `arrowthing.db`.

## Unity Client → Local Server

When running in the **Unity Editor**, `ApiClient` automatically uses `http://localhost:5000`. In builds, it points to `https://api.arrow-thing.com`.

Make sure the server is running before testing account features (register, login, display name) in the editor.

## API Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/health` | No | Health check |
| POST | `/api/auth/register` | No | Create account |
| POST | `/api/auth/login` | No | Log in |
| PATCH | `/api/auth/me` | JWT | Update display name |

### Register

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"password123","displayName":"Test"}'
```

### Login

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"password123"}'
```

### Update Display Name

```bash
curl -X PATCH http://localhost:5000/api/auth/me \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"displayName":"NewName"}'
```

## EF Core Migrations

Migrations live in `server/ArrowThing.Server/Migrations/` and are applied automatically on startup. To create a new migration after changing `AppDbContext` or models:

```bash
cd server
dotnet ef migrations add <MigrationName> --project ArrowThing.Server
```
