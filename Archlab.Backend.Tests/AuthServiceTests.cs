using Microsoft.Data.Sqlite;
using Archlab.Backend.Contracts;
using Archlab.Backend.Domain;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new AuthService(_db, DbContextFactory.BuildConfig());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<User> SeedUserAsync(string username = "testuser", string password = "password123", string role = "Operator")
    {
        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync(new LoginRequest("testuser", "password123"), default);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value!.Token);
        Assert.NotEmpty(result.Value.RefreshToken);
        Assert.Equal("testuser", result.Value.Username);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        await SeedUserAsync();

        var result = await _sut.LoginAsync(new LoginRequest("testuser", "wrong"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Fails()
    {
        var result = await _sut.LoginAsync(new LoginRequest("nobody", "password"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Login_InactiveUser_Fails()
    {
        var user = await SeedUserAsync();
        user.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest("testuser", "password123"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewToken()
    {
        await SeedUserAsync();
        var loginResult = await _sut.LoginAsync(new LoginRequest("testuser", "password123"), default);
        var refreshToken = loginResult.Value!.RefreshToken;

        var refreshResult = await _sut.RefreshAsync(new RefreshTokenRequest(refreshToken), default);

        Assert.True(refreshResult.Succeeded);
        Assert.NotEmpty(refreshResult.Value!.Token);
        Assert.NotEqual(refreshToken, refreshResult.Value.RefreshToken);
    }

    [Fact]
    public async Task Refresh_RevokedToken_Fails()
    {
        await SeedUserAsync();
        var loginResult = await _sut.LoginAsync(new LoginRequest("testuser", "password123"), default);
        var refreshToken = loginResult.Value!.RefreshToken;

        await _sut.LogoutAsync(refreshToken, default);

        var refreshResult = await _sut.RefreshAsync(new RefreshTokenRequest(refreshToken), default);

        Assert.False(refreshResult.Succeeded);
        Assert.Equal(401, refreshResult.Error!.StatusCode);
    }
}
