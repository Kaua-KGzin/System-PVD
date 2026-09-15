import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Percent, DollarSign, Search, Plus, Edit2, CheckCircle, XCircle } from 'lucide-react'
import { api } from '../api/client'
import type { SellerCommission, CommissionSummary } from '../types'

export default function CommissionPage() {
  const qc = useQueryClient()
  const [selectedCommission, setSelectedCommission] = useState<SellerCommission | null>(null)
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [showSummaryModal, setShowSummaryModal] = useState(false)
  const [createForm, setCreateForm] = useState({ userId: '', percentage: '' })
  const [editForm, setEditForm] = useState({ percentage: '', isActive: true })
  const [summaryUserId, setSummaryUserId] = useState<string>('')
  const [summaryPeriod, setSummaryPeriod] = useState({ start: '', end: '' })

  const { data: commissionsData, isLoading } = useQuery({
    queryKey: ['commissions'],
    queryFn: () => api.get('/commissions').then((r) => r.data),
  })

  const { data: usersData } = useQuery({
    queryKey: ['users-list'],
    queryFn: () => api.get('/users', { params: { pageSize: 100 } }).then((r) => r.data),
  })

  const { data: summaryData, isLoading: summaryLoading } = useQuery({
    queryKey: ['commission-summary', summaryUserId, summaryPeriod],
    queryFn: () => {
      const params: Record<string, string> = {}
      if (summaryPeriod.start) params.startDate = summaryPeriod.start
      if (summaryPeriod.end) params.endDate = summaryPeriod.end
      return api.get(`/commissions/summary/${summaryUserId}`, { params }).then((r) => r.data)
    },
    enabled: !!summaryUserId,
  })

  const createMutation = useMutation({
    mutationFn: () =>
      api.post('/commissions', {
        userId: createForm.userId,
        percentage: parseFloat(createForm.percentage),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['commissions'] })
      setShowCreateModal(false)
      setCreateForm({ userId: '', percentage: '' })
    },
  })

  const updateMutation = useMutation({
    mutationFn: () =>
      api.put(`/commissions/${selectedCommission?.id}`, {
        percentage: parseFloat(editForm.percentage),
        isActive: editForm.isActive,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['commissions'] })
      setShowEditModal(false)
      setSelectedCommission(null)
    },
  })

  const commissions: SellerCommission[] = commissionsData?.items ?? []
  const users: { id: string; username: string; role: string }[] = usersData?.items ?? []
  const summary: CommissionSummary | null = summaryData ?? null

  const openEditModal = (commission: SellerCommission) => {
    setSelectedCommission(commission)
    setEditForm({ percentage: commission.percentage.toString(), isActive: commission.isActive })
    setShowEditModal(true)
  }

  const openSummaryModal = (commission: SellerCommission) => {
    setSelectedCommission(commission)
    setSummaryUserId(commission.userId)
    setSummaryPeriod({ start: '', end: '' })
    setShowSummaryModal(true)
  }

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Comissoes de Vendedores</h1>
        <button className="btn-primary" onClick={() => setShowCreateModal(true)}>
          <Plus size={16} /> Nova Comissao
        </button>
      </div>

      <div className="card">
        {isLoading ? (
          <p className="loading">Carregando...</p>
        ) : !commissions.length ? (
          <p className="empty-text">Nenhuma configuracao de comissao encontrada.</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Vendedor</th>
                <th>Comissao (%)</th>
                <th>Status</th>
                <th>Criado em</th>
                <th>Acoes</th>
              </tr>
            </thead>
            <tbody>
              {commissions.map((c) => (
                <tr key={c.id}>
                  <td className="font-semibold">{c.username}</td>
                  <td>
                    <span style={{ color: '#16a34a', fontWeight: 700 }}>{c.percentage}%</span>
                  </td>
                  <td>
                    <span
                      className="badge"
                      style={{
                        background: c.isActive ? '#dcfce7' : '#fee2e2',
                        color: c.isActive ? '#166534' : '#991b1b',
                      }}
                    >
                      {c.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                  </td>
                  <td className="text-muted">
                    {new Date(c.createdAt).toLocaleDateString('pt-BR')}
                  </td>
                  <td>
                    <button
                      className="btn-icon"
                      onClick={() => openSummaryModal(c)}
                      title="Ver resumo"
                    >
                      <DollarSign size={15} />
                    </button>
                    <button
                      className="btn-icon"
                      onClick={() => openEditModal(c)}
                      title="Editar"
                    >
                      <Edit2 size={15} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showCreateModal && (
        <div className="modal-overlay" onClick={() => setShowCreateModal(false)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Nova Comissao</h2>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Vendedor *</label>
              <select
                value={createForm.userId}
                onChange={(e) => setCreateForm((f) => ({ ...f, userId: e.target.value }))}
              >
                <option value="">Selecione um vendedor</option>
                {users
                  .filter((u) => u.role === 'Operator' || u.role === 'Manager')
                  .map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.username} ({u.role})
                    </option>
                  ))}
              </select>
            </div>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Percentual de comissao (%) *</label>
              <input
                type="number"
                min={0}
                max={100}
                step={0.5}
                value={createForm.percentage}
                onChange={(e) => setCreateForm((f) => ({ ...f, percentage: e.target.value }))}
                placeholder="Ex: 5.00"
              />
            </div>

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowCreateModal(false)}>
                Cancelar
              </button>
              <button
                className="btn-primary"
                onClick={() => createMutation.mutate()}
                disabled={!createForm.userId || !createForm.percentage || createMutation.isPending}
              >
                {createMutation.isPending ? 'Criando...' : 'Criar Comissao'}
              </button>
            </div>
          </div>
        </div>
      )}

      {showEditModal && selectedCommission && (
        <div className="modal-overlay" onClick={() => setShowEditModal(false)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Editar Comissao</h2>
            <p className="text-muted" style={{ marginBottom: 16 }}>
              Vendedor: <strong>{selectedCommission.username}</strong>
            </p>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Percentual de comissao (%) *</label>
              <input
                type="number"
                min={0}
                max={100}
                step={0.5}
                value={editForm.percentage}
                onChange={(e) => setEditForm((f) => ({ ...f, percentage: e.target.value }))}
              />
            </div>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Status</label>
              <select
                value={editForm.isActive ? 'active' : 'inactive'}
                onChange={(e) => setEditForm((f) => ({ ...f, isActive: e.target.value === 'active' }))}
              >
                <option value="active">Ativo</option>
                <option value="inactive">Inativo</option>
              </select>
            </div>

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowEditModal(false)}>
                Cancelar
              </button>
              <button
                className="btn-primary"
                onClick={() => updateMutation.mutate()}
                disabled={!editForm.percentage || updateMutation.isPending}
              >
                {updateMutation.isPending ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {showSummaryModal && selectedCommission && (
        <div className="modal-overlay" onClick={() => setShowSummaryModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Resumo de Comissoes</h2>
            <p className="text-muted" style={{ marginBottom: 16 }}>
              Vendedor: <strong>{selectedCommission.username}</strong> | Taxa: <strong>{selectedCommission.percentage}%</strong>
            </p>

            <div className="form-row" style={{ marginBottom: 16 }}>
              <div className="form-group" style={{ flex: 1 }}>
                <label>Data inicial</label>
                <input
                  type="date"
                  value={summaryPeriod.start}
                  onChange={(e) => setSummaryPeriod((p) => ({ ...p, start: e.target.value }))}
                />
              </div>
              <div className="form-group" style={{ flex: 1 }}>
                <label>Data final</label>
                <input
                  type="date"
                  value={summaryPeriod.end}
                  onChange={(e) => setSummaryPeriod((p) => ({ ...p, end: e.target.value }))}
                />
              </div>
            </div>

            {summaryLoading ? (
              <p className="loading">Carregando resumo...</p>
            ) : summary ? (
              <div>
                <div className="stats-grid" style={{ marginBottom: 16 }}>
                  <div className="stat-card">
                    <div className="stat-icon"><DollarSign size={22} style={{ color: '#16a34a' }} /></div>
                    <div>
                      <p className="stat-label">Total Comissao</p>
                      <p className="stat-value">R$ {summary.totalCommission.toFixed(2)}</p>
                    </div>
                  </div>
                  <div className="stat-card">
                    <div className="stat-icon"><CheckCircle size={22} style={{ color: '#16a34a' }} /></div>
                    <div>
                      <p className="stat-label">Pago</p>
                      <p className="stat-value">R$ {summary.paidCommission.toFixed(2)}</p>
                    </div>
                  </div>
                  <div className="stat-card">
                    <div className="stat-icon"><XCircle size={22} style={{ color: '#f59e0b' }} /></div>
                    <div>
                      <p className="stat-label">Pendente</p>
                      <p className="stat-value">R$ {summary.pendingCommission.toFixed(2)}</p>
                    </div>
                  </div>
                  <div className="stat-card">
                    <div className="stat-icon"><Percent size={22} style={{ color: '#3b82f6' }} /></div>
                    <div>
                      <p className="stat-label">Vendas</p>
                      <p className="stat-value">{summary.totalSales}</p>
                    </div>
                  </div>
                </div>

                {summary.recentTransactions.length > 0 && (
                  <div>
                    <h3 className="card-title" style={{ marginBottom: 8 }}>Transacoes Recentes</h3>
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Venda</th>
                          <th>Valor</th>
                          <th>Comissao</th>
                          <th>Status</th>
                          <th>Data</th>
                        </tr>
                      </thead>
                      <tbody>
                        {summary.recentTransactions.map((t) => (
                          <tr key={t.id}>
                            <td>#{t.saleNumber}</td>
                            <td>R$ {t.saleAmount.toFixed(2)}</td>
                            <td className="font-semibold" style={{ color: '#16a34a' }}>
                              R$ {t.commissionAmount.toFixed(2)}
                            </td>
                            <td>
                              <span
                                className="badge"
                                style={{
                                  background: t.isPaid ? '#dcfce7' : '#fef3c7',
                                  color: t.isPaid ? '#166534' : '#92400e',
                                }}
                              >
                                {t.isPaid ? 'Pago' : 'Pendente'}
                              </span>
                            </td>
                            <td className="text-muted">
                              {new Date(t.createdAt).toLocaleDateString('pt-BR')}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            ) : (
              <p className="empty-text">Selecione um periodo para ver o resumo.</p>
            )}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowSummaryModal(false)}>
                Fechar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
