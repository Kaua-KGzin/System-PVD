import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { SalesSummary, StockAlert } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const methodLabel: Record<string, string> = { Cash: 'Dinheiro', Debit: 'Débito', Credit: 'Crédito', Pix: 'PIX' }

function todayRange() {
  const now = new Date()
  const from = new Date(now.getFullYear(), now.getMonth(), now.getDate()).toISOString()
  const to = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59).toISOString()
  return { from, to }
}

export default function ReportsPage() {
  const [range, setRange] = useState(todayRange())
  const [fromInput, setFromInput] = useState(range.from.slice(0, 10))
  const [toInput, setToInput] = useState(range.to.slice(0, 10))

  const applyRange = () =>
    setRange({
      from: new Date(fromInput).toISOString(),
      to: new Date(toInput + 'T23:59:59').toISOString(),
    })

  const { data: summary } = useQuery<SalesSummary>({
    queryKey: ['report-summary', range],
    queryFn: () =>
      api.get('/reports/sales-summary', { params: { from: range.from, to: range.to } }).then((r) => r.data),
  })

  const { data: alerts } = useQuery<StockAlert[]>({
    queryKey: ['stock-alerts'],
    queryFn: () => api.get('/reports/stock-alerts').then((r) => r.data),
  })

  return (
    <div className="page">
      <h1 className="page-title">Relatórios</h1>

      <div className="card mb-4">
        <div className="filter-row">
          <div className="form-group">
            <label>De</label>
            <input type="date" value={fromInput} onChange={(e) => setFromInput(e.target.value)} />
          </div>
          <div className="form-group">
            <label>Até</label>
            <input type="date" value={toInput} onChange={(e) => setToInput(e.target.value)} />
          </div>
          <button className="btn-primary" onClick={applyRange}>Filtrar</button>
        </div>
      </div>

      {summary && (
        <div className="reports-grid">
          <div className="card">
            <h2 className="card-title">Resumo de vendas</h2>
            <div className="detail-grid">
              <div><strong>Vendas:</strong> {summary.totalSales}</div>
              <div><strong>Receita bruta:</strong> {fmt(summary.totalRevenue)}</div>
              <div><strong>Descontos:</strong> {fmt(summary.totalDiscounts)}</div>
              <div><strong>Receita líquida:</strong> {fmt(summary.netRevenue)}</div>
            </div>
          </div>

          <div className="card">
            <h2 className="card-title">Por forma de pagamento</h2>
            <table className="table">
              <thead><tr><th>Método</th><th>Qtd</th><th>Total</th></tr></thead>
              <tbody>
                {summary.byPaymentMethod.map((m) => (
                  <tr key={m.method}>
                    <td>{methodLabel[m.method] ?? m.method}</td>
                    <td>{m.count}</td>
                    <td>{fmt(m.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {summary.byHour.length > 0 && (
            <div className="card">
              <h2 className="card-title">Vendas por hora</h2>
              <table className="table">
                <thead><tr><th>Hora</th><th>Vendas</th><th>Total</th></tr></thead>
                <tbody>
                  {summary.byHour.map((h) => (
                    <tr key={h.hour}>
                      <td>{String(h.hour).padStart(2, '0')}h</td>
                      <td>{h.count}</td>
                      <td>{fmt(h.total)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {alerts && alerts.length > 0 && (
        <div className="card mt-4">
          <h2 className="card-title">Alertas de estoque baixo</h2>
          <table className="table">
            <thead><tr><th>Produto</th><th>Código</th><th>Estoque atual</th><th>Mínimo</th><th>Déficit</th></tr></thead>
            <tbody>
              {alerts.map((a) => (
                <tr key={a.id} className="row-warning">
                  <td>{a.name}</td>
                  <td className="font-mono text-sm">{a.barcode}</td>
                  <td className="text-red-600 font-semibold">{a.stockQuantity}</td>
                  <td>{a.minStockQuantity}</td>
                  <td className="text-red-600 font-semibold">{a.deficit}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
