import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { api } from '../api/client'

interface AuditLogEntry {
  id: string
  userId: string | null
  username: string | null
  action: string
  entityName: string
  entityId: string | null
  changesJson: string | null
  ipAddress: string | null
  timestamp: string
}

export default function AuditPage() {
  const [page, setPage] = useState(1)
  const [entityFilter, setEntityFilter] = useState('')
  const [actionFilter, setActionFilter] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['audit-logs', page, entityFilter, actionFilter],
    queryFn: () =>
      api
        .get('/audit-logs', {
          params: {
            page,
            pageSize: 50,
            entityName: entityFilter || undefined,
            action: actionFilter || undefined,
          },
        })
        .then((r) => r.data),
  })

  const logs: AuditLogEntry[] = data?.items ?? []
  const totalPages = data?.totalPages ?? 1

  const getActionBadge = (action: string) => {
    const colors: Record<string, { bg: string; color: string }> = {
      Create: { bg: '#dcfce7', color: '#166534' },
      Update: { bg: '#dbeafe', color: '#1e40af' },
      Delete: { bg: '#fee2e2', color: '#991b1b' },
    }
    const style = colors[action] ?? { bg: '#f3f4f6', color: '#374151' }
    return (
      <span className="badge" style={{ background: style.bg, color: style.color }}>
        {action}
      </span>
    )
  }

  const formatChanges = (json: string | null) => {
    if (!json) return <span className="text-muted">-</span>
    try {
      const changes = JSON.parse(json)
      const keys = Object.keys(changes)
      if (keys.length === 0) return <span className="text-muted">-</span>
      return (
        <div style={{ fontSize: 12 }}>
          {keys.slice(0, 3).map((key) => (
            <div key={key}>
              <span className="text-muted">{key}:</span>{' '}
              <span className="font-mono">
                {typeof changes[key] === 'object'
                  ? JSON.stringify(changes[key])
                  : String(changes[key])}
              </span>
            </div>
          ))}
          {keys.length > 3 && <div className="text-muted">+{keys.length - 3} mais</div>}
        </div>
      )
    } catch {
      return <span className="text-muted font-mono" style={{ fontSize: 11 }}>{json.slice(0, 50)}</span>
    }
  }

  return (
    <div className="page">
      <div className="page-header">
        <h1 className="page-title">Auditoria</h1>
      </div>

      <div className="filters-row">
        <div className="form-group">
          <label>Entidade</label>
          <select
            value={entityFilter}
            onChange={(e) => {
              setEntityFilter(e.target.value)
              setPage(1)
            }}
          >
            <option value="">Todas</option>
            <option value="Product">Produto</option>
            <option value="Category">Categoria</option>
            <option value="Customer">Cliente</option>
            <option value="Supplier">Fornecedor</option>
            <option value="Sale">Venda</option>
            <option value="CashSession">Caixa</option>
            <option value="User">Usuario</option>
          </select>
        </div>
        <div className="form-group">
          <label>Acao</label>
          <select
            value={actionFilter}
            onChange={(e) => {
              setActionFilter(e.target.value)
              setPage(1)
            }}
          >
            <option value="">Todas</option>
            <option value="Create">Criar</option>
            <option value="Update">Atualizar</option>
            <option value="Delete">Excluir</option>
          </select>
        </div>
        <div style={{ alignSelf: 'flex-end' }}>
          <span className="text-muted" style={{ fontSize: 13 }}>
            {data?.totalCount ?? 0} registro{data?.totalCount !== 1 ? 's' : ''}
          </span>
        </div>
      </div>

      {isLoading ? (
        <p className="loading">Carregando...</p>
      ) : !logs.length ? (
        <div className="card">
          <p className="empty-text">Nenhum registro de auditoria encontrado.</p>
        </div>
      ) : (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Data/Hora</th>
                <th>Usuario</th>
                <th>Acao</th>
                <th>Entidade</th>
                <th>Alteracoes</th>
                <th>IP</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr key={log.id}>
                  <td className="text-muted" style={{ whiteSpace: 'nowrap' }}>
                    {new Date(log.timestamp).toLocaleString('pt-BR')}
                  </td>
                  <td className="font-semibold">{log.username ?? '-'}</td>
                  <td>{getActionBadge(log.action)}</td>
                  <td>
                    <span className="badge" style={{ background: '#f3f4f6', color: '#374151' }}>
                      {log.entityName}
                    </span>
                  </td>
                  <td>{formatChanges(log.changesJson)}</td>
                  <td className="font-mono text-muted" style={{ fontSize: 12 }}>
                    {log.ipAddress ?? '-'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {totalPages > 1 && (
            <div className="pagination">
              <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1}>
                <ChevronLeft size={14} /> Anterior
              </button>
              <span>
                Pagina {page} de {totalPages}
              </span>
              <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page === totalPages}>
                Proximo <ChevronRight size={14} />
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
