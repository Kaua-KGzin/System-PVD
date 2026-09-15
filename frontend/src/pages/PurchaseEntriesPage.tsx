import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Search, Trash2 } from 'lucide-react'
import { api } from '../api/client'
import { errorDetail } from '../api/errors'
import type { PurchaseEntry, Product, Supplier } from '../types'

interface ItemForm {
  productId: string
  productName: string
  barcode: string
  quantity: number
  unitCost: number
}

export default function PurchaseEntriesPage() {
  const qc = useQueryClient()
  const [showModal, setShowModal] = useState(false)
  const [detailEntry, setDetailEntry] = useState<PurchaseEntry | null>(null)
  const [form, setForm] = useState({
    supplierId: '',
    invoiceNumber: '',
    notes: '',
  })
  const [items, setItems] = useState<ItemForm[]>([])
  const [productSearch, setProductSearch] = useState('')
  const [error, setError] = useState('')

  const { data: entriesData, isLoading } = useQuery({
    queryKey: ['purchase-entries'],
    queryFn: () => api.get('/purchase-entries').then((r) => r.data),
  })

  const { data: suppliersData } = useQuery({
    queryKey: ['suppliers-list'],
    queryFn: () => api.get('/suppliers', { params: { pageSize: 100 } }).then((r) => r.data),
  })

  const { data: productsData } = useQuery({
    queryKey: ['products-search', productSearch],
    queryFn: () =>
      api
        .get('/products', { params: { search: productSearch, pageSize: 20 } })
        .then((r) => r.data),
    enabled: productSearch.length >= 2,
  })

  const createMutation = useMutation({
    mutationFn: () =>
      api.post('/purchase-entries', {
        supplierId: form.supplierId,
        invoiceNumber: form.invoiceNumber,
        notes: form.notes || null,
        items: items.map((i) => ({
          productId: i.productId,
          quantity: i.quantity,
          unitCost: i.unitCost,
        })),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['purchase-entries'] })
      setShowModal(false)
      resetForm()
    },
    onError: (e) => {
      setError(errorDetail(e, 'Erro ao registrar entrada.'))
    },
  })

  const resetForm = () => {
    setForm({ supplierId: '', invoiceNumber: '', notes: '' })
    setItems([])
    setProductSearch('')
    setError('')
  }

  const addProduct = (product: Product) => {
    if (items.some((i) => i.productId === product.id)) return
    setItems((prev) => [
      ...prev,
      {
        productId: product.id,
        productName: product.name,
        barcode: product.barcode,
        quantity: 1,
        unitCost: 0,
      },
    ])
    setProductSearch('')
  }

  const removeItem = (index: number) => {
    setItems((prev) => prev.filter((_, i) => i !== index))
  }

  const updateItem = (index: number, field: keyof ItemForm, value: number) => {
    setItems((prev) =>
      prev.map((item, i) =>
        i === index ? { ...item, [field]: value } : item
      )
    )
  }

  const suppliers: Supplier[] = suppliersData?.items ?? []
  const entries: PurchaseEntry[] = entriesData?.items ?? []
  const products: Product[] = productsData?.items ?? []
  const totalCost = items.reduce((sum, i) => sum + i.quantity * i.unitCost, 0)

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Entradas de Compra</h1>
        <button
          className="btn-primary"
          onClick={() => {
            resetForm()
            setShowModal(true)
          }}
        >
          <Plus size={16} /> Nova entrada
        </button>
      </div>

      {isLoading ? (
        <p className="loading">Carregando...</p>
      ) : !entries.length ? (
        <div className="card">
          <p className="empty-text">Nenhuma entrada registrada.</p>
        </div>
      ) : (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>NF</th>
                <th>Fornecedor</th>
                <th>Itens</th>
                <th>Total</th>
                <th>Data</th>
                <th style={{ width: 80 }}>Acoes</th>
              </tr>
            </thead>
            <tbody>
              {entries.map((e) => (
                <tr key={e.id}>
                  <td className="font-semibold">{e.invoiceNumber}</td>
                  <td>{e.supplierName}</td>
                  <td>{e.items.length}</td>
                  <td className="font-semibold">
                    R$ {e.totalCost.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="text-muted">
                    {new Date(e.receivedAt).toLocaleDateString('pt-BR')}
                  </td>
                  <td>
                    <button
                      className="btn-icon"
                      onClick={() => setDetailEntry(e)}
                      title="Detalhes"
                    >
                      <Search size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal modal-lg" onClick={(e) => e.stopPropagation()}>
            <h2>Nova entrada de compra</h2>

            <div className="form-grid" style={{ marginBottom: 16 }}>
              <div className="form-group">
                <label>Fornecedor *</label>
                <select
                  value={form.supplierId}
                  onChange={(e) => setForm((f) => ({ ...f, supplierId: e.target.value }))}
                >
                  <option value="">Selecione...</option>
                  {suppliers
                    .filter((s) => s.isActive)
                    .map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name}
                      </option>
                    ))}
                </select>
              </div>
              <div className="form-group">
                <label>Numero NF *</label>
                <input
                  value={form.invoiceNumber}
                  onChange={(e) => setForm((f) => ({ ...f, invoiceNumber: e.target.value }))}
                  placeholder="Ex: NF-001234"
                />
              </div>
            </div>

            <div className="form-group" style={{ marginBottom: 16 }}>
              <label>Observacoes</label>
              <input
                value={form.notes}
                onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
                placeholder="Opcional"
              />
            </div>

            <div className="form-group" style={{ marginBottom: 16 }}>
              <label>Adicionar produto</label>
              <input
                value={productSearch}
                onChange={(e) => setProductSearch(e.target.value)}
                placeholder="Buscar por nome ou barcode..."
              />
              {products.length > 0 && (
                <div className="search-results" style={{ border: '1px solid var(--border)', borderRadius: 8, marginTop: 4, maxHeight: 160, overflowY: 'auto' }}>
                  {products.map((p) => (
                    <div
                      key={p.id}
                      style={{ padding: '8px 12px', cursor: 'pointer', borderBottom: '1px solid var(--border)' }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = '#f9fafb')}
                      onMouseLeave={(e) => (e.currentTarget.style.background = '')}
                      onClick={() => addProduct(p)}
                    >
                      <span className="font-semibold">{p.name}</span>
                      <span className="text-muted" style={{ marginLeft: 8 }}>
                        {p.barcode} - R$ {p.unitPrice.toFixed(2)}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {items.length > 0 && (
              <div className="card" style={{ padding: 12, marginBottom: 16 }}>
                <table className="table">
                  <thead>
                    <tr>
                      <th>Produto</th>
                      <th style={{ width: 100 }}>Qtd</th>
                      <th style={{ width: 120 }}>Custo Unit.</th>
                      <th style={{ width: 120 }}>Subtotal</th>
                      <th style={{ width: 50 }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item, idx) => (
                      <tr key={item.productId}>
                        <td>{item.productName}</td>
                        <td>
                          <input
                            type="number"
                            value={item.quantity}
                            onChange={(e) => updateItem(idx, 'quantity', Number(e.target.value))}
                            min={0.01}
                            step={0.01}
                            style={{ width: '100%', padding: '4px 8px', border: '1px solid var(--border)', borderRadius: 4 }}
                          />
                        </td>
                        <td>
                          <input
                            type="number"
                            value={item.unitCost}
                            onChange={(e) => updateItem(idx, 'unitCost', Number(e.target.value))}
                            min={0}
                            step={0.01}
                            style={{ width: '100%', padding: '4px 8px', border: '1px solid var(--border)', borderRadius: 4 }}
                          />
                        </td>
                        <td className="font-semibold">
                          R$ {(item.quantity * item.unitCost).toFixed(2)}
                        </td>
                        <td>
                          <button className="btn-icon-danger" onClick={() => removeItem(idx)}>
                            <Trash2 size={14} />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                <div style={{ textAlign: 'right', marginTop: 8, fontWeight: 700, fontSize: 15 }}>
                  Total: R$ {totalCost.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}
                </div>
              </div>
            )}

            {error && <p className="login-error" style={{ marginTop: 4 }}>{error}</p>}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowModal(false)}>
                Cancelar
              </button>
              <button
                className="btn-primary"
                onClick={() => createMutation.mutate()}
                disabled={!form.supplierId || !form.invoiceNumber.trim() || items.length === 0 || createMutation.isPending}
              >
                {createMutation.isPending ? 'Registrando...' : 'Registrar entrada'}
              </button>
            </div>
          </div>
        </div>
      )}

      {detailEntry && (
        <div className="modal-overlay" onClick={() => setDetailEntry(null)}>
          <div className="modal modal-lg" onClick={(e) => e.stopPropagation()}>
            <h2>Detalhes da Entrada</h2>
            <div className="detail-grid" style={{ marginBottom: 16 }}>
              <div><strong>NF:</strong> {detailEntry.invoiceNumber}</div>
              <div><strong>Fornecedor:</strong> {detailEntry.supplierName}</div>
              <div><strong>Data:</strong> {new Date(detailEntry.receivedAt).toLocaleDateString('pt-BR')}</div>
              <div><strong>Total:</strong> R$ {detailEntry.totalCost.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</div>
              {detailEntry.notes && (
                <div style={{ gridColumn: '1 / -1' }}>
                  <strong>Obs:</strong> {detailEntry.notes}
                </div>
              )}
            </div>
            <table className="table">
              <thead>
                <tr>
                  <th>Produto</th>
                  <th>Barcode</th>
                  <th>Qtd</th>
                  <th>Custo Unit.</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {detailEntry.items.map((item) => (
                  <tr key={item.id}>
                    <td>{item.productName}</td>
                    <td className="font-mono">{item.barcode}</td>
                    <td>{item.quantity}</td>
                    <td>R$ {item.unitCost.toFixed(2)}</td>
                    <td className="font-semibold">R$ {item.totalCost.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setDetailEntry(null)}>
                Fechar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
