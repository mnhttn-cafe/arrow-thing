using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ArrowThing.Server.Models;
using Microsoft.IdentityModel.Tokens;

namespace ArrowThing.Server.Auth;

public class JwtHelper
{
    private readonly byte[] _key;

    public JwtHelper(IConfiguration configuration)
    {
        var secret =
            configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("username", user.Username),
            new Claim("display_name", user.DisplayName),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_key),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationParameters GetValidationParameters() =>
        new()
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_key),
        };
}
