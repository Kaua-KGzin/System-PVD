import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { XCircle, Eye, RotateCcw } from 'lucide-react'
import { api } from '../api/client'
import type { PagedResponse, Sale, SaleReturn } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const fmtDate = (s: string) =>
  new Date(s).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })

export default function SalesHistoryPage() {
  const qc = useQueryClient()
  const [page, setPage] = useState(1)
  const [detail, setDetail] = useState<Sale | null>(null)
  const [cancelId, setCancelId] = useState<string | null>(null)
  const [cancelReason, setCancelReason] = useState('')

  // Devoluções / Trocas
  const [returnSale, setReturnSale] = useState<Sale | null>(null)
  const [returnQuantities, setReturnQuantities] = useState<Record<string, number>>({})
  const [returnReason, setReturnReason] = useState('')
  const [returnMsg, setReturnMsg] = useState<{ type: 'ok' | 'err'; text: string } | null>(null)

  const { data, isLoading } = useQuery<PagedResponse<Sale>>({
    queryKey: ['sales', page],
    queryFn: () => api.get('/sales', { params: { page, pageSize: 10 } }).then((r) => r.data),
  })

  // Consulta devoluções existentes da venda selecionada
  const { data: saleReturns = [] } = useQuery<SaleReturn[]>({
    queryKey: ['sale-returns', detail?.id],
    queryFn: () => api.get(`/sales/${detail!.id}/returns`).then((r) => r.data),
    enabled: !!detail?.id,
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

  const returnMutation = useMutation({
    mutationFn: () => {
      const items = Object.entries(returnQuantities)
        .filter(([_, qty]) => qty > 0)
        .map(([productId, quantity]) => ({ productId, quantity }))

      return api.post(`/sales/${returnSale!.id}/returns`, {
        reason: returnReason.trim() || 'Devolução/Troca de mercadoria',
        operatorName: 'Operador',
        items,
      })
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sales'] })
      setReturnMsg({ type: 'ok', text: 'Devolução registrada! Mercadoria reposta ao estoque.' })
      setTimeout(() => {
        setReturnSale(null)
        setReturnQuantities({})
        setReturnReason('')
        setReturnMsg(null)
      }, 2500)
    },
    onError: (err: any) => {
      setReturnMsg({ type: 'err', text: err.response?.data?.error || 'Erro ao registrar devolução.' })
    },
  })

  const openReturnModal = (sale: Sale) => {
    setReturnSale(sale)
    const initial: Record<string, number> = {}
    sale.items.forEach((i) => {
      initial[i.productId] = 0
    })
    setReturnQuantities(initial)
  }

  return (
    <div className="page">
      <h1 className="page-title">Histórico de Vendas</h1>

      <div className="card">
        {isLoading ? (
          <p className="loading">Carregando...</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>#</th>
                <th>Operador</th>
                <th>Itens</th>
                <th>Total</th>
                <th>Pagamento</th>
                <th>Status</th>
                <th>Data</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {data?.items.map((sale) => (
                <tr key={sale.id}>
                  <td>#{sale.number}</td>
                  <td>{sale.operatorName}</td>
                  <td>{sale.items?.length ?? 0}</td>
                  <td>{fmt(sale.netTotal)}</td>
                  <td>{sale.payments?.map((p) => p.method).join(', ')}</td>
                  <td>
                    <span className={`badge badge-${sale.status.toLowerCase()}`}>
                      {sale.status === 'Completed' ? 'Concluída' : 'Cancelada'}
                    </span>
                  </td>
                  <td>{fmtDate(sale.createdAt)}</td>
                  <td className="actions">
                    <button className="btn-icon" onClick={() => setDetail(sale)} title="Ver detalhes">
                      <Eye size={15} />
                    </button>
                    {sale.status === 'Completed' && (
                      <>
                        <button
                          className="btn-icon"
                          onClick={() => openReturnModal(sale)}
                          title="Devolução / Troca"
                        >
                          <RotateCcw size={15} className="text-primary" />
                        </button>
                        <button
                          className="btn-icon-danger"
                          onClick={() => setCancelId(sale.id)}
                          title="Cancelar Venda"
                        >
                          <XCircle size={15} />
                        </button>
                      </>
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

      {/* Detalhes da Venda */}
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

            <h3 className="mt-4">Itens da Venda</h3>
            <table className="table">
              <thead>
                <tr>
                  <th>Produto</th>
                  <th>Qtd</th>
                  <th>Preço un.</th>
                  <th>Total</th>
                </tr>
              </thead>
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

            {saleReturns.length > 0 && (
              <div className="mt-4 p-3 bg-gray-50 border rounded">
                <h4 className="font-semibold text-sm text-primary mb-2">Devoluções / Trocas Realizadas nesta Venda:</h4>
                {saleReturns.map((ret) => (
                  <div key={ret.id} className="text-xs mb-2 pb-2 border-b last:border-b-0">
                    <p><strong>Data:</strong> {new Date(ret.returnedAt).toLocaleString('pt-BR')} | <strong>Motivo:</strong> {ret.reason}</p>
                    <p><strong>Estorno/Crédito:</strong> {fmt(ret.totalRefundAmount)}</p>
                    <p className="text-gray-600">
                      Itens devolvidos: {ret.items.map((it) => `${it.quantity}x ${it.productName}`).join(', ')}
                    </p>
                  </div>
                ))}
              </div>
            )}

            <div className="modal-actions mt-4">
              <button className="btn-secondary" onClick={() => setDetail(null)}>Fechar</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal de Devolução / Troca */}
      {returnSale && (
        <div className="modal-overlay" onClick={() => setReturnSale(null)}>
          <div className="modal modal-md" onClick={(e) => e.stopPropagation()}>
            <h2>Devolução / Troca — Venda #{returnSale.number}</h2>
            <p className="text-sm text-gray-500 mb-3">
              Informe a quantidade a ser devolvida para cada item. Os produtos retornarão automaticamente ao estoque.
            </p>

            {returnMsg && <div className={`alert alert-${returnMsg.type === 'ok' ? 'ok' : 'err'} mb-3`}>{returnMsg.text}</div>}

            <div className="space-y-3 max-h-60 overflow-y-auto mb-3">
              {returnSale.items.map((item) => (
                <div key={item.productId} className="flex justify-between items-center p-2 border rounded">
                  <div>
                    <p className="font-medium text-sm">{item.productName}</p>
                    <p className="text-xs text-gray-500">Vendido: {item.quantity} un. ({fmt(item.unitPrice)})</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <label className="text-xs">Devolver:</label>
                    <input
                      type="number"
                      min="0"
                      max={item.quantity}
                      step="1"
                      className="w-20 text-center"
                      value={returnQuantities[item.productId] ?? 0}
                      onChange={(e) =>
                        setReturnQuantities({
                          ...returnQuantities,
                          [item.productId]: Math.min(item.quantity, Math.max(0, Number(e.target.value))),
                        })
                      }
                    />
                  </div>
                </div>
              ))}
            </div>

            <div className="form-group mb-3">
              <label>Motivo da Devolução / Troca</label>
              <input
                value={returnReason}
                onChange={(e) => setReturnReason(e.target.value)}
                placeholder="Ex: Tamanho incorreto, produto com defeito, arrependimento"
              />
            </div>

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setReturnSale(null)}>Cancelar</button>
              <button
                className="btn-primary"
                onClick={() => returnMutation.mutate()}
                disabled={
                  returnMutation.isPending ||
                  Object.values(returnQuantities).every((q) => q === 0)
                }
              >
                {returnMutation.isPending ? 'Processando...' : 'Confirmar Devolução'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Cancelamento de Venda */}
      {cancelId && (
        <div className="modal-overlay" onClick={() => setCancelId(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Cancelar Venda</h2>
            <p>O cancelamento irá estornar toda a venda e retornar todos os produtos ao estoque.</p>
            <div className="form-group mt-4">
              <label>Motivo do cancelamento</label>
              <input
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                placeholder="Ex: Erro de digitação, desistência"
              />
            </div>
            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setCancelId(null)}>Voltar</button>
              <button
                className="btn-danger"
                disabled={!cancelReason.trim() || cancelMutation.isPending}
                onClick={() => cancelMutation.mutate(cancelId)}
              >
                {cancelMutation.isPending ? 'Cancelando...' : 'Confirmar cancelamento'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
