using Pdv.Backend.Domain;

namespace Pdv.Backend.Contracts;

public sealed record CreateUserRequest(string Username, string Password, string Role);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static UserResponse From(User u) =>
        new(u.Id, u.Username, u.Role, u.IsActive, u.CreatedAt);
}
