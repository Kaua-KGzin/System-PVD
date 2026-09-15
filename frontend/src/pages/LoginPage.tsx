import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../api/client'
import { useAuthStore } from '../store/auth'
import type { AuthResponse } from '../types'

export default function LoginPage() {
  const navigate = useNavigate()
  const login = useAuthStore((s) => s.login)
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      const { data } = await api.post<AuthResponse>('/auth/login', { username, password })
      login(data)
      navigate('/')
    } catch {
      setError('Usuário ou senha inválidos.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <span className="login-brand">ARCH</span>
          <span className="login-brand-sub">NEXUS</span>
        </div>
        <p className="login-subtitle">Sistema de Gestão Comercial</p>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="username">Usuário</label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Digite seu usuário"
              required
              autoFocus
            />
          </div>
          <div className="form-group" style={{ marginTop: '10px' }}>
            <label htmlFor="password">Senha</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Digite sua senha"
              required
            />
          </div>
          {error && <p className="login-error">{error}</p>}
          <button type="submit" className="btn-primary w-full" disabled={loading} style={{ marginTop: '20px' }}>
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>

        <div style={{ marginTop: '20px', paddingTop: '20px', borderTop: '1px solid var(--border)', textAlign: 'center' }}>
          <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '10px' }}>
            Acessando para avaliar o portfólio?
          </p>
          <button 
            type="button" 
            className="btn-secondary w-full" 
            onClick={async () => {
              setError('')
              setLoading(true)
              try {
                const { data } = await api.post<AuthResponse>('/auth/login', { username: 'admin', password: 'admin123' })
                login(data)
                navigate('/')
              } catch {
                setError('Falha no login de demonstração. Banco não iniciado?')
              } finally {
                setLoading(false)
              }
            }} 
            disabled={loading}
          >
            Acessar Demo (Admin)
          </button>
        </div>
      </div>
    </div>
  )
}
