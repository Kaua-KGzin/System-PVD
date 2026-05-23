import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { useSettingsStore } from '../store/settings'
import type { CashSession } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

export default function CashPage() {
  const qc = useQueryClient()
  const { terminalId, setTerminalId } = useSettingsStore()
  const [openAmount, setOpenAmount] = useState('')
  const [operatorName, setOperatorName] = useState('')
  const [closeAmount, setCloseAmount] = useState('')
  const [closeNotes, setCloseNotes] = useState('')

  const { data: session, isLoading } = useQuery<CashSession>({
    queryKey: ['cash-session-open', terminalId],
    queryFn: () => api.get(`/cash-sessions/open/${terminalId}`).then((r) => r.data),
    retry: false,
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
        <div className="cash-open-layout">
          <div className="card">
            <h2 className="card-title">Caixa aberto</h2>
            <div className="detail-grid">
              <div><strong>Operador:</strong> {session.operatorName}</div>
              <div><strong>Abertura:</strong> {new Date(session.openedAt).toLocaleString('pt-BR')}</div>
              <div><strong>Fundo de caixa:</strong> {fmt(session.openingAmount)}</div>
              <div><strong>Saldo esperado:</strong> {fmt(session.expectedClosingAmount)}</div>
            </div>
          </div>

          <div className="card card-md">
            <h2 className="card-title">Fechar caixa</h2>
            <div className="form-group">
              <label>Valor em caixa (contagem física)</label>
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
              <label>Observações</label>
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
