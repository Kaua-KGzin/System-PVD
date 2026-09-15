import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowDownRight, ArrowUpRight } from 'lucide-react'
import { api } from '../api/client'
import { useSettingsStore } from '../store/settings'
import type { CashSession, CashMovement } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

export default function CashPage() {
  const qc = useQueryClient()
  const { terminalId, setTerminalId } = useSettingsStore()
  const [openAmount, setOpenAmount] = useState('')
  const [operatorName, setOperatorName] = useState('')
  const [closeAmount, setCloseAmount] = useState('')
  const [closeNotes, setCloseNotes] = useState('')

  // Movimentação de caixa (Sangria / Suprimento)
  const [movType, setMovType] = useState<'Supply' | 'Bleed'>('Supply')
  const [movAmount, setMovAmount] = useState('')
  const [movReason, setMovReason] = useState('')
  const [movError, setMovError] = useState<string | null>(null)

  const { data: session, isLoading } = useQuery<CashSession>({
    queryKey: ['cash-session-open', terminalId],
    queryFn: () => api.get(`/cash-sessions/open/${terminalId}`).then((r) => r.data),
    retry: false,
  })

  const { data: movements = [] } = useQuery<CashMovement[]>({
    queryKey: ['cash-movements', session?.id],
    queryFn: () => api.get(`/cash-sessions/${session!.id}/movements`).then((r) => r.data),
    enabled: !!session?.id,
  })

  const openMutation = useMutation({
    mutationFn: () =>
      api.post('/cash-sessions', {
        terminalId,
        operatorName,
        openingAmount: parseFloat(openAmount) || 0,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['cash-session-open'] }),
  })

  const movementMutation = useMutation({
    mutationFn: () =>
      api.post(`/cash-sessions/${session!.id}/movements`, {
        type: movType,
        amount: parseFloat(movAmount) || 0,
        reason: movReason.trim() || null,
        operatorName: session?.operatorName ?? 'Operador',
      }),
    onSuccess: () => {
      setMovAmount('')
      setMovReason('')
      setMovError(null)
      qc.invalidateQueries({ queryKey: ['cash-session-open'] })
      qc.invalidateQueries({ queryKey: ['cash-movements', session?.id] })
    },
    onError: (err: any) => {
      setMovError(err.response?.data?.error || 'Erro ao registrar movimentação.')
    },
  })

  const closeMutation = useMutation({
    mutationFn: () =>
      api.post(`/cash-sessions/${session!.id}/close`, {
        closingAmount: parseFloat(closeAmount) || 0,
        notes: closeNotes || null,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cash-session-open'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
    },
  })

  if (isLoading) return <div className="page"><p className="loading">Verificando caixa...</p></div>

  return (
    <div className="page">
      <h1 className="page-title">Controle de Caixa — {terminalId}</h1>

      {!session ? (
        <div className="card card-md">
          <h2 className="card-title">Abrir caixa</h2>
          <div className="form-group">
            <label>Terminal</label>
            <input value={terminalId} onChange={(e) => setTerminalId(e.target.value)} placeholder="Ex: CAIXA-01" />
          </div>
          <div className="form-group">
            <label>Operador</label>
            <input value={operatorName} onChange={(e) => setOperatorName(e.target.value)} placeholder="Nome do operador" />
          </div>
          <div className="form-group">
            <label>Valor de abertura (fundo de caixa)</label>
            <input type="number" step="0.01" value={openAmount} onChange={(e) => setOpenAmount(e.target.value)} placeholder="0,00" />
          </div>
          <button
            className="btn-primary w-full"
            onClick={() => openMutation.mutate()}
            disabled={!operatorName.trim() || openMutation.isPending}
          >
            {openMutation.isPending ? 'Abrindo...' : 'Abrir caixa'}
          </button>
        </div>
      ) : (
        <div className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="card">
              <h2 className="card-title">Status do Caixa</h2>
              <div className="detail-grid">
                <div><strong>Operador:</strong> {session.operatorName}</div>
                <div><strong>Abertura:</strong> {new Date(session.openedAt).toLocaleString('pt-BR')}</div>
                <div><strong>Fundo de caixa:</strong> {fmt(session.openingAmount)}</div>
                <div><strong>Saldo esperado:</strong> {fmt(session.expectedClosingAmount)}</div>
              </div>
            </div>

            <div className="card">
              <h2 className="card-title">Movimentação de Tesouraria</h2>
              <p className="text-sm text-gray-500 mb-3">
                Lance sangrias (retirada para o cofre) ou suprimentos (entrada de troco) sem afetar as vendas.
              </p>
              <div className="flex gap-2 mb-3">
                <button
                  type="button"
                  className={`btn flex-1 flex items-center justify-center gap-1 ${movType === 'Supply' ? 'btn-primary' : 'btn-secondary'}`}
                  onClick={() => setMovType('Supply')}
                >
                  <ArrowDownRight size={16} /> Suprimento (Entrada)
                </button>
                <button
                  type="button"
                  className={`btn flex-1 flex items-center justify-center gap-1 ${movType === 'Bleed' ? 'btn-danger' : 'btn-secondary'}`}
                  onClick={() => setMovType('Bleed')}
                >
                  <ArrowUpRight size={16} /> Sangria (Retirada)
                </button>
              </div>
              <div className="form-group">
                <label>Valor (R$)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
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
                  placeholder={movType === 'Supply' ? 'Ex: Reforço de troco' : 'Ex: Recolhimento para cofre'}
                />
              </div>
              {movError && <div className="alert alert-err">{movError}</div>}
              <button
                className="btn-primary w-full"
                onClick={() => movementMutation.mutate()}
                disabled={!movAmount || parseFloat(movAmount) <= 0 || movementMutation.isPending}
              >
                {movementMutation.isPending ? 'Lançando...' : `Confirmar ${movType === 'Supply' ? 'Suprimento' : 'Sangria'}`}
              </button>
            </div>
          </div>

          {movements.length > 0 && (
            <div className="card">
              <h2 className="card-title">Extrato de Movimentações da Sessão</h2>
              <table className="table w-full">
                <thead>
                  <tr>
                    <th>Data/Hora</th>
                    <th>Tipo</th>
                    <th>Valor</th>
                    <th>Motivo</th>
                    <th>Operador</th>
                  </tr>
                </thead>
                <tbody>
                  {movements.map((m) => (
                    <tr key={m.id}>
                      <td>{new Date(m.createdAt).toLocaleTimeString('pt-BR')}</td>
                      <td>
                        <span className={`badge ${m.type === 'Supply' ? 'badge-open' : 'badge-danger'}`}>
                          {m.type === 'Supply' ? 'Suprimento (+)' : 'Sangria (-)'}
                        </span>
                      </td>
                      <td><strong>{fmt(m.amount)}</strong></td>
                      <td>{m.reason || '—'}</td>
                      <td>{m.operatorName}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="card card-md">
            <h2 className="card-title">Fechar caixa</h2>
            <div className="form-group">
              <label>Valor em caixa (contagem física das cédulas e moedas)</label>
              <input
                type="number"
                step="0.01"
                value={closeAmount}
                onChange={(e) => setCloseAmount(e.target.value)}
                placeholder="0,00"
              />
            </div>
            {closeAmount && (
              <div className={`alert ${parseFloat(closeAmount) >= session.expectedClosingAmount ? 'alert-ok' : 'alert-warn'}`}>
                Diferença: {fmt(parseFloat(closeAmount) - session.expectedClosingAmount)}
              </div>
            )}
            <div className="form-group">
              <label>Observações do fechamento</label>
              <input value={closeNotes} onChange={(e) => setCloseNotes(e.target.value)} placeholder="Opcional" />
            </div>
            <button
              className="btn-danger w-full"
              onClick={() => closeMutation.mutate()}
              disabled={!closeAmount || closeMutation.isPending}
            >
              {closeMutation.isPending ? 'Fechando...' : 'Fechar caixa'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
