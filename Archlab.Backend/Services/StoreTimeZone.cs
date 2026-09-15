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
    /// The half-open window [Start, End) covering the current day in <paramref name="zone"/>,
    /// expressed as UTC instants.
    /// </summary>
    /// <remarks>
    /// Returned in UTC on purpose. These bounds are compared against Sale.CreatedAt, which maps to
    /// `timestamp with time zone` on PostgreSQL, and Npgsql refuses a DateTimeOffset parameter
    /// whose offset is not zero — so handing back the -03:00 values the calendar arithmetic
    /// produces would throw before the query ran. Converting keeps the same instants.
    /// </remarks>
    public static (DateTimeOffset Start, DateTimeOffset End) Today(TimeZoneInfo zone)
    {
        // Both boundaries are read as local times in the zone itself, each with the offset in
        // effect on its own date: adding 24 elapsed hours to the start would land an hour off
        // across a DST transition, taking an extra hour in spring and dropping one in autumn.
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
        var tomorrow = today.AddDays(1);

        var start = new DateTimeOffset(today, zone.GetUtcOffset(today));
        var end = new DateTimeOffset(tomorrow, zone.GetUtcOffset(tomorrow));

        return (start.ToUniversalTime(), end.ToUniversalTime());
    }
}
