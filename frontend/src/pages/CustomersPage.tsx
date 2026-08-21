import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Edit2, Search, UserCheck, UserX } from 'lucide-react'
import { api } from '../api/client'
import { errorDetail } from '../api/errors'
import type { PagedResponse, Customer } from '../types'

export default function CustomersPage() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const [editing, setEditing] = useState<Customer | null>(null)
  const [form, setForm] = useState({ name: '', document: '', phone: '', email: '' })
  const [isActive, setIsActive] = useState(true)
  const [error, setError] = useState('')

  const { data, isLoading } = useQuery<PagedResponse<Customer>>({
    queryKey: ['customers', search, includeInactive, page],
    queryFn: () =>
      api.get('/customers', { params: { search, includeInactive, page, pageSize: 15 } }).then((r) => r.data),
  })

  const saveMutation = useMutation({
    // Awaited rather than returned: create and update send different bodies (only the update
    // contract carries isActive), and returning both responses makes the mutation's type the
    // union of two AxiosResponse request-body shapes. Nothing here reads the response.
    mutationFn: async () => {
      const body = {
        name: form.name,
        document: form.document || null,
        phone: form.phone || null,
        email: form.email || null,
      }
      if (editing) {
        await api.put(`/customers/${editing.id}`, { ...body, isActive })
        return
      }
      await api.post('/customers', body)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['customers'] })
      setShowModal(false)
      setEditing(null)
      setError('')
    },
    onError: (e) => {
      setError(errorDetail(e, 'Erro ao salvar cliente.'))
    },
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ name: '', document: '', phone: '', email: '' })
    setIsActive(true)
    setError('')
    setShowModal(true)
  }

  const openEdit = (c: Customer) => {
    setEditing(c)
    setForm({
      name: c.name,
      document: c.document ?? '',
      phone: c.phone ?? '',
      email: c.email ?? '',
    })
    setIsActive(c.isActive)
    setError('')
    setShowModal(true)
  }

  const fmtDate = (s: string) =>
    new Date(s).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Clientes</h1>
        <button className="btn-primary" onClick={openCreate}>
          <Plus size={16} /> Novo cliente
        </button>
      </div>

      <div className="filters-row">
        <div className="search-bar" style={{ flex: 1 }}>
          <Search size={16} className="search-icon" />
          <input
            placeholder="Buscar por nome ou CPF/CNPJ..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1) }}
          />
        </div>
        <label className="toggle-label">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(e) => { setIncludeInactive(e.target.checked); setPage(1) }}
          />
          Incluir inativos
        </label>
      </div>

      <div className="card">
        {isLoading ? (
          <p className="loading">Carregando...</p>
        ) : !data?.items.length ? (
          <p className="empty-text">Nenhum cliente encontrado.</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Nome</th>
                <th>CPF / CNPJ</th>
                <th>Telefone</th>
                <th>E-mail</th>
                <th>Status</th>
                <th>Cadastro</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((c) => (
                <tr key={c.id} className={!c.isActive ? 'row-inactive' : ''}>
                  <td className="font-semibold">{c.name}</td>
                  <td className="font-mono text-sm">{c.document ?? '—'}</td>
                  <td>{c.phone ?? '—'}</td>
                  <td>{c.email ?? '—'}</td>
                  <td>
                    {c.isActive ? (
                      <span className="badge badge-open">Ativo</span>
                    ) : (
                      <span className="badge badge-cancelled">Inativo</span>
                    )}
                  </td>
                  <td>{fmtDate(c.createdAt)}</td>
                  <td className="actions">
                    <button className="btn-icon" onClick={() => openEdit(c)} title="Editar">
                      <Edit2 size={15} />
                    </button>
                    {c.isActive ? (
                      <UserX size={15} className="text-muted" style={{ opacity: 0.4, margin: '0 4px' }} />
                    ) : (
                      <UserCheck size={15} className="text-muted" style={{ opacity: 0.4, margin: '0 4px' }} />
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <div className="pagination">
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</button>
          <span>
            Página {data?.page ?? 1} de {data?.totalPages ?? 1} — {data?.totalCount ?? 0} clientes
          </span>
          <button disabled={!data || page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
            Próxima
          </button>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>{editing ? 'Editar cliente' : 'Novo cliente'}</h2>

            <div className="form-grid">
              <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                <label>Nome *</label>
                <input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  placeholder="Nome completo ou razão social"
                />
              </div>
              <div className="form-group">
                <label>CPF / CNPJ</label>
                <input
                  value={form.document}
                  onChange={(e) => setForm((f) => ({ ...f, document: e.target.value }))}
                  placeholder="000.000.000-00"
                />
              </div>
              <div className="form-group">
                <label>Telefone</label>
                <input
                  value={form.phone}
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                  placeholder="(11) 99999-0000"
                />
              </div>
              <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                <label>E-mail</label>
                <input
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                  placeholder="email@exemplo.com"
                />
              </div>
              {editing && (
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label className="toggle-label">
                    <input
                      type="checkbox"
                      checked={isActive}
                      onChange={(e) => setIsActive(e.target.checked)}
                    />
                    Cliente ativo
                  </label>
                </div>
              )}
            </div>

            {error && <p className="login-error" style={{ marginTop: 8 }}>{error}</p>}

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
