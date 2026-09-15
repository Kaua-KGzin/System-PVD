using Archlab.Backend.Common;
using Archlab.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Archlab.Backend.Endpoints;

public static class AuditLogEndpoints
{
    public static IEndpointRouteBuilder MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-logs")
            .WithTags("Auditoria")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (
            PdvDbContext db,
            string? entityName,
            string? action,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 50 : pageSize;

            var query = db.AuditLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityName))
                query = query.Where(a => a.EntityName == entityName);

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((resolvedPage - 1) * resolvedPageSize)
                .Take(resolvedPageSize)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.Username,
                    a.Action,
                    a.EntityName,
                    a.EntityId,
                    a.ChangesJson,
                    a.IpAddress,
                    a.Timestamp
                })
                .ToArrayAsync(cancellationToken);

            return Results.Ok(new
            {
                items,
                page = resolvedPage,
                pageSize = resolvedPageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)resolvedPageSize)
            });
        }).WithName("ListAuditLogs");

        return app;
    }
}
