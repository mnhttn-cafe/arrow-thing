using System.Text.RegularExpressions;
using ArrowThing.Server.Data;
using ArrowThing.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ArrowThing.Server.Auth;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;

    public AuthService(AppDbContext db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<(AuthResponse? Response, int StatusCode, string? Error)> RegisterAsync(
        RegisterRequest request
    )
    {
        // Validate username
        if (
            string.IsNullOrWhiteSpace(request.Username)
            || request.Username.Length < 3
            || request.Username.Length > 20
        )
            return (null, 400, "Username must be 3-20 characters.");

        if (!Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]+$"))
            return (null, 400, "Username must contain only letters, numbers, and underscores.");

        // Validate display name
        if (
            string.IsNullOrWhiteSpace(request.DisplayName)
            || request.DisplayName.Length < 2
            || request.DisplayName.Length > 24
        )
            return (null, 400, "Display name must be 2-24 characters.");

        // Validate password
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return (null, 400, "Password must be at least 8 characters.");

        var normalizedUsername = request.Username.ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Username == normalizedUsername))
            return (null, 409, "Username is already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = normalizedUsername,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);
        return (new AuthResponse(token, user.DisplayName), 200, null);
    }

    public async Task<(AuthResponse? Response, int StatusCode, string? Error)> LoginAsync(
        LoginRequest request
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
        )
            return (null, 400, "Username and password are required.");

        var normalizedUsername = request.Username.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername);

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return (null, 401, "Invalid username or password.");

        var token = _jwt.GenerateToken(user);
        return (new AuthResponse(token, user.DisplayName), 200, null);
    }

    public async Task<(
        DisplayNameResponse? Response,
        int StatusCode,
        string? Error
    )> UpdateDisplayNameAsync(Guid userId, UpdateDisplayNameRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(request.DisplayName)
            || request.DisplayName.Length < 2
            || request.DisplayName.Length > 24
        )
            return (null, 400, "Display name must be 2-24 characters.");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return (null, 401, "User not found.");

        user.DisplayName = request.DisplayName.Trim();
        await _db.SaveChangesAsync();

        return (new DisplayNameResponse(user.DisplayName), 200, null);
    }
}
