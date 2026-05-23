import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { XCircle, Eye } from 'lucide-react'
import { api } from '../api/client'
import type { PagedResponse, Sale } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const fmtDate = (s: string) =>
  new Date(s).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })

export default function SalesHistoryPage() {
  const qc = useQueryClient()
  const [page, setPage] = useState(1)
  const [detail, setDetail] = useState<Sale | null>(null)
  const [cancelId, setCancelId] = useState<string | null>(null)
  const [cancelReason, setCancelReason] = useState('')

  const { data, isLoading } = useQuery<PagedResponse<Sale>>({
    queryKey: ['sales', page],
    queryFn: () => api.get('/sales', { params: { page, pageSize: 10 } }).then((r) => r.data),
  })

  const cancelMutation = useMutation({
    mutationFn: (id: string) => api.post(`/sales/${id}/cancel`, { reason: cancelReason }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sales'] })
      qc.invalidateQueries({ queryKey: ['dashboard'] })
      setCancelId(null)
      setCancelReason('')
    },
  })

  return (
    <div className="page">
      <h1 className="page-title">Histórico de Vendas</h1>

      <div className="card">
        {isLoading ? (
          <p className="loading">Carregando...</p>
        ) : (
          <table className="table">
            <thead>
              <tr><th>#</th><th>Operador</th><th>Itens</th><th>Total</th><th>Pagamento</th><th>Status</th><th>Data</th><th>Ações</th></tr>
            </thead>
            <tbody>
              {data?.items.map((sale) => (
                <tr key={sale.id}>
                  <td>#{sale.number}</td>
                  <td>{sale.operatorName}</td>
                  <td>{sale.items?.length ?? 0}</td>
                  <td>{fmt(sale.netTotal)}</td>
                  <td>{sale.payments?.map((p) => p.method).join(', ')}</td>
                  <td><span className={`badge badge-${sale.status.toLowerCase()}`}>{sale.status === 'Completed' ? 'Concluída' : 'Cancelada'}</span></td>
                  <td>{fmtDate(sale.createdAt)}</td>
                  <td className="actions">
                    <button className="btn-icon" onClick={() => setDetail(sale)} title="Ver detalhes"><Eye size={15} /></button>
                    {sale.status === 'Completed' && (
                      <button className="btn-icon-danger" onClick={() => setCancelId(sale.id)} title="Cancelar"><XCircle size={15} /></button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <div className="pagination">
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</button>
          <span>Página {data?.page} de {data?.totalPages ?? 1} — {data?.totalCount ?? 0} vendas</span>
          <button disabled={!data || page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Próxima</button>
        </div>
      </div>

      {detail && (
        <div className="modal-overlay" onClick={() => setDetail(null)}>
          <div className="modal modal-lg" onClick={(e) => e.stopPropagation()}>
            <h2>Venda #{detail.number}</h2>
            <div className="detail-grid">
              <div><strong>Operador:</strong> {detail.operatorName}</div>
              <div><strong>Status:</strong> <span className={`badge badge-${detail.status.toLowerCase()}`}>{detail.status}</span></div>
              <div><strong>Total bruto:</strong> {fmt(detail.grossTotal)}</div>
              <div><strong>Total líquido:</strong> {fmt(detail.netTotal)}</div>
              <div><strong>Pago:</strong> {fmt(detail.amountPaid)}</div>
              <div><strong>Troco:</strong> {fmt(detail.changeAmount)}</div>
            </div>
            <h3 className="mt-4">Itens</h3>
            <table className="table">
              <thead><tr><th>Produto</th><th>Qtd</th><th>Preço un.</th><th>Total</th></tr></thead>
              <tbody>
                {detail.items?.map((i) => (
                  <tr key={i.productId}>
                    <td>{i.productName}</td>
                    <td>{i.quantity}</td>
                    <td>{fmt(i.unitPrice)}</td>
                    <td>{fmt(i.netTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="modal-actions mt-4">
              <button className="btn-secondary" onClick={() => setDetail(null)}>Fechar</button>
            </div>
          </div>
        </div>
      )}

      {cancelId && (
        <div className="modal-overlay" onClick={() => setCancelId(null)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Cancelar venda</h2>
            <div className="form-group">
              <label>Motivo do cancelamento</label>
              <input value={cancelReason} onChange={(e) => setCancelReason(e.target.value)} placeholder="Ex: Erro de digitação" />
            </div>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setCancelId(null)}>Voltar</button>
              <button
                className="btn-danger"
                onClick={() => cancelMutation.mutate(cancelId)}
                disabled={!cancelReason.trim() || cancelMutation.isPending}
              >
                Confirmar cancelamento
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
