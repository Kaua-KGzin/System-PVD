import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Edit2, Tag } from 'lucide-react'
import { api } from '../api/client'
import type { Category } from '../types'

export default function CategoriesPage() {
  const qc = useQueryClient()
  const [includeInactive, setIncludeInactive] = useState(false)
  const [showModal, setShowModal] = useState(false)
  const [editing, setEditing] = useState<Category | null>(null)
  const [form, setForm] = useState({ name: '', description: '' })
  const [isActive, setIsActive] = useState(true)
  const [error, setError] = useState('')

  const { data: categories, isLoading } = useQuery<Category[]>({
    queryKey: ['categories', includeInactive],
    queryFn: () =>
      api.get('/categories', { params: { includeInactive } }).then((r) => r.data),
  })

  const saveMutation = useMutation({
    mutationFn: () => {
      if (editing) {
        return api.put(`/categories/${editing.id}`, {
          name: form.name,
          description: form.description || null,
          isActive,
        })
      }
      return api.post('/categories', {
        name: form.name,
        description: form.description || null,
      })
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['categories'] })
      setShowModal(false)
      setEditing(null)
      setError('')
    },
    onError: (e: any) => {
      setError(e.response?.data?.detail ?? 'Erro ao salvar categoria.')
    },
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ name: '', description: '' })
    setIsActive(true)
    setError('')
    setShowModal(true)
  }

  const openEdit = (c: Category) => {
    setEditing(c)
    setForm({ name: c.name, description: c.description ?? '' })
    setIsActive(c.isActive)
    setError('')
    setShowModal(true)
  }

  const active = categories?.filter((c) => c.isActive) ?? []
  const inactive = categories?.filter((c) => !c.isActive) ?? []

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Categorias</h1>
        <button className="btn-primary" onClick={openCreate}>
          <Plus size={16} /> Nova categoria
        </button>
      </div>

      <div className="filters-row" style={{ marginBottom: 16 }}>
        <label className="toggle-label">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(e) => setIncludeInactive(e.target.checked)}
          />
          Mostrar inativas
        </label>
        <span className="text-muted" style={{ fontSize: 13 }}>
          {active.length} ativa{active.length !== 1 ? 's' : ''}
          {includeInactive && inactive.length > 0 && ` · ${inactive.length} inativa${inactive.length !== 1 ? 's' : ''}`}
        </span>
      </div>

      {isLoading ? (
        <p className="loading">Carregando...</p>
      ) : !categories?.length ? (
        <div className="card">
          <p className="empty-text">Nenhuma categoria cadastrada.</p>
        </div>
      ) : (
        <div className="categories-grid">
          {categories.map((c) => (
            <div key={c.id} className={`category-card ${!c.isActive ? 'category-inactive' : ''}`}>
              <div className="category-icon">
                <Tag size={20} />
              </div>
              <div className="category-info">
                <p className="category-name">{c.name}</p>
                {c.description && <p className="category-desc">{c.description}</p>}
              </div>
              <div className="category-right">
                {c.isActive ? (
                  <span className="badge badge-open">Ativa</span>
                ) : (
                  <span className="badge badge-cancelled">Inativa</span>
                )}
                <button className="btn-icon" onClick={() => openEdit(c)} title="Editar">
                  <Edit2 size={15} />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>{editing ? 'Editar categoria' : 'Nova categoria'}</h2>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Nome *</label>
              <input
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                placeholder="Ex: Bebidas, Laticínios..."
                autoFocus
              />
            </div>
            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Descrição</label>
              <input
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                placeholder="Opcional"
              />
            </div>
            {editing && (
              <div className="form-group" style={{ marginBottom: 12 }}>
                <label className="toggle-label">
                  <input
                    type="checkbox"
                    checked={isActive}
                    onChange={(e) => setIsActive(e.target.checked)}
                  />
                  Categoria ativa
                </label>
              </div>
            )}

            {error && <p className="login-error" style={{ marginTop: 4 }}>{error}</p>}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
              <button
                className="btn-primary"
                onClick={() => saveMutation.mutate()}
                disabled={!form.name.trim() || saveMutation.isPending}
              >
                {saveMutation.isPending ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
