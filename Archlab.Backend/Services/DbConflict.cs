using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Archlab.Backend.Services;

/// <summary>
/// Tells apart "two tills collided, try again" from "the write is genuinely broken".
/// </summary>
/// <remarks>
/// EF raises <see cref="DbUpdateConcurrencyException"/> only when a concurrency token stops a
/// row from matching. PostgreSQL loses a race in two other ways that arrive as a plain
/// <see cref="DbUpdateException"/>: a Serializable transaction aborted by SSI (SQLSTATE 40001)
/// and a unique-key violation (23505) — which is exactly what the Sale.Number index produces
/// when two terminals draw the same counter value. Both mean the caller should retry, so both
/// belong on the 409 path instead of falling through to a 500. SQLite reports neither, which is
/// why the SQLite-backed suite never exercised this.
/// </remarks>
public static class DbConflict
{
    private const string SerializationFailure = "40001";
    private const string UniqueViolation = "23505";
    private const string DeadlockDetected = "40P01";

    public static bool IsRetryable(DbUpdateException exception) =>
        exception.InnerException is DbException
        {
            SqlState: SerializationFailure or UniqueViolation or DeadlockDetected
        };
}
