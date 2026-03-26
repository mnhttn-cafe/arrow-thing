# Email Verification, Password Reset, Email Change, Password Change & Admin Tooling

Email-based auth with 6-digit code verification, password reset, email change, password change, and admin lock/unlock tooling. Username removed — email is the sole login identifier. Registration does not reveal whether an email is already taken.

## Design

Per `OnlineRoadmap.md` Accounts section. Resend HTTP API for transactional email.

## Implementation (done)

### Server
- [x] `Models/User.cs` — Email, SecurityStamp, EmailVerifiedAt, LockedAt, VerificationCode/ExpiresAt, reset token fields, PendingEmail fields
- [x] `Auth/IEmailService.cs` — interface (5 methods: verification code, already-registered notification, password reset, email change verification, email change notification)
- [x] `Auth/EmailService.cs` — Resend HTTP API wrapper for all email types
- [x] ~~`Auth/TokenHelper.cs`~~ — removed (all flows now use 6-digit codes)
- [x] `Auth/AuthService.cs` — full auth service: register (non-revealing), login (rejects unverified), GetMe, display name update, verify code, resend verification, forgot password, reset password (code-based), change password, change email (code-based), confirm email change, lock account, unlock account
- [x] `Auth/JwtHelper.cs` — SecurityStamp claim in JWT, username claim removed
- [x] `Auth/AuthDtos.cs` — all DTOs for email-based auth (RegisterRequest, LoginRequest, VerifyCodeRequest, ResetPasswordRequest, ChangePasswordRequest, LockAccountRequest, MeResponse, etc.)
- [x] `Data/AppDbContext.cs` — unique email index, username index removed
- [x] `Program.cs` — SecurityStamp validation middleware, all auth endpoints, admin endpoints (X-Admin-Key). Pure API — no browser pages.
- [x] Migrations: AddEmailAndTokens, AddPendingEmailChange, AddSecurityStamp, AddAccountLock, RemoveUsername, ReplaceVerificationTokenWithCode, ReplaceEmailChangeTokenWithCode, ReplacePasswordResetTokenWithCode
- [x] 37 integration tests passing

### Client
- [x] `ApiClient.cs` — RegisterAsync (returns MessageResponse), VerifyCodeAsync, LoginAsync, GetMeAsync, ChangeEmailAsync, ConfirmEmailChangeAsync, ChangePasswordAsync, ForgotPasswordAsync, ResetPasswordAsync, ResendVerificationAsync
- [x] `AccountManager.cs` — full-screen account panel with 10 forms: login, register (with confirm password), verify code, forgot password, reset password (code + new password + confirm), account info (masked email), change email, confirm email code, change password, change display name. All forms clear fields on navigation.
- [x] `AccountPanel.uxml` — all forms with proper field types
- [x] `MainMenuLayoutTests.cs` — updated for all form elements

### Username Removal
- [x] Server: removed Username from User model, AppDbContext, all DTOs, JwtHelper, AuthService, tests
- [x] Client: removed register-username from UXML, renamed login-username to login-email, updated AccountManager + ApiClient
- [x] Migration: RemoveUsername

## Manual Test Cases

Tests below cover UI behavior and client-side logic not reachable by server integration tests. Server-side validation (duplicate email, wrong password, invalid input, rate limiting, admin key auth, etc.) is covered by the 37 automated integration tests.

### Registration & Verification (UI)
- [x] Register: fill form → submit → see verify code form with message "We sent a 6-digit code to {email}"
- [x] Register: mismatched confirm password → client-side error "Passwords do not match."
- [x] Verify code: enter correct code → logged in, see account info
- [x] Verify back: click "Back to login" → returns to login form

### Login (UI)
- [x] Login: valid credentials → see account info with masked email

### Account Info (UI)
- [x] Account info: displays greeting, masked email (a*****c@domain.com)
- [x] Display name change: update → greeting updates

### Change Password (UI)
- [x] Change password: valid change → success message, must log in again with new password
- [x] Change password: mismatched confirm → client-side error "Passwords do not match."

### Change Email (UI)
- [x] Change email: submit → transitions to confirm code form with message "We sent a 6-digit code to {newEmail}"
- [x] Confirm email change: enter correct code → returns to account info with updated masked email

### Password Reset (UI)
- [x] Forgot password: submit email → transitions to reset form with message "We sent a 6-digit code to {email}"
- [x] Reset password: enter code + new password → success message "Password has been reset. You can now log in."
- [x] Reset password: mismatched confirm password → client-side error "Passwords do not match."

### Session & Persistence
- [x] Logout: click → returns to login form
- [x] Token persistence: login → reload page → still logged in
