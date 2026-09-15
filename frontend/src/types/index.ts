export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface LoginRequest {
  username: string
  password: string
}

export interface AuthResponse {
  token: string
  refreshToken: string
  username: string
  role: string
  expiresAt: string
}

export interface Product {
  id: string
  barcode: string
  sku: string | null
  name: string
  unitOfMeasure: string
  unitPrice: number
  costPrice: number
  stockQuantity: number
  minStockQuantity: number
  isActive: boolean
  categoryId: string | null
  categoryName: string | null
}

export interface Category {
  id: string
  name: string
  description: string | null
  isActive: boolean
  createdAt: string
}

export interface Customer {
  id: string
  name: string
  document: string | null
  phone: string | null
  email: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

export interface SaleItem {
  id: string
  productId: string
  barcode: string
  productName: string
  quantity: number
  unitPrice: number
  unitDiscount: number
  grossTotal: number
  discountTotal: number
  netTotal: number
}

export interface SalePayment {
  method: string
  amount: number
  cardBrand: string | null
}

export interface Sale {
  id: string
  number: number
  terminalId: string
  operatorName: string
  customerDocument: string | null
  status: string
  grossTotal: number
  itemDiscountTotal: number
  saleDiscountTotal: number
  netTotal: number
  amountPaid: number
  changeAmount: number
  cancelledAt: string | null
  cancellationReason: string | null
  createdAt: string
  items: SaleItem[]
  payments: SalePayment[]
}

export interface CashSession {
  id: string
  terminalId: string
  operatorName: string
  openingAmount: number
  openedAt: string
  closedAt: string | null
  expectedClosingAmount: number
  closingAmount: number | null
  closingDifference: number | null
  closingNotes: string | null
  status: string
}

export interface Supplier {
  id: string
  name: string
  cnpj: string | null
  contactName: string | null
  phone: string | null
  email: string | null
  isActive: boolean
}

export interface TodaySalesStats {
  count: number
  total: number
  averageTicket: number
}

export interface RecentSaleEntry {
  id: string
  number: number
  terminalId: string
  operatorName: string
  netTotal: number
  status: string
  createdAt: string
}

export interface DashboardData {
  todaySales: TodaySalesStats
  openCashSessions: number
  lowStockProducts: number
  recentSales: RecentSaleEntry[]
}

export interface SalesSummary {
  totalSales: number
  totalRevenue: number
  totalDiscounts: number
  netRevenue: number
  byPaymentMethod: { method: string; count: number; total: number }[]
  byHour: { hour: number; count: number; total: number }[]
}

export interface StockAlert {
  id: string
  barcode: string
  name: string
  stockQuantity: number
  minStockQuantity: number
  deficit: number
}

export interface CartItem {
  product: Product
  quantity: number
  unitDiscount: number
}

export interface CashMovement {
  id: string
  cashSessionId: string
  type: 'Supply' | 'Bleed'
  amount: number
  reason: string | null
  operatorName: string
  createdAt: string
}

export interface SaleReturnItem {
  id: string
  productId: string
  barcode: string
  productName: string
  quantity: number
  unitPrice: number
  refundAmount: number
}

export interface SaleReturn {
  id: string
  saleId: string
  reason: string
  operatorName: string
  totalRefundAmount: number
  returnedAt: string
  items: SaleReturnItem[]
}

export interface User {
  id: string
  username: string
  role: string
  isActive: boolean
  createdAt: string
}

export interface AuditLog {
  id: string
  userId: string | null
  username: string | null
  action: string
  entityName: string
  entityId: string | null
  changesJson: string | null
  ipAddress: string | null
  timestamp: string
}

export interface PurchaseEntry {
  id: string
  supplierId: string
  supplierName: string
  invoiceNumber: string
  notes: string | null
  totalCost: number
  receivedAt: string
  createdAt: string
  items: PurchaseEntryItem[]
}

export interface PurchaseEntryItem {
  id: string
  productId: string
  productName: string
  barcode: string
  quantity: number
  unitCost: number
  totalCost: number
}

export interface CustomerLoyalty {
  id: string
  customerId: string
  customerName: string
  totalPoints: number
  redeemedPoints: number
  availablePoints: number
  createdAt: string
  transactions: LoyaltyTransaction[]
}

export interface LoyaltyTransaction {
  id: string
  points: number
  type: string
  notes: string | null
  createdAt: string
}

export interface SellerCommission {
  id: string
  userId: string
  username: string
  percentage: number
  isActive: boolean
  createdAt: string
}

export interface CommissionTransaction {
  id: string
  saleNumber: number
  saleAmount: number
  commissionPercentage: number
  commissionAmount: number
  isPaid: boolean
  paidAt: string | null
  createdAt: string
}

export interface CommissionSummary {
  totalCommission: number
  paidCommission: number
  pendingCommission: number
  totalSales: number
  recentTransactions: CommissionTransaction[]
}
