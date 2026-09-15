import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { CartItem, Product } from '../types'

interface CartState {
  items: CartItem[]
  cashSessionId: string | null
  operatorName: string
  saleDiscount: number
  setCashSession: (id: string, operator: string) => void
  addItem: (product: Product, quantity?: number) => void
  removeItem: (barcode: string) => void
  updateQuantity: (barcode: string, quantity: number) => void
  updateDiscount: (barcode: string, discount: number) => void
  setSaleDiscount: (discount: number) => void
  clear: () => void
  grossTotal: () => number
  netTotal: () => number
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      items: [],
      cashSessionId: null,
      operatorName: '',
      saleDiscount: 0,

      setCashSession: (id, operator) => set({ cashSessionId: id, operatorName: operator }),

      addItem: (product, quantity = 1) => {
        const items = get().items
        const existing = items.find((i) => i.product.barcode === product.barcode)
        if (existing) {
          set({
            items: items.map((i) =>
              i.product.barcode === product.barcode ? { ...i, quantity: i.quantity + quantity } : i
            ),
          })
        } else {
          set({ items: [...items, { product, quantity, unitDiscount: 0 }] })
        }
      },

      removeItem: (barcode) =>
        set({ items: get().items.filter((i) => i.product.barcode !== barcode) }),

      updateQuantity: (barcode, quantity) =>
        set({
          items: get().items.map((i) =>
            i.product.barcode === barcode ? { ...i, quantity: Math.max(1, quantity) } : i
          ),
        }),

      updateDiscount: (barcode, discount) =>
        set({
          items: get().items.map((i) =>
            i.product.barcode === barcode ? { ...i, unitDiscount: Math.max(0, discount) } : i
          ),
        }),

      setSaleDiscount: (discount) => set({ saleDiscount: Math.max(0, discount) }),

      clear: () => set({ items: [], saleDiscount: 0 }),

      grossTotal: () => get().items.reduce((sum, i) => sum + i.product.unitPrice * i.quantity, 0),

      netTotal: () => {
        const gross = get().grossTotal()
        const itemDiscounts = get().items.reduce((sum, i) => sum + (i.unitDiscount || 0) * i.quantity, 0)
        return Math.max(0, gross - itemDiscounts - get().saleDiscount)
      },
    }),
    {
      name: 'archlab-cart-storage',
      partialize: (state) => ({
        items: state.items,
        cashSessionId: state.cashSessionId,
        operatorName: state.operatorName,
        saleDiscount: state.saleDiscount,
      }),
    }
  )
)
