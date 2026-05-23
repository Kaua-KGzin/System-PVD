namespace Pdv.Backend.Domain;

public enum CashSessionStatus
{
    Open,
    Closed
}

public enum SaleStatus
{
    Completed,
    Cancelled
}

public enum PaymentMethod
{
    Cash,
    CreditCard,
    DebitCard,
    Pix,
    Voucher,
    StoreCredit
}

public enum FiscalDocumentStatus
{
    Issued,
    Cancelled
}

public enum InventoryMovementType
{
    Sale,
    SaleCancellation,
    PurchaseEntry,
    ManualAdjustment
}
