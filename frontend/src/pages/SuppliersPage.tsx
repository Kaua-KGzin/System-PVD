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
  const [form, setForm] = useState({ name: '', cnpj: '', contactName: '', phone: '', email: '' })

  const { data, isLoading } = useQuery<PagedResponse<Supplier>>({
    queryKey: ['suppliers', search, page],
    queryFn: () => api.get('/suppliers', { params: { search, page, pageSize: 15 } }).then((r) => r.data),
  })

  const saveMutation = useMutation({
    // Awaited rather than returned: create and update send different bodies (only the update
    // contract carries isActive), and returning both responses makes the mutation's type the
    // union of two AxiosResponse request-body shapes. Nothing here reads the response.
    mutationFn: async () => {
      const body = {
        name: form.name,
        cnpj: form.cnpj || null,
        contactName: form.contactName || null,
        phone: form.phone || null,
        email: form.email || null,
      }
      if (editing) {
        await api.put(`/suppliers/${editing.id}`, { ...body, isActive: true })
        return
      }
      await api.post('/suppliers', body)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['suppliers'] })
      setShowModal(false)
      setEditing(null)
    },
  })

  const openCreate = () => {
    setEditing(null)
    setForm({ name: '', cnpj: '', contactName: '', phone: '', email: '' })
    setShowModal(true)
  }

  const openEdit = (s: Supplier) => {
    setEditing(s)
    setForm({
      name: s.name,
      cnpj: s.cnpj ?? '',
      contactName: s.contactName ?? '',
      phone: s.phone ?? '',
      email: s.email ?? '',
    })
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
        <input
          placeholder="Buscar fornecedor..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
      </div>

      <div className="card">
        {isLoading ? <p className="loading">Carregando...</p> : (
          <table className="table">
            <thead>
              <tr><th>Nome</th><th>CNPJ</th><th>Contato</th><th>Telefone</th><th>Email</th><th>Ações</th></tr>
            </thead>
            <tbody>
              {data?.items.map((s) => (
                <tr key={s.id}>
                  <td>{s.name}</td>
                  <td className="font-mono text-sm">{s.cnpj ?? '—'}</td>
                  <td>{s.contactName ?? '—'}</td>
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
              {([['name', 'Razão social *'], ['cnpj', 'CNPJ'], ['contactName', 'Contato'], ['phone', 'Telefone'], ['email', 'E-mail']] as [keyof typeof form, string][]).map(([key, label]) => (
                <div className="form-group" key={key}>
                  <label>{label}</label>
                  <input value={form[key]} onChange={(e) => setForm((f) => ({ ...f, [key]: e.target.value }))} />
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
