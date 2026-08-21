namespace Archlab.Backend.Services;

/// <summary>
/// The timezone the store keeps its books in, and the "today" window derived from it.
/// </summary>
/// <remarks>
/// Anywhere a figure collapses an instant into a calendar day or an hour of the day — revenue by
/// day, sales by hour, "vendas de hoje" on the dashboard — the answer only means something in one
/// timezone, and it is the store's. Reading the host's instead made the same rows produce
/// different numbers depending on where the process ran: the container has no timezone configured
/// and is therefore UTC, where a sale rung up at 21:00 in Sao Paulo belongs to the next day, while
/// the developer's machine booked it to the right one. Configuration, resolved in one place.
/// </remarks>
public static class StoreTimeZone
{
    public const string ConfigurationKey = "Reports:TimeZone";

    public static TimeZoneInfo Resolve(IConfiguration? configuration) =>
        Resolve(configuration?[ConfigurationKey]);

    /// <summary>
    /// The zone for an IANA id, falling back to UTC when it is unset or the host cannot resolve
    /// it — never to the host's own zone, which is the failure this type exists to prevent.
    /// </summary>
    public static TimeZoneInfo Resolve(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? zone
            : TimeZoneInfo.Utc;

    /// <summary>
    /// The half-open window [Start, End) covering the current day in <paramref name="zone"/>.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Today(TimeZoneInfo zone)
    {
        // Midnight is read as a local time in the zone itself, so GetUtcOffset picks the offset
        // actually in effect then rather than the one in effect now.
        var midnight = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
        var start = new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));

        return (start, start.AddDays(1));
    }
}
