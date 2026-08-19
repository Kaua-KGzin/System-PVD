import { useState, useRef } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { Trash2, Plus, Minus, ShoppingBag } from 'lucide-react'
import { api } from '../api/client'
import { errorDetail } from '../api/errors'
import { useCartStore } from '../store/cart'
import { useSettingsStore } from '../store/settings'
import type { CashSession, Product } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

type PaymentMethod = 'Cash' | 'Debit' | 'Credit' | 'Pix'

export default function PdvPage() {
  const { terminalId } = useSettingsStore()
  const barcodeRef = useRef<HTMLInputElement>(null)
  const [barcode, setBarcode] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [amountPaid, setAmountPaid] = useState('')
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)
  const [lastSale, setLastSale] = useState<{ number: number; change: number } | null>(null)

  const { items, cashSessionId, addItem, removeItem, updateQuantity, grossTotal, netTotal, saleDiscount, clear } = useCartStore()

  const { data: openSession } = useQuery<CashSession>({
    queryKey: ['open-session', terminalId],
    queryFn: () => api.get(`/cash-sessions/open/${terminalId}`).then((r) => r.data),
    retry: false,
  })

  const lookupMutation = useMutation({
    mutationFn: (bc: string) => api.get<Product>(`/products/barcode/${encodeURIComponent(bc)}`).then((r) => r.data),
    onSuccess: (product) => {
      addItem(product)
      setBarcode('')
      barcodeRef.current?.focus()
    },
    onError: () => {
      setMsg({ type: 'err', text: 'Produto não encontrado.' })
      setTimeout(() => setMsg(null), 2000)
    },
  })

  const saleMutation = useMutation({
    mutationFn: () =>
      api.post('/sales', {
        cashSessionId: openSession?.id ?? cashSessionId,
        operatorName: openSession?.operatorName ?? 'Operador',
        saleDiscountTotal: saleDiscount,
        items: items.map((i) => ({
          barcode: i.product.barcode,
          quantity: i.quantity,
          itemDiscount: i.itemDiscount,
        })),
        payments: [{ method: paymentMethod, amount: parseFloat(amountPaid) || netTotal(), cardBrand: null }],
        issueFiscalDocument: false,
      }).then((r) => r.data),
    onSuccess: (sale) => {
      setLastSale({ number: sale.number, change: sale.changeAmount })
      clear()
      setAmountPaid('')
      setMsg({ type: 'ok', text: `Venda #${sale.number} finalizada!` })
      setTimeout(() => setMsg(null), 4000)
    },
    onError: (e) => {
      setMsg({ type: 'err', text: errorDetail(e, 'Erro ao finalizar venda.') })
      setTimeout(() => setMsg(null), 4000)
    },
  })

  const handleBarcodeSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (barcode.trim()) lookupMutation.mutate(barcode.trim())
  }

  if (!openSession) {
    return (
      <div className="page">
        <h1 className="page-title">Frente de Caixa</h1>
        <div className="alert alert-warn">
          Nenhum caixa aberto para {terminalId}. Abra um caixa na aba <strong>Caixa</strong> antes de vender.
        </div>
      </div>
    )
  }

  return (
    <div className="pdv-layout">
      <div className="pdv-cart">
        <div className="pdv-header">
          <h1 className="page-title">Frente de Caixa</h1>
          <span className="badge badge-open">Caixa: {openSession.terminalId}</span>
        </div>

        <form onSubmit={handleBarcodeSubmit} className="barcode-form">
          <input
            ref={barcodeRef}
            value={barcode}
            onChange={(e) => setBarcode(e.target.value)}
            placeholder="Código de barras ou EAN..."
            className="barcode-input"
            autoFocus
          />
          <button type="submit" className="btn-primary" disabled={lookupMutation.isPending}>
            Adicionar
          </button>
        </form>

        {msg && <div className={`alert alert-${msg.type === 'ok' ? 'ok' : 'err'}`}>{msg.text}</div>}
        {lastSale && (
          <div className="alert alert-ok">
            Troco: <strong>{fmt(lastSale.change)}</strong>
          </div>
        )}

        <div className="cart-items">
          {items.length === 0 ? (
            <div className="empty-cart">
              <ShoppingBag size={48} className="text-gray-300" />
              <p>Carrinho vazio</p>
            </div>
          ) : (
            items.map((item) => (
              <div key={item.product.barcode} className="cart-item">
                <div className="cart-item-info">
                  <p className="cart-item-name">{item.product.name}</p>
                  <p className="cart-item-price">{fmt(item.product.unitPrice)} un.</p>
                </div>
                <div className="cart-item-controls">
                  <button onClick={() => updateQuantity(item.product.barcode, item.quantity - 1)} className="qty-btn">
                    <Minus size={14} />
                  </button>
                  <span className="qty-value">{item.quantity}</span>
                  <button onClick={() => updateQuantity(item.product.barcode, item.quantity + 1)} className="qty-btn">
                    <Plus size={14} />
                  </button>
                </div>
                <p className="cart-item-total">{fmt(item.product.unitPrice * item.quantity)}</p>
                <button onClick={() => removeItem(item.product.barcode)} className="btn-icon-danger">
                  <Trash2 size={16} />
                </button>
              </div>
            ))
          )}
        </div>
      </div>

      <div className="pdv-checkout">
        <div className="checkout-totals">
          <div className="total-row">
            <span>Subtotal</span>
            <span>{fmt(grossTotal())}</span>
          </div>
          <div className="total-row total-net">
            <span>Total</span>
            <span>{fmt(netTotal())}</span>
          </div>
        </div>

        <div className="form-group">
          <label>Forma de pagamento</label>
          <div className="payment-methods">
            {(['Cash', 'Debit', 'Credit', 'Pix'] as PaymentMethod[]).map((m) => (
              <button
                key={m}
                className={`payment-btn ${paymentMethod === m ? 'active' : ''}`}
                onClick={() => setPaymentMethod(m)}
              >
                {m === 'Cash' ? 'Dinheiro' : m === 'Debit' ? 'Débito' : m === 'Credit' ? 'Crédito' : 'PIX'}
              </button>
            ))}
          </div>
        </div>

        {paymentMethod === 'Cash' && (
          <div className="form-group">
            <label>Valor recebido</label>
            <input
              type="number"
              step="0.01"
              min="0"
              value={amountPaid}
              onChange={(e) => setAmountPaid(e.target.value)}
              placeholder={fmt(netTotal())}
            />
            {amountPaid && parseFloat(amountPaid) >= netTotal() && (
              <p className="change-preview">Troco: {fmt(parseFloat(amountPaid) - netTotal())}</p>
            )}
          </div>
        )}

        <button
          className="btn-primary btn-lg w-full"
          onClick={() => saleMutation.mutate()}
          disabled={items.length === 0 || saleMutation.isPending}
        >
          {saleMutation.isPending ? 'Processando...' : `Finalizar — ${fmt(netTotal())}`}
        </button>

        <button className="btn-secondary w-full mt-2" onClick={clear} disabled={items.length === 0}>
          Limpar carrinho
        </button>
      </div>
    </div>
  )
}
