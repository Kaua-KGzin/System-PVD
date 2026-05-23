namespace Pdv.Backend.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string Token,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string Username,
    string Role);

public sealed record RefreshTokenRequest(string RefreshToken);
