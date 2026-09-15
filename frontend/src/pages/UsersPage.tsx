import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, UserCheck, UserX } from 'lucide-react'
import { api } from '../api/client'
import { errorDetail } from '../api/errors'
import type { User } from '../types'

export default function UsersPage() {
  const qc = useQueryClient()
  const [showModal, setShowModal] = useState(false)
  const [form, setForm] = useState({ username: '', password: '', role: 'Operator' })
  const [error, setError] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => api.get('/users').then((r) => r.data),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      api.post('/users', {
        username: form.username,
        password: form.password,
        role: form.role,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
      setShowModal(false)
      setForm({ username: '', password: '', role: 'Operator' })
      setError('')
    },
    onError: (e) => {
      setError(errorDetail(e, 'Erro ao criar usuario.'))
    },
  })

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      isActive
        ? api.post(`/users/${id}/deactivate`)
        : api.post(`/users/${id}/reactivate`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
    },
  })

  const users: User[] = data?.items ?? []

  const getRoleBadge = (role: string) => {
    const colors: Record<string, { bg: string; color: string }> = {
      Admin: { bg: '#fef3c7', color: '#92400e' },
      Manager: { bg: '#dbeafe', color: '#1e40af' },
      Operator: { bg: '#dcfce7', color: '#166534' },
    }
    const style = colors[role] ?? { bg: '#f3f4f6', color: '#374151' }
    return (
      <span className="badge" style={{ background: style.bg, color: style.color }}>
        {role}
      </span>
    )
  }

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Usuarios</h1>
        <button
          className="btn-primary"
          onClick={() => {
            setForm({ username: '', password: '', role: 'Operator' })
            setError('')
            setShowModal(true)
          }}
        >
          <Plus size={16} /> Novo usuario
        </button>
      </div>

      {isLoading ? (
        <p className="loading">Carregando...</p>
      ) : !users.length ? (
        <div className="card">
          <p className="empty-text">Nenhum usuario cadastrado.</p>
        </div>
      ) : (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Usuario</th>
                <th>Perfil</th>
                <th>Status</th>
                <th>Criado em</th>
                <th style={{ width: 80 }}>Acoes</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id} className={!u.isActive ? 'row-inactive' : ''}>
                  <td className="font-semibold">{u.username}</td>
                  <td>{getRoleBadge(u.role)}</td>
                  <td>
                    {u.isActive ? (
                      <span className="badge badge-completed">Ativo</span>
                    ) : (
                      <span className="badge badge-cancelled">Inativo</span>
                    )}
                  </td>
                  <td className="text-muted">
                    {new Date(u.createdAt).toLocaleDateString('pt-BR')}
                  </td>
                  <td>
                    <div className="actions">
                      {u.isActive ? (
                        <button
                          className="btn-icon-danger"
                          onClick={() => toggleActiveMutation.mutate({ id: u.id, isActive: true })}
                          title="Desativar"
                          disabled={toggleActiveMutation.isPending}
                        >
                          <UserX size={15} />
                        </button>
                      ) : (
                        <button
                          className="btn-icon"
                          onClick={() => toggleActiveMutation.mutate({ id: u.id, isActive: false })}
                          title="Reativar"
                          disabled={toggleActiveMutation.isPending}
                        >
                          <UserCheck size={15} />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Novo usuario</h2>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Username *</label>
              <input
                value={form.username}
                onChange={(e) => setForm((f) => ({ ...f, username: e.target.value }))}
                placeholder="Nome do usuario"
                autoFocus
              />
            </div>
            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Senha *</label>
              <input
                type="password"
                value={form.password}
                onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
                placeholder="Minimo 12 caracteres"
              />
            </div>
            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Perfil *</label>
              <select
                value={form.role}
                onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))}
              >
                <option value="Operator">Operator</option>
                <option value="Manager">Manager</option>
                <option value="Admin">Admin</option>
              </select>
            </div>

            {error && <p className="login-error" style={{ marginTop: 4 }}>{error}</p>}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowModal(false)}>
                Cancelar
              </button>
              <button
                className="btn-primary"
                onClick={() => createMutation.mutate()}
                disabled={!form.username.trim() || form.password.length < 12 || createMutation.isPending}
              >
                {createMutation.isPending ? 'Criando...' : 'Criar usuario'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
