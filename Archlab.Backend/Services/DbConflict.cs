using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Archlab.Backend.Services;

/// <summary>
/// Tells apart "two tills collided, try again" from "the write is genuinely broken".
/// </summary>
/// <remarks>
/// EF raises <see cref="DbUpdateConcurrencyException"/> only when a concurrency token stops a
/// row from matching. PostgreSQL loses a race in two other ways: a Serializable transaction
/// aborted by SSI (SQLSTATE 40001) and a unique-key violation (23505) — which is exactly what
/// the Sale.Number index produces when two terminals draw the same counter value. Both mean the
/// caller should retry, so both belong on the 409 path instead of falling through to a 500.
/// SQLite reports neither, which is why the SQLite-backed suite never exercised this.
/// <para>
/// The SQLSTATE is checked on the exception itself as well as on its inner exception, because
/// where it surfaces decides how it is wrapped: raised by a statement it arrives inside a
/// <see cref="DbUpdateException"/> from SaveChanges, but SSI can equally fail the transaction at
/// COMMIT, and that one comes straight out of CommitAsync as the provider's own DbException with
/// no EF wrapper around it.
/// </para>
/// </remarks>
public static class DbConflict
{
    private const string SerializationFailure = "40001";
    private const string UniqueViolation = "23505";
    private const string DeadlockDetected = "40P01";

    public static bool IsRetryable(Exception exception) =>
        HasRetryableSqlState(exception) || HasRetryableSqlState(exception.InnerException);

    private static bool HasRetryableSqlState(Exception? exception) =>
        exception is DbException
        {
            SqlState: SerializationFailure or UniqueViolation or DeadlockDetected
        };
}
