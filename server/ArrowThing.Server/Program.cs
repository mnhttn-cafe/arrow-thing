using System.Security.Claims;
using ArrowThing.Server.Auth;
using ArrowThing.Server.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("Default");
if (connectionString != null)
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
}
else
{
    // Dev/test fallback: SQLite
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=arrowthing.db")
    );
}

// Auth services
builder.Services.AddSingleton<JwtHelper>();
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

app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapGet("/health", () => Results.Ok());

app.MapPost(
    "/api/auth/register",
    async (RegisterRequest request, AuthService auth) =>
    {
        var (response, status, error) = await auth.RegisterAsync(request);
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPost(
    "/api/auth/login",
    async (LoginRequest request, AuthService auth) =>
    {
        var (response, status, error) = await auth.LoginAsync(request);
        return response != null
            ? Results.Ok(response)
            : Results.Json(new { error }, statusCode: status);
    }
);

app.MapPatch(
        "/api/auth/me",
        async (UpdateDisplayNameRequest request, AuthService auth, ClaimsPrincipal user) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (response, status, error) = await auth.UpdateDisplayNameAsync(userId, request);
            return response != null
                ? Results.Ok(response)
                : Results.Json(new { error }, statusCode: status);
        }
    )
    .RequireAuthorization();

app.Run();

// Make the implicit Program class accessible to integration tests.
public partial class Program { }
