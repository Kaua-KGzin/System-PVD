namespace Pdv.Backend.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, string Username, string Role);
