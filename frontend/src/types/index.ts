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
  internalCode: string | null
  name: string
  unitOfMeasure: string
  unitPrice: number
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
  productId: string
  barcode: string
  productName: string
  quantity: number
  unitPrice: number
  itemDiscount: number
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
  tradeName: string | null
  document: string | null
  phone: string | null
  email: string | null
  isActive: boolean
}

export interface DashboardData {
  todaySales: number
  todayRevenue: number
  todayAvgTicket: number
  activeProducts: number
  lowStockProducts: number
  openCashSessions: number
  recentSales: Sale[]
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
  itemDiscount: number
}
