namespace Pdv.Backend.Contracts;

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? Cnpj,
    string? ContactName,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public sealed record CreateSupplierRequest(
    string Name,
    string? Cnpj,
    string? ContactName,
    string? Phone,
    string? Email
);

public sealed record UpdateSupplierRequest(
    string? Name,
    string? Cnpj,
    string? ContactName,
    string? Phone,
    string? Email,
    bool? IsActive
);

public sealed record PurchaseEntryResponse(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string InvoiceNumber,
    string? Notes,
    decimal TotalCost,
    DateTimeOffset ReceivedAt,
    DateTimeOffset CreatedAt,
    PurchaseEntryItemResponse[] Items
);

public sealed record PurchaseEntryItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost
);

public sealed record CreatePurchaseEntryRequest(
    Guid SupplierId,
    string InvoiceNumber,
    string? Notes,
    DateTimeOffset? ReceivedAt,
    List<CreatePurchaseEntryItemRequest> Items
);

public sealed record CreatePurchaseEntryItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost
);
