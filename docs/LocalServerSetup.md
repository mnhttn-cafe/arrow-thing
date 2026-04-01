# Local Server Setup

How to run the Arrow Thing server locally for development and testing.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL (local instance or Docker)
- `dotnet-ef` CLI tool (for migrations):
  ```
  dotnet tool install --global dotnet-ef
  ```

## Quick Start

Start a local PostgreSQL instance (Docker is easiest):

```bash
docker run --name arrowthing-dev \
  -e POSTGRES_PASSWORD=dev \
  -e POSTGRES_DB=arrowthing \
  -p 5432:5432 -d postgres:16-alpine
```

Set the connection string via user secrets:

```bash
cd server/ArrowThing.Server
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Database=arrowthing;Username=postgres;Password=dev"
```

Then run from the `server/` directory:

```bash
cd server
dotnet run --project ArrowThing.Server
```

The server starts at **http://localhost:5000**. Migrations are applied automatically on startup.

## Configuration

### JWT Secret

A dev-only JWT secret is pre-configured in `appsettings.Development.json`. No changes needed for local testing.

### Resend (Email)

Email features (verification codes, password reset, email change) require a [Resend](https://resend.com/) API key:

```bash
cd server/ArrowThing.Server
dotnet user-secrets set "Resend:ApiKey" "re_your_api_key_here"
```

`Resend:FromAddress` is pre-configured in `appsettings.Development.json`. Without a Resend key, registration will still succeed but no verification code email will be sent — check the server console for the error log.

To view all configured secrets:

```bash
cd server/ArrowThing.Server
dotnet user-secrets list
```

### Admin API Key

To test admin endpoints (lock/unlock account), set an admin key:

```bash
cd server/ArrowThing.Server
dotnet user-secrets set "Admin:ApiKey" "any-secret-key"
```

### Database

Both local dev and production use PostgreSQL. Set the connection string via user secrets (see Quick Start) or any other [ASP.NET Core configuration source](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration).

To reset the local database, drop and recreate it in PostgreSQL, then restart the server (migrations re-run on startup).

## Running Tests

```bash
cd server
dotnet test
```

Tests use **Testcontainers** to spin up a throwaway `postgres:16-alpine` container and a **fake email service** via `TestFactory` — they don't touch your local database or send real emails. Docker must be running.

## Unity Client → Local Server

When running in the **Unity Editor**, `ApiClient` automatically uses `http://localhost:5000`. In builds, it points to `https://api.arrow-thing.com`.

Make sure the server is running before testing account features in the editor.

## API Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/health` | No | Health check |
| POST | `/api/auth/register` | No | Create account (sends verification code) |
| POST | `/api/auth/verify-code` | No | Verify email with 6-digit code |
| POST | `/api/auth/login` | No | Log in (requires verified email) |
| GET | `/api/auth/me` | JWT | Get account info (email, display name, verified) |
| PATCH | `/api/auth/me` | JWT | Update display name |
| POST | `/api/auth/change-password` | JWT | Change password (invalidates sessions) |
| POST | `/api/auth/change-email` | JWT | Request email change (sends 6-digit code) |
| POST | `/api/auth/confirm-email-change` | JWT | Confirm email change via 6-digit code |
| POST | `/api/auth/forgot-password` | No | Request password reset code |
| POST | `/api/auth/reset-password` | No | Reset password via 6-digit code |
| POST | `/api/auth/resend-verification` | No | Resend verification code (5-min cooldown) |
| POST | `/api/admin/lock-account` | Admin key | Lock account + invalidate sessions |
| POST | `/api/admin/unlock-account` | Admin key | Unlock account + send password reset code |

### Register

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123","displayName":"Test"}'
```

Returns `{"message":"Check your email for a verification code."}` — check your inbox (or server logs if no Resend key) for the 6-digit code.

### Verify Code

```bash
curl -X POST http://localhost:5000/api/auth/verify-code \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","code":"123456"}'
```

Returns `{"token":"...","displayName":"Test","emailVerified":true}` on success.

### Login

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}'
```

Only works for verified accounts.

### Update Display Name

```bash
curl -X PATCH http://localhost:5000/api/auth/me \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"displayName":"NewName"}'
```

### Change Password

```bash
curl -X POST http://localhost:5000/api/auth/change-password \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"currentPassword":"password123","newPassword":"newpassword456"}'
```

Bumps SecurityStamp — all existing tokens are invalidated.

### Forgot Password

```bash
curl -X POST http://localhost:5000/api/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com"}'
```

Returns a generic message regardless of whether the account exists (prevents email enumeration). Sends a 6-digit reset code if the account exists.

### Reset Password

```bash
curl -X POST http://localhost:5000/api/auth/reset-password \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","code":"123456","newPassword":"newpassword456"}'
```

### Change Email

```bash
curl -X POST http://localhost:5000/api/auth/change-email \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"newEmail":"newemail@example.com","currentPassword":"password123"}'
```

Sends a 6-digit code to the new email address.

### Confirm Email Change

```bash
curl -X POST http://localhost:5000/api/auth/confirm-email-change \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"email":"newemail@example.com","code":"123456"}'
```

Returns a new JWT with the updated email. Old email receives a notification.

### Admin Lock/Unlock

```bash
curl -X POST http://localhost:5000/api/admin/lock-account \
  -H "Content-Type: application/json" \
  -H "X-Admin-Key: your-admin-key" \
  -d '{"email":"test@example.com"}'
```

## EF Core Migrations

Migrations live in `server/ArrowThing.Server/Migrations/` and are applied automatically on startup. To create a new migration after changing `AppDbContext` or models:

The connection string must point to PostgreSQL when generating migrations (not the SQLite fallback) so the snapshot reflects the correct column types. With user secrets configured, this works automatically:

```bash
cd server
dotnet ef migrations add <MigrationName> --project ArrowThing.Server
```
