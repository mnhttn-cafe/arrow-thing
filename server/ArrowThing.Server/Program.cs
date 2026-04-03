using System.Security.Claims;
using ArrowThing.Server.Auth;
using ArrowThing.Server.Data;
using ArrowThing.Server.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

static bool VerifyAdminKey(IConfiguration config, HttpContext ctx)
{
    var adminKey = config["Admin:ApiKey"];
    if (string.IsNullOrEmpty(adminKey))
        return false;

    var provided = ctx.Request.Headers["X-Admin-Key"].FirstOrDefault() ?? "";
    return PasswordHasher.FixedTimeEquals(provided, adminKey);
}

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(
    (context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ArrowThing")
            .WriteTo.Console();

        var lokiUrl = context.Configuration["Loki:Url"];
        if (!string.IsNullOrEmpty(lokiUrl))
        {
            configuration.WriteTo.GrafanaLoki(
                lokiUrl,
                labels: new List<LokiLabel>
                {
                    new LokiLabel { Key = "app", Value = "arrow-thing" },
                }
            );
        }
    }
);

// OpenTelemetry metrics
builder
    .Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddPrometheusExporter();
    });

// Database
// connectionString may be null in test environments where TestFactory replaces the
// DbContext registration entirely. In production, docker-compose always provides it.
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// HTTP client for Resend
builder.Services.AddHttpClient();

// Auth services
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

// Configure JWT validation after all config sources are registered
builder
    .Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtHelper>(
        (options, jwt) =>
        {
            options.TokenValidationParameters = jwt.GetValidationParameters();
        }
    );

var app = builder.Build();

// Apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// Validate security stamp on authenticated requests — rejects tokens issued before a stamp change
app.Use(
    async (context, next) =>
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var stampClaim = user.FindFirstValue("security_stamp");
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (stampClaim != null && userIdClaim != null)
            {
                var db = context.RequestServices.GetRequiredService<AppDbContext>();
                var dbUser = await db.Users.FindAsync(Guid.Parse(userIdClaim));

                if (dbUser == null || dbUser.SecurityStamp != stampClaim)
                {
                    var audit = context.RequestServices.GetRequiredService<AuditLogService>();
                    await audit.LogAsync(
                        AuditEvent.SessionInvalidated,
                        Guid.Parse(userIdClaim),
                        dbUser?.Email,
                        GetClientIp(context)
                    );

                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(
                        new { error = "Session invalidated. Please log in again." }
                    );
                    return;
                }
            }
        }

        await next();
    }
);

// Endpoints
app.MapGet("/health", () => Results.Ok());

app.MapPost(
    "/api/auth/register",
    async (RegisterRequest request, AuthService auth, HttpContext ctx) =>
    {
        var (response, status, error) = await auth.RegisterAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
    "/api/auth/login",
    async (LoginRequest request, AuthService auth, HttpContext ctx) =>
    {
        var (response, status, error) = await auth.LoginAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapGet(
        "/api/auth/me",
        async (AuthService auth, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (response, status, error) = await auth.GetMeAsync(userId);
            return response != null
                ? Results.Ok(response)
                : Results.Json(new { error }, statusCode: status);
        }
    )
    .RequireAuthorization();

app.MapPatch(
        "/api/auth/me",
        async (
            UpdateDisplayNameRequest request,
            AuthService auth,
            ClaimsPrincipal user,
            HttpContext ctx
        ) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (response, status, error) = await auth.UpdateDisplayNameAsync(
                userId,
                request,
                GetClientIp(ctx)
            );
            return response != null
                ? Results.Ok(response)
                : Results.Json(new { error }, statusCode: status);
        }
    )
    .RequireAuthorization();

app.MapPost(
    "/api/auth/verify-code",
    async (VerifyCodeRequest request, AuthService auth, HttpContext ctx) =>
    {
        var (response, status, error) = await auth.VerifyCodeAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
    "/api/auth/resend-verification",
    async (ResendVerificationRequest request, AuthService auth, HttpContext ctx) =>
    {
        var (response, status, error) = await auth.ResendVerificationAsync(
            request,
            GetClientIp(ctx)
        );
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
    "/api/auth/forgot-password",
    async (ForgotPasswordRequest request, AuthService auth, HttpContext ctx) =>
    {
        var (response, status, error) = await auth.ForgotPasswordAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
    "/api/auth/reset-password",
    async (ResetPasswordRequest request, AuthService auth, HttpContext ctx) =>
    {
        var (response, status, error) = await auth.ResetPasswordAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
        "/api/auth/change-email",
        async (
            ChangeEmailRequest request,
            AuthService auth,
            ClaimsPrincipal user,
            HttpContext ctx
        ) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (response, status, error) = await auth.ChangeEmailAsync(
                userId,
                request,
                GetClientIp(ctx)
            );
            return response != null
                ? Results.Ok(response)
                : Results.Json(new { error }, statusCode: status);
        }
    )
    .RequireAuthorization();

app.MapPost(
        "/api/auth/change-password",
        async (
            ChangePasswordRequest request,
            AuthService auth,
            ClaimsPrincipal user,
            HttpContext ctx
        ) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (response, status, error) = await auth.ChangePasswordAsync(
                userId,
                request,
                GetClientIp(ctx)
            );
            return response != null
                ? Results.Ok(response)
                : Results.Json(new { error }, statusCode: status);
        }
    )
    .RequireAuthorization();

app.MapPost(
        "/api/auth/confirm-email-change",
        async (
            ConfirmEmailChangeRequest request,
            AuthService auth,
            ClaimsPrincipal user,
            HttpContext ctx
        ) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (response, status, error) = await auth.ConfirmEmailChangeAsync(
                userId,
                request,
                GetClientIp(ctx)
            );
            return response != null
                ? Results.Ok(response)
                : Results.Json(new { error }, statusCode: status);
        }
    )
    .RequireAuthorization();

// Admin endpoints (protected by API key, not JWT)
app.MapPost(
    "/api/admin/lock-account",
    async (LockAccountRequest request, AuthService auth, IConfiguration config, HttpContext ctx) =>
    {
        if (!VerifyAdminKey(config, ctx))
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        var (response, status, error) = await auth.LockAccountAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
    "/api/admin/unlock-account",
    async (LockAccountRequest request, AuthService auth, IConfiguration config, HttpContext ctx) =>
    {
        if (!VerifyAdminKey(config, ctx))
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        var (response, status, error) = await auth.UnlockAccountAsync(request, GetClientIp(ctx));
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPrometheusScrapingEndpoint("/metrics");

app.Run();

static string? GetClientIp(HttpContext ctx)
{
    // Nginx forwards the real client IP from Cloudflare's CF-Connecting-IP header
    // as X-Forwarded-For. Fall back to connection remote IP for direct access.
    return ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()
        ?? ctx.Connection.RemoteIpAddress?.ToString();
}

// Make the implicit Program class accessible to integration tests.
public partial class Program { }
