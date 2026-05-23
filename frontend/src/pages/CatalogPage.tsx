import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Search, Edit2, BarChart2 } from 'lucide-react'
import { api } from '../api/client'
import type { PagedResponse, Product, Category } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

export default function CatalogPage() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const [editing, setEditing] = useState<Product | null>(null)
  const [adjustId, setAdjustId] = useState<string | null>(null)
  const [adjustDelta, setAdjustDelta] = useState('')
  const [adjustNote, setAdjustNote] = useState('')

  const [form, setForm] = useState({ barcode: '', name: '', unitOfMeasure: 'UN', unitPrice: '', stockQuantity: '', minStockQuantity: '', categoryId: '' })

  const { data } = useQuery<PagedResponse<Product>>({
    queryKey: ['products', search, page],
    queryFn: () => api.get('/products', { params: { search, page, pageSize: 15 } }).then((r) => r.data),
  })

  const { data: categories } = useQuery<Category[]>({
    queryKey: ['categories'],
    queryFn: () => api.get('/categories').then((r) => r.data),
  })

  const saveMutation = useMutation({
    mutationFn: () => {
      const body = {
        barcode: form.barcode,
        sku: null,
        name: form.name,
        unitOfMeasure: form.unitOfMeasure,
        unitPrice: parseFloat(form.unitPrice),
        stockQuantity: parseFloat(form.stockQuantity),
        minStockQuantity: parseFloat(form.minStockQuantity),
        categoryId: form.categoryId || null,
      }
      return editing
        ? api.put(`/products/${editing.id}`, body)
        : api.post('/products', body)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['products'] })
      setShowModal(false)
      setEditing(null)
    },
  })

  const adjustMutation = useMutation({
    mutationFn: () =>
      api.post(`/products/${adjustId}/stock-adjustments`, { delta: parseFloat(adjustDelta), notes: adjustNote }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['products'] })
      setAdjustId(null)
      setAdjustDelta('')
      setAdjustNote('')
    },
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ barcode: '', name: '', unitOfMeasure: 'UN', unitPrice: '', stockQuantity: '', minStockQuantity: '', categoryId: '' })
    setShowModal(true)
  }

  const openEdit = (p: Product) => {
    setEditing(p)
    setForm({
      barcode: p.barcode,
      name: p.name,
      unitOfMeasure: p.unitOfMeasure,
      unitPrice: String(p.unitPrice),
      stockQuantity: String(p.stockQuantity),
      minStockQuantity: String(p.minStockQuantity),
      categoryId: p.categoryId ?? '',
    })
    setShowModal(true)
  }

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Catálogo de Produtos</h1>
        <button className="btn-primary" onClick={openCreate}><Plus size={16} /> Novo produto</button>
      </div>

      <div className="search-bar">
        <Search size={16} className="search-icon" />
        <input
          placeholder="Buscar por nome ou código..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
      </div>

      <div className="card">
        <table className="table">
          <thead>
            <tr>
              <th>Código</th><th>Nome</th><th>Un.</th><th>Preço</th><th>Estoque</th><th>Mín.</th><th>Ações</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((p) => (
              <tr key={p.id} className={p.stockQuantity <= p.minStockQuantity ? 'row-warning' : ''}>
                <td className="font-mono text-sm">{p.barcode}</td>
                <td>{p.name}</td>
                <td>{p.unitOfMeasure}</td>
                <td>{fmt(p.unitPrice)}</td>
                <td className={p.stockQuantity <= p.minStockQuantity ? 'text-red-600 font-semibold' : ''}>{p.stockQuantity}</td>
                <td>{p.minStockQuantity}</td>
                <td className="actions">
                  <button className="btn-icon" onClick={() => openEdit(p)} title="Editar"><Edit2 size={15} /></button>
                  <button className="btn-icon" onClick={() => setAdjustId(p.id)} title="Ajustar estoque"><BarChart2 size={15} /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="pagination">
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</button>
          <span>Página {data?.page} de {data?.totalPages ?? 1}</span>
          <button disabled={!data || page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Próxima</button>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>{editing ? 'Editar produto' : 'Novo produto'}</h2>
            <div className="form-grid">
              {['barcode:Código de barras', 'name:Nome', 'unitOfMeasure:Unidade', 'unitPrice:Preço', 'stockQuantity:Estoque', 'minStockQuantity:Estoque mínimo'].map((field) => {
                const [key, label] = field.split(':')
                return (
                  <div className="form-group" key={key}>
                    <label>{label}</label>
                    <input
                      value={(form as any)[key]}
                      onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))}
                      type={['unitPrice', 'stockQuantity', 'minStockQuantity'].includes(key) ? 'number' : 'text'}
                      step="0.01"
                    />
                  </div>
                )
              })}
              <div className="form-group">
                <label>Categoria</label>
                <select value={form.categoryId} onChange={(e) => setForm((f) => ({ ...f, categoryId: e.target.value }))}>
                  <option value="">Sem categoria</option>
                  {categories?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
            </div>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
              <button className="btn-primary" onClick={() => saveMutation.mutate()} disabled={saveMutation.isPending}>
                {saveMutation.isPending ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {adjustId && (
        <div className="modal-overlay" onClick={() => setAdjustId(null)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Ajustar estoque</h2>
            <div className="form-group">
              <label>Delta (positivo = entrada, negativo = saída)</label>
              <input type="number" value={adjustDelta} onChange={(e) => setAdjustDelta(e.target.value)} placeholder="Ex: 10 ou -5" />
            </div>
            <div className="form-group">
              <label>Motivo</label>
              <input value={adjustNote} onChange={(e) => setAdjustNote(e.target.value)} placeholder="Motivo do ajuste" />
            </div>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setAdjustId(null)}>Cancelar</button>
              <button className="btn-primary" onClick={() => adjustMutation.mutate()} disabled={adjustMutation.isPending}>
                Aplicar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
