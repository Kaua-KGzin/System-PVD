using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class UserService(PdvDbContext db)
{
    private static readonly string[] ValidRoles = ["Admin", "Operator", "Manager"];

    private static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            return "Senha precisa ter no minimo 12 caracteres.";

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
            return "Senha precisa conter letras maiusculas, minusculas, numeros e caracteres especiais.";

        return null;
    }

    public async Task<PagedResponse<UserResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => UserResponse.From(u))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<UserResponse>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return ServiceResult<UserResponse>.Fail("Username e obrigatorio.");

        var passwordError = ValidatePassword(request.Password);
        if (passwordError is not null)
            return ServiceResult<UserResponse>.Fail(passwordError);

        var role = request.Role?.Trim();
        if (!ValidRoles.Contains(role))
            return ServiceResult<UserResponse>.Fail($"Role invalida. Valores aceitos: {string.Join(", ", ValidRoles)}.");

        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            return ServiceResult<UserResponse>.Fail("Ja existe um usuario com este username.", StatusCodes.Status409Conflict);

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role!
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserResponse>.Ok(UserResponse.From(user));
    }

    public async Task<ServiceResult<UserResponse>> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return ServiceResult<UserResponse>.Fail("Usuario nao encontrado.", StatusCodes.Status404NotFound);

        user.IsActive = false;

        await db.RefreshTokens
            .Where(r => r.UserId == id && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<UserResponse>.Ok(UserResponse.From(user));
    }

    public async Task<ServiceResult<UserResponse>> ReactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return ServiceResult<UserResponse>.Fail("Usuario nao encontrado.", StatusCodes.Status404NotFound);

        user.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<UserResponse>.Ok(UserResponse.From(user));
    }

    public async Task<ServiceResult<UserResponse>> ChangePasswordAsync(Guid id, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var passwordError = ValidatePassword(request.NewPassword);
        if (passwordError is not null)
            return ServiceResult<UserResponse>.Fail(passwordError);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
            return ServiceResult<UserResponse>.Fail("Usuario nao encontrado.", StatusCodes.Status404NotFound);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ServiceResult<UserResponse>.Fail("Senha atual incorreta.", StatusCodes.Status401Unauthorized);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await db.RefreshTokens
            .Where(r => r.UserId == id && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<UserResponse>.Ok(UserResponse.From(user));
    }
}
