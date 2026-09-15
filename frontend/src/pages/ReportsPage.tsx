import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { api } from '../api/client'
import type { SalesSummary, StockAlert } from '../types'

const fmt = (n: number) => n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
const methodLabel: Record<string, string> = { Cash: 'Dinheiro', Debit: 'Debito', Credit: 'Credito', Pix: 'PIX', Voucher: 'Voucher', StoreCredit: 'Credito Loja' }

function formatLocalDate(d: Date): string {
  const year = d.getFullYear()
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function todayRange() {
  const now = new Date()
  const dateStr = formatLocalDate(now)
  return {
    from: new Date(`${dateStr}T00:00:00`).toISOString(),
    to: new Date(`${dateStr}T23:59:59.999`).toISOString(),
    dateStr,
  }
}

function exportToCSV(data: Record<string, unknown>[], filename: string) {
  if (!data.length) return
  const headers = Object.keys(data[0])
  const csvContent = [
    headers.join(';'),
    ...data.map((row) => headers.map((h) => String(row[h] ?? '')).join(';')),
  ].join('\n')

  const BOM = '\uFEFF'
  const blob = new Blob([BOM + csvContent], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = filename
  link.click()
  URL.revokeObjectURL(link.href)
}

export default function ReportsPage() {
  const initial = todayRange()
  const [range, setRange] = useState({ from: initial.from, to: initial.to })
  const [fromInput, setFromInput] = useState(initial.dateStr)
  const [toInput, setToInput] = useState(initial.dateStr)

  const applyRange = () =>
    setRange({
      from: new Date(`${fromInput}T00:00:00`).toISOString(),
      to: new Date(`${toInput}T23:59:59.999`).toISOString(),
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

  const exportSummaryCSV = () => {
    if (!summary) return
    const data = summary.byPaymentMethod.map((m) => ({
      Metodo: methodLabel[m.method] ?? m.method,
      Quantidade: m.count,
      Total: m.total,
    }))
    exportToCSV(data, `resumo-pagamentos-${fromInput}.csv`)
  }

  const exportHourlyCSV = () => {
    if (!summary) return
    const data = summary.byHour.map((h) => ({
      Hora: `${String(h.hour).padStart(2, '0')}:00`,
      Vendas: h.count,
      Total: h.total,
    }))
    exportToCSV(data, `vendas-por-hora-${fromInput}.csv`)
  }

  const exportStockAlertsCSV = () => {
    if (!alerts?.length) return
    const data = alerts.map((a) => ({
      Produto: a.name,
      Codigo: a.barcode,
      EstoqueAtual: a.stockQuantity,
      Minimo: a.minStockQuantity,
      Deficit: a.deficit,
    }))
    exportToCSV(data, `alertas-estoque-${formatLocalDate(new Date())}.csv`)
  }

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
            <div className="page-header" style={{ marginBottom: 12 }}>
              <h2 className="card-title">Por forma de pagamento</h2>
              <button className="btn-icon" onClick={exportSummaryCSV} title="Exportar CSV">
                <Download size={16} />
              </button>
            </div>
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
              <div className="page-header" style={{ marginBottom: 12 }}>
                <h2 className="card-title">Vendas por hora</h2>
                <button className="btn-icon" onClick={exportHourlyCSV} title="Exportar CSV">
                  <Download size={16} />
                </button>
              </div>
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
          <div className="page-header" style={{ marginBottom: 12 }}>
            <h2 className="card-title">Alertas de estoque baixo</h2>
            <button className="btn-icon" onClick={exportStockAlertsCSV} title="Exportar CSV">
              <Download size={16} />
            </button>
          </div>
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
