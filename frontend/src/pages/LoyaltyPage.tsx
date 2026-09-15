import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Star, Gift, Search } from 'lucide-react'
import { api } from '../api/client'
import type { CustomerLoyalty, Customer, LoyaltyTransaction } from '../types'

export default function LoyaltyPage() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerLoyalty | null>(null)
  const [redeemPoints, setRedeemPoints] = useState('')
  const [redeemNotes, setRedeemNotes] = useState('')
  const [showRedeemModal, setShowRedeemModal] = useState(false)

  const { data: loyaltyData, isLoading } = useQuery({
    queryKey: ['loyalty'],
    queryFn: () => api.get('/loyalty').then((r) => r.data),
  })

  const { data: customersData } = useQuery({
    queryKey: ['customers-search', search],
    queryFn: () => api.get('/customers', { params: { search, pageSize: 10 } }).then((r) => r.data),
    enabled: search.length >= 2,
  })

  const { data: customerLoyalty } = useQuery({
    queryKey: ['customer-loyalty', selectedCustomer?.customerId],
    queryFn: () =>
      api.get(`/loyalty/customer/${selectedCustomer?.customerId}`).then((r) => r.data),
    enabled: !!selectedCustomer?.customerId,
  })

  const redeemMutation = useMutation({
    mutationFn: () =>
      api.post(`/loyalty/customer/${selectedCustomer?.customerId}/redeem`, {
        points: parseInt(redeemPoints),
        notes: redeemNotes || null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['loyalty'] })
      qc.invalidateQueries({ queryKey: ['customer-loyalty'] })
      setShowRedeemModal(false)
      setRedeemPoints('')
      setRedeemNotes('')
    },
  })

  const loyaltyList: CustomerLoyalty[] = loyaltyData?.items ?? []
  const customers: Customer[] = customersData?.items ?? []

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Programa de Fidelidade</h1>
      </div>

      <div className="card mb-4">
        <div className="card-title" style={{ marginBottom: 12 }}>Buscar cliente</div>
        <div className="search-bar" style={{ marginBottom: 0 }}>
          <Search size={16} className="search-icon" />
          <input
            placeholder="Buscar por nome ou documento..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        {customers.length > 0 && (
          <div className="search-results" style={{ border: '1px solid var(--border)', borderRadius: 8, marginTop: 8, maxHeight: 200, overflowY: 'auto' }}>
            {customers.map((c) => (
              <div
                key={c.id}
                style={{ padding: '10px 12px', cursor: 'pointer', borderBottom: '1px solid var(--border)' }}
                onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--bg)')}
                onMouseLeave={(e) => (e.currentTarget.style.background = '')}
                onClick={() => {
                  setSelectedCustomer(loyaltyList.find((l) => l.customerId === c.id) ?? null)
                  setSearch('')
                }}
              >
                <span className="font-semibold">{c.name}</span>
                <span className="text-muted" style={{ marginLeft: 8 }}>
                  {c.document ?? 'Sem documento'}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

      {selectedCustomer && customerLoyalty && (
        <div className="card mb-4">
          <div className="page-header" style={{ marginBottom: 16 }}>
            <div>
              <h2 className="card-title">{customerLoyalty.customerName}</h2>
              <p className="text-muted">Programa de Fidelidade</p>
            </div>
            <button
              className="btn-primary"
              onClick={() => setShowRedeemModal(true)}
              disabled={customerLoyalty.availablePoints <= 0}
            >
              <Gift size={16} /> Resgatar Pontos
            </button>
          </div>

          <div className="stats-grid" style={{ marginBottom: 16 }}>
            <div className="stat-card">
              <div className="stat-icon"><Star size={22} style={{ color: '#f59e0b' }} /></div>
              <div>
                <p className="stat-label">Pontos Disponiveis</p>
                <p className="stat-value" style={{ color: '#f59e0b' }}>{customerLoyalty.availablePoints}</p>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-icon"><Star size={22} style={{ color: '#16a34a' }} /></div>
              <div>
                <p className="stat-label">Total Acumulado</p>
                <p className="stat-value">{customerLoyalty.totalPoints}</p>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-icon"><Gift size={22} style={{ color: '#dc2626' }} /></div>
              <div>
                <p className="stat-label">Resgatados</p>
                <p className="stat-value">{customerLoyalty.redeemedPoints}</p>
              </div>
            </div>
          </div>

          {customerLoyalty.transactions.length > 0 && (
            <div>
              <h3 className="card-title" style={{ marginBottom: 8 }}>Historico</h3>
              <table className="table">
                <thead>
                  <tr>
                    <th>Data</th>
                    <th>Tipo</th>
                    <th>Pontos</th>
                    <th>Observacao</th>
                  </tr>
                </thead>
                <tbody>
                  {customerLoyalty.transactions.map((t: LoyaltyTransaction) => (
                    <tr key={t.id}>
                      <td className="text-muted">
                        {new Date(t.createdAt).toLocaleDateString('pt-BR')}
                      </td>
                      <td>
                        <span
                          className="badge"
                          style={{
                            background: t.type === 'Earn' ? '#dcfce7' : t.type === 'Redeem' ? '#fee2e2' : '#dbeafe',
                            color: t.type === 'Earn' ? '#166534' : t.type === 'Redeem' ? '#991b1b' : '#1e40af',
                          }}
                        >
                          {t.type === 'Earn' ? 'Ganhou' : t.type === 'Redeem' ? 'Resgatou' : 'Ajuste'}
                        </span>
                      </td>
                      <td className="font-semibold">
                        {t.type === 'Earn' ? '+' : '-'}{t.points}
                      </td>
                      <td className="text-muted">{t.notes ?? '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {!selectedCustomer && (
        <div className="card">
          <h2 className="card-title" style={{ marginBottom: 12 }}>Clientes no Programa</h2>
          {isLoading ? (
            <p className="loading">Carregando...</p>
          ) : !loyaltyList.length ? (
            <p className="empty-text">Nenhum cliente no programa de fidelidade ainda.</p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Cliente</th>
                  <th>Pontos Disponiveis</th>
                  <th>Total Ganho</th>
                  <th>Total Resgatado</th>
                  <th>Acoes</th>
                </tr>
              </thead>
              <tbody>
                {loyaltyList.map((l) => (
                  <tr key={l.id}>
                    <td className="font-semibold">{l.customerName}</td>
                    <td>
                      <span style={{ color: '#f59e0b', fontWeight: 700 }}>{l.availablePoints}</span>
                    </td>
                    <td>{l.totalPoints}</td>
                    <td>{l.redeemedPoints}</td>
                    <td>
                      <button
                        className="btn-icon"
                        onClick={() => setSelectedCustomer(l)}
                        title="Ver detalhes"
                      >
                        <Search size={15} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {showRedeemModal && selectedCustomer && (
        <div className="modal-overlay" onClick={() => setShowRedeemModal(false)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Resgatar Pontos</h2>
            <p className="text-muted" style={{ marginBottom: 16 }}>
              Disponivel: <strong>{customerLoyalty?.availablePoints ?? 0}</strong> pontos
            </p>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Pontos a resgatar *</label>
              <input
                type="number"
                min={1}
                max={customerLoyalty?.availablePoints ?? 0}
                value={redeemPoints}
                onChange={(e) => setRedeemPoints(e.target.value)}
                placeholder="Quantidade de pontos"
              />
            </div>
            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Observacao</label>
              <input
                value={redeemNotes}
                onChange={(e) => setRedeemNotes(e.target.value)}
                placeholder="Motivo do resgate (opcional)"
              />
            </div>

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowRedeemModal(false)}>
                Cancelar
              </button>
              <button
                className="btn-primary"
                onClick={() => redeemMutation.mutate()}
                disabled={
                  !redeemPoints ||
                  parseInt(redeemPoints) <= 0 ||
                  parseInt(redeemPoints) > (customerLoyalty?.availablePoints ?? 0) ||
                  redeemMutation.isPending
                }
              >
                {redeemMutation.isPending ? 'Resgatando...' : 'Confirmar Resgate'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
