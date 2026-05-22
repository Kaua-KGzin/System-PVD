import { NavLink } from 'react-router-dom'
import { LayoutDashboard, ShoppingCart, Package, ClipboardList, DollarSign, Truck, FileText, LogOut, Users, Tag } from 'lucide-react'
import { useAuthStore } from '../store/auth'
import { api } from '../api/client'

const links = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/pdv', icon: ShoppingCart, label: 'Frente de Caixa' },
  { to: '/catalog', icon: Package, label: 'Catálogo' },
  { to: '/categories', icon: Tag, label: 'Categorias' },
  { to: '/customers', icon: Users, label: 'Clientes' },
  { to: '/sales', icon: ClipboardList, label: 'Histórico' },
  { to: '/cash', icon: DollarSign, label: 'Caixa' },
  { to: '/suppliers', icon: Truck, label: 'Fornecedores' },
  { to: '/reports', icon: FileText, label: 'Relatórios' },
]

export default function Sidebar() {
  const { username, role, logout } = useAuthStore()

  const handleLogout = async () => {
    const refreshToken = localStorage.getItem('refreshToken')
    if (refreshToken) await api.post('/auth/logout', { refreshToken }).catch(() => {})
    logout()
  }

  return (
    <aside className="sidebar">
      <div className="sidebar-brand">
        <span className="brand-arch">ARCH</span>
        <span className="brand-system">System</span>
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
        <div className="user-info">
          <p className="user-name">{username}</p>
          <p className="user-role">{role}</p>
        </div>
        <button className="btn-icon" onClick={handleLogout} title="Sair">
          <LogOut size={18} />
        </button>
      </div>
    </aside>
  )
}
