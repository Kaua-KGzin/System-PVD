using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Data.Interceptors;

// The accessor is optional so tests can construct the interceptor without a request pipeline;
// outside a request the actor columns stay null.
public sealed class AuditLogInterceptor(IHttpContextAccessor? httpContextAccessor = null) : SaveChangesInterceptor
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Secrets must never reach the audit trail: the hash is credential material and the
    // refresh token is a live credential — every login writes one.
    private static readonly HashSet<string> RedactedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(User.PasswordHash),
        nameof(RefreshToken.Token)
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        CreateAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null)
            return base.SavingChanges(eventData, result);

        CreateAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void CreateAuditLogs(DbContext context)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => e.Entity is not AuditLog)
            .ToList();

        if (entries.Count == 0) return;

        var httpContext = httpContextAccessor?.HttpContext;
        var principal = httpContext?.User;
        var userId = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = principal?.Identity?.IsAuthenticated == true
            ? principal.Identity.Name ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            : null;
        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();

        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var action = entry.State.ToString();

            var changes = new Dictionary<string, object?>();
            string? entityId = null;

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey())
                {
                    entityId = property.CurrentValue?.ToString();
                }

                var isSensitive = RedactedProperties.Contains(property.Metadata.Name);

                if (entry.State == EntityState.Modified && property.IsModified)
                {
                    changes[property.Metadata.Name] = isSensitive
                        ? new { Original = (object?)RedactedValue, Current = (object?)RedactedValue }
                        : new { Original = property.OriginalValue, Current = property.CurrentValue };
                }
                else if (entry.State == EntityState.Added)
                {
                    changes[property.Metadata.Name] = isSensitive ? RedactedValue : property.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    changes[property.Metadata.Name] = isSensitive ? RedactedValue : property.OriginalValue;
                }
            }

            var auditLog = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                ChangesJson = JsonSerializer.Serialize(changes, JsonOptions),
                UserId = userId,
                Username = username,
                IpAddress = ipAddress,
                Timestamp = DateTimeOffset.UtcNow
            };

            context.Set<AuditLog>().Add(auditLog);
        }
    }
}
