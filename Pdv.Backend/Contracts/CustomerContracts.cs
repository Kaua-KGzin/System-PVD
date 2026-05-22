using Pdv.Backend.Domain;

namespace Pdv.Backend.Contracts;

public sealed record CreateCustomerRequest(
    string Name,
    string? Document,
    string? Phone,
    string? Email);

public sealed record UpdateCustomerRequest(
    string Name,
    string? Document,
    string? Phone,
    string? Email,
    bool IsActive);

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Document,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static CustomerResponse From(Customer c) =>
        new(c.Id, c.Name, c.Document, c.Phone, c.Email, c.IsActive, c.CreatedAt, c.UpdatedAt);
}
