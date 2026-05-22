import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Edit2, Search } from 'lucide-react'
import { api } from '../api/client'
import type { PagedResponse, Supplier } from '../types'

export default function SuppliersPage() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [showModal, setShowModal] = useState(false)
  const [editing, setEditing] = useState<Supplier | null>(null)
  const [form, setForm] = useState({ name: '', tradeName: '', document: '', phone: '', email: '' })

  const { data, isLoading } = useQuery<PagedResponse<Supplier>>({
    queryKey: ['suppliers', search, page],
    queryFn: () => api.get('/suppliers', { params: { search, page, pageSize: 15 } }).then((r) => r.data),
  })

  const saveMutation = useMutation({
    mutationFn: () => {
      const body = { ...form, tradeName: form.tradeName || null, document: form.document || null, phone: form.phone || null, email: form.email || null }
      return editing ? api.put(`/suppliers/${editing.id}`, { ...body, isActive: true }) : api.post('/suppliers', body)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['suppliers'] })
      setShowModal(false)
      setEditing(null)
    },
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ name: '', tradeName: '', document: '', phone: '', email: '' })
    setShowModal(true)
  }

  const openEdit = (s: Supplier) => {
    setEditing(s)
    setForm({ name: s.name, tradeName: s.tradeName ?? '', document: s.document ?? '', phone: s.phone ?? '', email: s.email ?? '' })
    setShowModal(true)
  }

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Fornecedores</h1>
        <button className="btn-primary" onClick={openCreate}><Plus size={16} /> Novo fornecedor</button>
      </div>

      <div className="search-bar">
        <Search size={16} className="search-icon" />
        <input placeholder="Buscar fornecedor..." value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
      </div>

      <div className="card">
        {isLoading ? <p className="loading">Carregando...</p> : (
          <table className="table">
            <thead><tr><th>Nome</th><th>Nome fantasia</th><th>CNPJ/CPF</th><th>Telefone</th><th>Email</th><th>Ações</th></tr></thead>
            <tbody>
              {data?.items.map((s) => (
                <tr key={s.id}>
                  <td>{s.name}</td>
                  <td>{s.tradeName ?? '—'}</td>
                  <td className="font-mono text-sm">{s.document ?? '—'}</td>
                  <td>{s.phone ?? '—'}</td>
                  <td>{s.email ?? '—'}</td>
                  <td><button className="btn-icon" onClick={() => openEdit(s)}><Edit2 size={15} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <div className="pagination">
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</button>
          <span>Página {data?.page} de {data?.totalPages ?? 1}</span>
          <button disabled={!data || page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Próxima</button>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>{editing ? 'Editar fornecedor' : 'Novo fornecedor'}</h2>
            <div className="form-grid">
              {[['name', 'Razão social *'], ['tradeName', 'Nome fantasia'], ['document', 'CNPJ / CPF'], ['phone', 'Telefone'], ['email', 'E-mail']].map(([key, label]) => (
                <div className="form-group" key={key}>
                  <label>{label}</label>
                  <input value={(form as any)[key]} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))} />
                </div>
              ))}
            </div>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
              <button className="btn-primary" onClick={() => saveMutation.mutate()} disabled={!form.name.trim() || saveMutation.isPending}>
                {saveMutation.isPending ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
