import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { LayoutDashboard, ShoppingCart, Package, ClipboardList, DollarSign, Truck, FileText, LogOut, Users, Tag, UserCog, Warehouse, History, Key, Sun, Moon, Star, Percent } from 'lucide-react'
import { useAuthStore } from '../store/auth'
import { useSettingsStore } from '../store/settings'
import { api } from '../api/client'
import { errorDetail } from '../api/errors'

const links = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/pdv', icon: ShoppingCart, label: 'Frente de Caixa' },
  { to: '/catalog', icon: Package, label: 'Catalogo' },
  { to: '/categories', icon: Tag, label: 'Categorias' },
  { to: '/customers', icon: Users, label: 'Clientes' },
  { to: '/sales', icon: ClipboardList, label: 'Historico' },
  { to: '/cash', icon: DollarSign, label: 'Caixa' },
  { to: '/suppliers', icon: Truck, label: 'Fornecedores' },
  { to: '/purchase-entries', icon: Warehouse, label: 'Entradas' },
  { to: '/users', icon: UserCog, label: 'Usuarios' },
  { to: '/audit', icon: History, label: 'Auditoria' },
  { to: '/loyalty', icon: Star, label: 'Fidelidade' },
  { to: '/commissions', icon: Percent, label: 'Comissoes' },
  { to: '/reports', icon: FileText, label: 'Relatorios' },
]

export default function Sidebar() {
  const { username, role, logout } = useAuthStore()
  const { darkMode, toggleDarkMode } = useSettingsStore()
  const [showPasswordModal, setShowPasswordModal] = useState(false)
  const [passwordForm, setPasswordForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' })
  const [passwordError, setPasswordError] = useState('')
  const [passwordSuccess, setPasswordSuccess] = useState(false)

  const handleLogout = async () => {
    const refreshToken = localStorage.getItem('refreshToken')
    if (refreshToken) await api.post('/auth/logout', { refreshToken }).catch(() => {})
    logout()
  }

  const handleChangePassword = async () => {
    setPasswordError('')
    setPasswordSuccess(false)

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordError('As senhas nao conferem.')
      return
    }

    if (passwordForm.newPassword.length < 12) {
      setPasswordError('A nova senha precisa ter no minimo 12 caracteres.')
      return
    }

    try {
      await api.post('/users/change-password', {
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
      })
      setPasswordSuccess(true)
      setPasswordForm({ currentPassword: '', newPassword: '', confirmPassword: '' })
      setTimeout(() => {
        setShowPasswordModal(false)
        setPasswordSuccess(false)
      }, 2000)
    } catch (e) {
      setPasswordError(errorDetail(e, 'Erro ao alterar senha.'))
    }
  }

  return (
    <aside className="sidebar">
      <div className="sidebar-brand">
        <span className="brand-arch">ARCH</span>
        <span className="brand-nexus">NEXUS</span>
      </div>

      <nav className="sidebar-nav">
        {links.map(({ to, icon: Icon, label }) => (
          <NavLink
            key={to}
            to={to}
            end={to === '/'}
            className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}
          >
            <Icon size={18} />
            <span>{label}</span>
          </NavLink>
        ))}
      </nav>

      <div className="sidebar-footer">
        <div className="user-info" style={{ cursor: 'pointer' }} onClick={() => setShowPasswordModal(true)}>
          <p className="user-name">{username}</p>
          <p className="user-role">{role}</p>
        </div>
        <button className="btn-icon" onClick={toggleDarkMode} title={darkMode ? 'Modo claro' : 'Modo escuro'}>
          {darkMode ? <Sun size={16} /> : <Moon size={16} />}
        </button>
        <button className="btn-icon" onClick={() => setShowPasswordModal(true)} title="Alterar senha">
          <Key size={16} />
        </button>
        <button className="btn-icon" onClick={handleLogout} title="Sair">
          <LogOut size={18} />
        </button>
      </div>

      {showPasswordModal && (
        <div className="modal-overlay" onClick={() => setShowPasswordModal(false)}>
          <div className="modal modal-sm" onClick={(e) => e.stopPropagation()}>
            <h2>Alterar Senha</h2>

            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Senha atual *</label>
              <input
                type="password"
                value={passwordForm.currentPassword}
                onChange={(e) => setPasswordForm((f) => ({ ...f, currentPassword: e.target.value }))}
                placeholder="Sua senha atual"
                autoFocus
              />
            </div>
            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Nova senha *</label>
              <input
                type="password"
                value={passwordForm.newPassword}
                onChange={(e) => setPasswordForm((f) => ({ ...f, newPassword: e.target.value }))}
                placeholder="Minimo 12 caracteres"
              />
            </div>
            <div className="form-group" style={{ marginBottom: 12 }}>
              <label>Confirmar nova senha *</label>
              <input
                type="password"
                value={passwordForm.confirmPassword}
                onChange={(e) => setPasswordForm((f) => ({ ...f, confirmPassword: e.target.value }))}
                placeholder="Repita a nova senha"
              />
            </div>

            {passwordError && <p className="login-error" style={{ marginTop: 4 }}>{passwordError}</p>}
            {passwordSuccess && <p className="alert alert-ok" style={{ marginTop: 4 }}>Senha alterada com sucesso!</p>}

            <div className="modal-actions">
              <button className="btn-secondary" onClick={() => setShowPasswordModal(false)}>
                Cancelar
              </button>
              <button
                className="btn-primary"
                onClick={handleChangePassword}
                disabled={!passwordForm.currentPassword || !passwordForm.newPassword || !passwordForm.confirmPassword}
              >
                Alterar Senha
              </button>
            </div>
          </div>
        </div>
      )}
    </aside>
  )
}
