import { useState, useRef, useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Trash2, Plus, Minus, ShoppingBag, Search, ArrowDownRight, X } from 'lucide-react'
import { api } from '../api/client'
import { errorDetail } from '../api/errors'
import { useCartStore } from '../store/cart'
import { useSettingsStore } from '../store/settings'
import type { CashSession, Product } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

type PaymentMethod = 'Cash' | 'DebitCard' | 'CreditCard' | 'Pix' | 'Voucher' | 'StoreCredit'

interface PaymentLine {
  method: PaymentMethod
  amount: number
  transactionReference?: string
}

export default function PdvPage() {
  const qc = useQueryClient()
  const { terminalId } = useSettingsStore()
  const barcodeRef = useRef<HTMLInputElement>(null)
  const searchInputRef = useRef<HTMLInputElement>(null)

  const [barcode, setBarcode] = useState('')
  const [selectedMethod, setSelectedMethod] = useState<PaymentMethod>('Cash')
  const [paymentAmount, setPaymentAmount] = useState('')
  const [payments, setPayments] = useState<PaymentLine[]>([])
  const [msg, setMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)
  const [lastSale, setLastSale] = useState<{ number: number; change: number } | null>(null)

  // Modais de atalhos
  const [isSearchOpen, setIsSearchOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')
  const [isDiscountOpen, setIsDiscountOpen] = useState(false)
  const [discountInput, setDiscountInput] = useState('')
  const [isMovementOpen, setIsMovementOpen] = useState(false)
  const [movType, setMovType] = useState<'Supply' | 'Bleed'>('Supply')
  const [movAmount, setMovAmount] = useState('')
  const [movReason, setMovReason] = useState('')

  // Pix QR Code Modal
  const [pixData, setPixData] = useState<{ qrcode: string; copiaECola: string } | null>(null)

  const { items, cashSessionId, addItem, removeItem, updateQuantity, grossTotal, netTotal, saleDiscount, setSaleDiscount, clear } = useCartStore()

  const { data: openSession } = useQuery<CashSession>({
    queryKey: ['open-session', terminalId],
    queryFn: () => api.get(`/cash-sessions/open/${terminalId}`).then((r) => r.data),
    retry: false,
  })

  // Pesquisa de produtos (F1)
  const { data: searchResults } = useQuery<any>({
    queryKey: ['search-products', searchQuery],
    queryFn: () => api.get(`/products?search=${encodeURIComponent(searchQuery)}&pageSize=8`).then((r) => r.data),
    enabled: isSearchOpen && searchQuery.trim().length > 1,
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

  const pixMutation = useMutation({
    mutationFn: (amount: number) =>
      api.post('/payments/pix', { amount, description: `Venda ${terminalId}` }).then((r) => r.data),
    onSuccess: (data) => {
      setPixData({ qrcode: data.qrCodeBase64, copiaECola: data.copiaECola })
    },
  })

  const movementMutation = useMutation({
    mutationFn: () =>
      api.post(`/cash-sessions/${openSession!.id}/movements`, {
        type: movType,
        amount: parseFloat(movAmount) || 0,
        reason: movReason.trim() || null,
        operatorName: openSession?.operatorName ?? 'Operador',
      }),
    onSuccess: () => {
      setMovAmount('')
      setMovReason('')
      setIsMovementOpen(false)
      qc.invalidateQueries({ queryKey: ['open-session', terminalId] })
      setMsg({ type: 'ok', text: `${movType === 'Supply' ? 'Suprimento' : 'Sangria'} registrado com sucesso!` })
      setTimeout(() => setMsg(null), 3000)
    },
    onError: (err: any) => {
      setMsg({ type: 'err', text: err.response?.data?.error || 'Erro na movimentação de caixa.' })
      setTimeout(() => setMsg(null), 3000)
    },
  })

  // Cálculos de pagamento
  const totalDue = netTotal()
  const totalPaid = payments.reduce((sum, p) => sum + p.amount, 0)
  const remainingDue = Math.max(0, Math.round((totalDue - totalPaid) * 100) / 100)
  const change = Math.max(0, Math.round((totalPaid - totalDue) * 100) / 100)

  const saleMutation = useMutation({
    mutationFn: () => {
      // Se não adicionou pagamentos explícitos na lista mas tem valor digitado ou valor total único
      const resolvedPayments = payments.length > 0
        ? payments
        : [{ method: selectedMethod, amount: parseFloat(paymentAmount) || totalDue }]

      return api.post('/sales', {
        cashSessionId: openSession?.id ?? cashSessionId,
        operatorName: openSession?.operatorName ?? 'Operador',
        saleDiscountTotal: saleDiscount,
        items: items.map((i) => ({
          barcode: i.product.barcode,
          quantity: i.quantity,
          unitDiscount: i.unitDiscount ?? 0,
        })),
        payments: resolvedPayments.map((p) => ({
          method: p.method,
          amount: p.amount,
          transactionReference: p.transactionReference ?? null,
        })),
        issueFiscalDocument: false,
      }).then((r) => r.data)
    },
    onSuccess: (sale) => {
      setLastSale({ number: sale.number, change: sale.changeAmount })
      clear()
      setPayments([])
      setPaymentAmount('')
      setPixData(null)
      setMsg({ type: 'ok', text: `Venda #${sale.number} finalizada com sucesso!` })
      setTimeout(() => setMsg(null), 4000)
      barcodeRef.current?.focus()
    },
    onError: (e) => {
      setMsg({ type: 'err', text: errorDetail(e, 'Erro ao finalizar venda.') })
      setTimeout(() => setMsg(null), 4000)
    },
  })

  // Atalhos de teclado (Zero Mouse)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F1') {
        e.preventDefault()
        setIsSearchOpen(true)
      } else if (e.key === 'F2') {
        e.preventDefault()
        if (items.length > 0) {
          const lastItem = items[items.length - 1]
          const q = prompt(`Alterar quantidade de ${lastItem.product.name}:`, String(lastItem.quantity))
          if (q && !isNaN(Number(q)) && Number(q) > 0) {
            updateQuantity(lastItem.product.barcode, Number(q))
          }
        }
      } else if (e.key === 'F3') {
        e.preventDefault()
        setIsDiscountOpen(true)
      } else if (e.key === 'F8') {
        e.preventDefault()
        setIsMovementOpen(true)
      } else if (e.key === 'F12') {
        e.preventDefault()
        if (items.length > 0 && (totalPaid >= totalDue || payments.length === 0)) {
          saleMutation.mutate()
        }
      } else if (e.key === 'Escape') {
        setIsSearchOpen(false)
        setIsDiscountOpen(false)
        setIsMovementOpen(false)
        setPixData(null)
        barcodeRef.current?.focus()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [items, totalPaid, totalDue, payments, saleMutation, updateQuantity])

  const handleBarcodeSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (barcode.trim()) lookupMutation.mutate(barcode.trim())
  }

  const addPaymentLine = () => {
    const val = parseFloat(paymentAmount) || remainingDue
    if (val <= 0) return

    setPayments([...payments, { method: selectedMethod, amount: val }])
    setPaymentAmount('')

    if (selectedMethod === 'Pix') {
      pixMutation.mutate(val)
    }
  }

  const removePaymentLine = (idx: number) => {
    setPayments(payments.filter((_, i) => i !== idx))
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
        <div className="pdv-header flex justify-between items-center">
          <div>
            <h1 className="page-title">Frente de Caixa</h1>
            <span className="badge badge-open">Terminal: {openSession.terminalId} ({openSession.operatorName})</span>
          </div>
          <div className="flex gap-2">
            <button className="btn btn-secondary text-xs flex items-center gap-1" onClick={() => setIsSearchOpen(true)}>
              <Search size={14} /> Buscar Produto (F1)
            </button>
            <button className="btn btn-secondary text-xs flex items-center gap-1" onClick={() => setIsMovementOpen(true)}>
              <ArrowDownRight size={14} /> Sangria/Suprimento (F8)
            </button>
          </div>
        </div>

        <form onSubmit={handleBarcodeSubmit} className="barcode-form">
          <input
            ref={barcodeRef}
            value={barcode}
            onChange={(e) => setBarcode(e.target.value)}
            placeholder="Código de barras, EAN ou bipe o produto..."
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
            Venda #{lastSale.number} finalizada! Troco: <strong>{fmt(lastSale.change)}</strong>
          </div>
        )}

        <div className="cart-items">
          {items.length === 0 ? (
            <div className="empty-cart">
              <ShoppingBag size={48} className="text-gray-300" />
              <p>Carrinho vazio — Bipe um item ou pressione [F1] para buscar</p>
            </div>
          ) : (
            items.map((item) => (
              <div key={item.product.barcode} className="cart-item">
                <div className="cart-item-info">
                  <p className="cart-item-name">{item.product.name}</p>
                  <p className="cart-item-price">
                    {fmt(item.product.unitPrice)} un.
                    {item.unitDiscount > 0 && <span className="text-danger ml-2">(-{fmt(item.unitDiscount)} desc)</span>}
                  </p>
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
                <p className="cart-item-total">{fmt((item.product.unitPrice - (item.unitDiscount || 0)) * item.quantity)}</p>
                <button onClick={() => removeItem(item.product.barcode)} className="btn-icon-danger">
                  <Trash2 size={16} />
                </button>
              </div>
            ))
          )}
        </div>

        {/* Barra de atalhos rápidos do PDV */}
        <div className="pdv-shortcuts-bar flex justify-between text-xs text-gray-500 border-t pt-2 mt-2">
          <span><strong>[F1]</strong> Buscar</span>
          <span><strong>[F2]</strong> Qtd</span>
          <span><strong>[F3]</strong> Desconto</span>
          <span><strong>[F8]</strong> Sangria/Suprimento</span>
          <span><strong>[F12]</strong> Finalizar</span>
          <span><strong>[Esc]</strong> Fechar</span>
        </div>
      </div>

      <div className="pdv-checkout">
        <div className="checkout-totals">
          <div className="total-row">
            <span>Subtotal</span>
            <span>{fmt(grossTotal())}</span>
          </div>
          {saleDiscount > 0 && (
            <div className="total-row text-danger">
              <span>Desconto Geral (F3)</span>
              <span>-{fmt(saleDiscount)}</span>
            </div>
          )}
          <div className="total-row total-net">
            <span>Total a Pagar</span>
            <span>{fmt(totalDue)}</span>
          </div>
        </div>

        {/* Multi-pagamento / Split */}
        <div className="form-group">
          <label>Forma de pagamento</label>
          <div className="payment-methods grid grid-cols-3 gap-2">
            {(
              [
                ['Cash', 'Dinheiro'],
                ['DebitCard', 'Debito'],
                ['CreditCard', 'Credito'],
                ['Pix', 'PIX'],
                ['Voucher', 'Voucher'],
                ['StoreCredit', 'Credito Loja'],
              ] as [PaymentMethod, string][]
            ).map(([m, label]) => (
              <button
                key={m}
                type="button"
                className={`payment-btn ${selectedMethod === m ? 'active' : ''}`}
                onClick={() => setSelectedMethod(m)}
              >
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className="form-group">
          <label className="flex justify-between">
            <span>Valor desta forma de pagamento</span>
            {remainingDue > 0 && <span className="text-xs text-primary font-semibold">Restante: {fmt(remainingDue)}</span>}
          </label>
          <div className="flex gap-2">
            <input
              type="number"
              step="0.01"
              min="0"
              value={paymentAmount}
              onChange={(e) => setPaymentAmount(e.target.value)}
              placeholder={fmt(remainingDue > 0 ? remainingDue : totalDue)}
            />
            <button type="button" className="btn btn-secondary" onClick={addPaymentLine} disabled={items.length === 0}>
              Adicionar
            </button>
          </div>
        </div>

        {/* Lista de parcelas de pagamento adicionadas */}
        {payments.length > 0 && (
          <div className="payments-list bg-gray-50 p-2 rounded border mb-3">
            <p className="text-xs font-semibold text-gray-500 mb-1">Pagamentos Lançados:</p>
            {payments.map((p, idx) => (
              <div key={idx} className="flex justify-between items-center text-sm py-1 border-b last:border-b-0">
                <span>
                  {p.method === 'Cash' ? 'Dinheiro' : p.method === 'DebitCard' ? 'Debito' : p.method === 'CreditCard' ? 'Credito' : p.method === 'Voucher' ? 'Voucher' : p.method === 'StoreCredit' ? 'Credito Loja' : 'PIX'}:
                </span>
                <div className="flex items-center gap-2">
                  <strong>{fmt(p.amount)}</strong>
                  <button type="button" onClick={() => removePaymentLine(idx)} className="text-danger hover:underline">
                    <X size={14} />
                  </button>
                </div>
              </div>
            ))}
            <div className="flex justify-between text-xs mt-2 pt-1 border-t font-semibold">
              <span>Total Pago:</span>
              <span>{fmt(totalPaid)}</span>
            </div>
            {change > 0 && (
              <div className="flex justify-between text-xs text-success font-semibold">
                <span>Troco:</span>
                <span>{fmt(change)}</span>
              </div>
            )}
          </div>
        )}

        {/* Visualização de QR Code Pix quando gerado */}
        {pixData && (
          <div className="pix-card p-3 border rounded text-center mb-3 bg-white">
            <p className="font-semibold text-xs text-primary mb-1">Escaneie o QR Code PIX:</p>
            <img src={pixData.qrcode} alt="QR Code Pix" className="mx-auto w-32 h-32" />
            <p className="text-xs text-gray-500 mt-1 break-all select-all font-mono">{pixData.copiaECola}</p>
          </div>
        )}

        <button
          className="btn-primary btn-lg w-full"
          onClick={() => saleMutation.mutate()}
          disabled={items.length === 0 || saleMutation.isPending || (payments.length > 0 && totalPaid < totalDue)}
        >
          {saleMutation.isPending ? 'Finalizando...' : `Finalizar Venda [F12] — ${fmt(totalDue)}`}
        </button>

        <button className="btn-secondary w-full mt-2" onClick={clear} disabled={items.length === 0}>
          Limpar carrinho
        </button>
      </div>

      {/* Modal F1: Busca de Produtos */}
      {isSearchOpen && (
        <div className="modal-overlay">
          <div className="modal-content card card-md">
            <div className="flex justify-between items-center mb-3">
              <h2 className="card-title">Buscar Produto [F1]</h2>
              <button onClick={() => setIsSearchOpen(false)} className="btn-icon"><X size={18} /></button>
            </div>
            <input
              ref={searchInputRef}
              autoFocus
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Digite o nome ou código do produto..."
              className="w-full mb-3"
            />
            <div className="search-results max-h-60 overflow-y-auto space-y-1">
              {searchResults?.items?.map((p: Product) => (
                <div
                  key={p.id}
                  className="p-2 border rounded hover:bg-gray-100 cursor-pointer flex justify-between items-center"
                  onClick={() => {
                    addItem(p)
                    setIsSearchOpen(false)
                    barcodeRef.current?.focus()
                  }}
                >
                  <div>
                    <p className="font-medium text-sm">{p.name}</p>
                    <p className="text-xs text-gray-500">{p.barcode}</p>
                  </div>
                  <strong>{fmt(p.unitPrice)}</strong>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Modal F3: Desconto Geral */}
      {isDiscountOpen && (
        <div className="modal-overlay">
          <div className="modal-content card card-sm">
            <div className="flex justify-between items-center mb-3">
              <h2 className="card-title">Desconto Geral [F3]</h2>
              <button onClick={() => setIsDiscountOpen(false)} className="btn-icon"><X size={18} /></button>
            </div>
            <div className="form-group">
              <label>Valor do desconto (R$)</label>
              <input
                type="number"
                step="0.01"
                min="0"
                autoFocus
                value={discountInput}
                onChange={(e) => setDiscountInput(e.target.value)}
                placeholder={fmt(saleDiscount)}
              />
            </div>
            <button
              className="btn-primary w-full"
              onClick={() => {
                setSaleDiscount(parseFloat(discountInput) || 0)
                setIsDiscountOpen(false)
              }}
            >
              Aplicar Desconto
            </button>
          </div>
        </div>
      )}

      {/* Modal F8: Sangria / Suprimento Rápido */}
      {isMovementOpen && (
        <div className="modal-overlay">
          <div className="modal-content card card-sm">
            <div className="flex justify-between items-center mb-3">
              <h2 className="card-title">Movimentação Rápida [F8]</h2>
              <button onClick={() => setIsMovementOpen(false)} className="btn-icon"><X size={18} /></button>
            </div>
            <div className="flex gap-2 mb-3">
              <button
                type="button"
                className={`btn flex-1 ${movType === 'Supply' ? 'btn-primary' : 'btn-secondary'}`}
                onClick={() => setMovType('Supply')}
              >
                Suprimento (+)
              </button>
              <button
                type="button"
                className={`btn flex-1 ${movType === 'Bleed' ? 'btn-danger' : 'btn-secondary'}`}
                onClick={() => setMovType('Bleed')}
              >
                Sangria (-)
              </button>
            </div>
            <div className="form-group">
              <label>Valor (R$)</label>
              <input
                type="number"
                step="0.01"
                min="0.01"
                autoFocus
                value={movAmount}
                onChange={(e) => setMovAmount(e.target.value)}
                placeholder="0,00"
              />
            </div>
            <div className="form-group">
              <label>Motivo</label>
              <input
                value={movReason}
                onChange={(e) => setMovReason(e.target.value)}
                placeholder="Ex: Troco inicial ou retirada"
              />
            </div>
            <button
              className="btn-primary w-full"
              onClick={() => movementMutation.mutate()}
              disabled={!movAmount || parseFloat(movAmount) <= 0 || movementMutation.isPending}
            >
              {movementMutation.isPending ? 'Lançando...' : 'Confirmar'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
