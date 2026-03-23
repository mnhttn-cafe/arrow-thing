namespace ArrowThing.Server.Auth;

public record RegisterRequest(string Username, string Password, string DisplayName);

public record LoginRequest(string Username, string Password);

public record UpdateDisplayNameRequest(string DisplayName);

public record AuthResponse(string Token, string DisplayName);

public record DisplayNameResponse(string DisplayName);
