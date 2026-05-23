import { useQuery } from '@tanstack/react-query'
import { ShoppingCart, AlertTriangle, DollarSign, TrendingUp, Monitor } from 'lucide-react'
import { api } from '../api/client'
import type { DashboardData, StockAlert } from '../types'

const fmt = (n: number) =>
  n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

function StatCard({ icon: Icon, label, value, color }: {
  icon: React.ElementType; label: string; value: string | number; color: string
}) {
  return (
    <div className={`stat-card border-l-4 ${color}`}>
      <div className="stat-icon"><Icon size={22} /></div>
      <div>
        <p className="stat-label">{label}</p>
        <p className="stat-value">{value}</p>
      </div>
    </div>
  )
}

export default function DashboardPage() {
  const { data: dashboard, isLoading } = useQuery<DashboardData>({
    queryKey: ['dashboard'],
    queryFn: () => api.get('/dashboard').then((r) => r.data),
    refetchInterval: 30000,
  })

  const { data: alerts } = useQuery<StockAlert[]>({
    queryKey: ['stock-alerts'],
    queryFn: () => api.get('/reports/stock-alerts').then((r) => r.data),
  })

  if (isLoading) return <div className="loading">Carregando dashboard...</div>

  const sales = dashboard?.todaySales

  return (
    <div className="page">
      <h1 className="page-title">Dashboard</h1>

      <div className="stats-grid">
        <StatCard icon={ShoppingCart} label="Vendas hoje" value={sales?.count ?? 0} color="border-blue-500" />
        <StatCard icon={DollarSign} label="Receita hoje" value={fmt(sales?.total ?? 0)} color="border-green-500" />
        <StatCard icon={TrendingUp} label="Ticket médio" value={fmt(sales?.averageTicket ?? 0)} color="border-purple-500" />
        <StatCard icon={AlertTriangle} label="Estoque baixo" value={dashboard?.lowStockProducts ?? 0} color="border-yellow-500" />
        <StatCard icon={Monitor} label="Caixas abertos" value={dashboard?.openCashSessions ?? 0} color="border-teal-500" />
      </div>

      <div className="dashboard-bottom">
        <div className="card">
          <h2 className="card-title">Últimas vendas</h2>
          {!dashboard?.recentSales?.length ? (
            <p className="empty-text">Nenhuma venda hoje.</p>
          ) : (
            <table className="table">
              <thead>
                <tr><th>#</th><th>Operador</th><th>Total</th><th>Status</th><th>Hora</th></tr>
              </thead>
              <tbody>
                {dashboard.recentSales.map((sale) => (
                  <tr key={sale.id}>
                    <td>#{sale.number}</td>
                    <td>{sale.operatorName}</td>
                    <td>{fmt(sale.netTotal)}</td>
                    <td><span className={`badge badge-${sale.status.toLowerCase()}`}>{sale.status === 'Completed' ? 'Concluída' : 'Cancelada'}</span></td>
                    <td>{new Date(sale.createdAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {!!alerts?.length && (
          <div className="card">
            <h2 className="card-title">Alertas de estoque</h2>
            <table className="table">
              <thead>
                <tr><th>Produto</th><th>Estoque</th><th>Mínimo</th><th>Déficit</th></tr>
              </thead>
              <tbody>
                {alerts.map((a) => (
                  <tr key={a.id}>
                    <td>{a.name}</td>
                    <td className="text-red-600 font-semibold">{a.stockQuantity}</td>
                    <td>{a.minStockQuantity}</td>
                    <td className="text-red-600">{a.deficit}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
